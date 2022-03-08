// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Primitives;

namespace Microsoft.AspNetCore.Mvc.ApiExplorer;

internal class EndpointMetadataApiDescriptionProvider : IApiDescriptionProvider
{
    private readonly EndpointDataSource _endpointDataSource;
    private readonly IHostEnvironment _environment;
    private readonly IServiceProviderIsService? _serviceProviderIsService;
    private readonly ParameterBindingMethodCache ParameterBindingMethodCache = new();
    private readonly ParameterPolicyFactory _parameterPolicyFactory;

    // Executes before MVC's DefaultApiDescriptionProvider and GrpcHttpApiDescriptionProvider for no particular reason.
    public int Order => -1100;

    public EndpointMetadataApiDescriptionProvider(
        EndpointDataSource endpointDataSource,
        IHostEnvironment environment,
        ParameterPolicyFactory parameterPolicyFactory,
        IServiceProviderIsService? serviceProviderIsService)
    {
        _endpointDataSource = endpointDataSource;
        _environment = environment;
        _serviceProviderIsService = serviceProviderIsService;
        _parameterPolicyFactory = parameterPolicyFactory;
    }

    public void OnProvidersExecuting(ApiDescriptionProviderContext context)
    {
        // Keep in sync with EndpointRouteBuilderExtensions.cs
        static bool ShouldDisableInferredBody(string method)
        {
            // GET, DELETE, HEAD, CONNECT, TRACE, and OPTIONS normally do not contain bodies
            return method.Equals(HttpMethods.Get, StringComparison.Ordinal) ||
                   method.Equals(HttpMethods.Delete, StringComparison.Ordinal) ||
                   method.Equals(HttpMethods.Head, StringComparison.Ordinal) ||
                   method.Equals(HttpMethods.Options, StringComparison.Ordinal) ||
                   method.Equals(HttpMethods.Trace, StringComparison.Ordinal) ||
                   method.Equals(HttpMethods.Connect, StringComparison.Ordinal);
        }

        foreach (var endpoint in _endpointDataSource.Endpoints)
        {
            if (endpoint is RouteEndpoint routeEndpoint &&
                routeEndpoint.Metadata.GetMetadata<MethodInfo>() is { } methodInfo &&
                routeEndpoint.Metadata.GetMetadata<IHttpMethodMetadata>() is { } httpMethodMetadata &&
                routeEndpoint.Metadata.GetMetadata<IExcludeFromDescriptionMetadata>() is null or { ExcludeFromDescription: false })
            {
                // We need to detect if any of the methods allow inferred body
                var disableInferredBody = httpMethodMetadata.HttpMethods.Any(ShouldDisableInferredBody);

                // REVIEW: Should we add an ApiDescription for endpoints without IHttpMethodMetadata? Swagger doesn't handle
                // a null HttpMethod even though it's nullable on ApiDescription, so we'd need to define "default" HTTP methods.
                // In practice, the Delegate will be called for any HTTP method if there is no IHttpMethodMetadata.
                foreach (var httpMethod in httpMethodMetadata.HttpMethods)
                {
                    var withApiDescription = routeEndpoint.Metadata.GetMetadata<Action<EndpointApiDescription>>();
                    var apiDescription = CreateEndpointApiDescription(routeEndpoint, httpMethod, methodInfo, disableInferredBody);
                    if (withApiDescription is not null)
                    {
                        withApiDescription(apiDescription);
                    }

                    context.Results.Add(apiDescription.TransformToApiDescription());
                }
            }
        }
    }

    public void OnProvidersExecuted(ApiDescriptionProviderContext context)
    {
    }

    private EndpointApiDescription CreateEndpointApiDescription(
        RouteEndpoint routeEndpoint,
        string httpMethod,
        MethodInfo methodInfo,
        bool disableInferredBody)
    {
        var apiDescription = new EndpointApiDescription
        {
            HttpMethod = httpMethod,
            GroupName = routeEndpoint.Metadata.GetMetadata<IEndpointGroupNameMetadata>()?.EndpointGroupName,
            Metadata = routeEndpoint.Metadata
        };

        // Swashbuckle uses the "controller" name to group endpoints together.
        // For now, put all methods defined the same declaring type together.
        if (methodInfo.DeclaringType is not null && !TypeHelper.IsCompilerGeneratedType(methodInfo.DeclaringType))
        {
            apiDescription.Items["controllerName"] = methodInfo.DeclaringType.Name;
        }
        else
        {
            // If the declaring type is null or compiler-generated (e.g. lambdas),
            // group the methods under the application name.
            apiDescription.Items["controllerName"] = _environment.ApplicationName;
        }

        apiDescription.Items["RelativePath"] = routeEndpoint.RoutePattern.RawText?.TrimStart('/');
        apiDescription.Items["DisplayName"] = routeEndpoint.DisplayName;

        var hasBodyOrFormFileParameter = false;

        foreach (var parameter in methodInfo.GetParameters())
        {
            var parameterDescription = CreateEndpointApiParameter(
                parameter,
                routeEndpoint.RoutePattern,
                disableInferredBody);

            if (parameterDescription is null)
            {
                continue;
            }

            apiDescription.Parameters?.Add(parameter.Name!, parameterDescription);

            hasBodyOrFormFileParameter |=
                parameterDescription.Source == ParameterSource.Body ||
                parameterDescription.Source == ParameterSource.FormFile;
        }

        // Get IAcceptsMetadata.
        var acceptsMetadata = routeEndpoint.Metadata.GetMetadata<IAcceptsMetadata>();
        if (acceptsMetadata is not null)
        {
            // Add a default body parameter if there was no explicitly defined parameter associated with
            // either the body or a form and the user explicity defined some metadata describing the
            // content types the endpoint consumes (such as Accepts<TRequest>(...) or [Consumes(...)]).
            if (!hasBodyOrFormFileParameter)
            {
                var acceptsRequestType = acceptsMetadata.RequestType;
                var isOptional = acceptsMetadata.IsOptional;
                
                var name = acceptsRequestType is not null ? acceptsRequestType.Name : typeof(void).Name;
                var parameterDescription = new EndpointApiParameter
                {
                    Name = name,
                    Source = ParameterSource.Body,
                    ParameterType = acceptsRequestType ?? typeof(void),
                    ContentTypes = acceptsMetadata.ContentTypes
                };

                parameterDescription.Items["IsRequired"] = !isOptional;
                parameterDescription.Items["ModelMetadata"] = CreateModelMetadata(acceptsRequestType ?? typeof(void));

                apiDescription.Parameters?.Add(name, parameterDescription);
            }

            apiDescription.Items["SupportedRequestFormats"] = acceptsMetadata.ContentTypes.Select(contentType => new ApiRequestFormat { MediaType = contentType }).ToList();
        }

        PopulateEndpointResponses(apiDescription, methodInfo.ReturnType, routeEndpoint.Metadata);
        return apiDescription;
    }

    private EndpointApiParameter? CreateEndpointApiParameter(ParameterInfo parameter, RoutePattern pattern, bool disableInferredBody)
    {
        var (source, name, allowEmpty, paramType) = GetParameterSourceAndName(parameter, pattern, disableInferredBody);

        // Services are ignored because they are not request parameters.
        if (source == ParameterSource.Services)
        {
            return null;
        }

        // Determine the "requiredness" based on nullability, default value or if allowEmpty is set
        var nullabilityContext = new NullabilityInfoContext();
        var nullability = nullabilityContext.Create(parameter);
        var isOptional = parameter.HasDefaultValue || nullability.ReadState != NullabilityState.NotNull || allowEmpty;

        var endpointApiParameter =  new EndpointApiParameter
        {
            Name = name,
            Source = source,
            ParameterType = parameter.ParameterType,
            ParameterInfo = parameter
        };

        endpointApiParameter.Items["IsRequired"] = !isOptional;
        endpointApiParameter.Items["Constraints"] = GetParameterConstraints(pattern, parameter);
        endpointApiParameter.Items["DefaultValue"] = parameter.DefaultValue;
        endpointApiParameter.Items["ModelMetadata"] = CreateModelMetadata(paramType);

        return endpointApiParameter;
    }

    private List<IRouteConstraint>? GetParameterConstraints(RoutePattern pattern, ParameterInfo parameter)
    {
        if (parameter.Name is null)
        {
            throw new InvalidOperationException($"Encountered a parameter of type '{parameter.ParameterType}' without a name. Parameters must have a name.");
        }

        // Only produce a `RouteInfo` property for parameters that are defined in the route template
        if (pattern.GetParameter(parameter.Name) is not RoutePatternParameterPart parameterPart)
        {
            return null;
        }

        var constraints = new List<IRouteConstraint>();

        if (pattern.ParameterPolicies.TryGetValue(parameter.Name, out var parameterPolicyReferences))
        {
            foreach (var parameterPolicyReference in parameterPolicyReferences)
            {
                var policy = _parameterPolicyFactory.Create(parameterPart, parameterPolicyReference);
                if (policy is IRouteConstraint generatedConstraint)
                {
                    constraints.Add(generatedConstraint);
                }
            }
        }

        return constraints;
    }

    // TODO: Share more of this logic with RequestDelegateFactory.CreateArgument(...) using RequestDelegateFactoryUtilities
    // which is shared source.
    private (ParameterSource, string, bool, Type) GetParameterSourceAndName(ParameterInfo parameter, RoutePattern pattern, bool disableInferredBody)
    {
        var attributes = parameter.GetCustomAttributes();

        if (attributes.OfType<IFromRouteMetadata>().FirstOrDefault() is { } routeAttribute)
        {
            return (ParameterSource.Path, routeAttribute.Name ?? parameter.Name ?? string.Empty, false, parameter.ParameterType);
        }
        else if (attributes.OfType<IFromQueryMetadata>().FirstOrDefault() is { } queryAttribute)
        {
            return (ParameterSource.Query, queryAttribute.Name ?? parameter.Name ?? string.Empty, false, parameter.ParameterType);
        }
        else if (attributes.OfType<IFromHeaderMetadata>().FirstOrDefault() is { } headerAttribute)
        {
            return (ParameterSource.Header, headerAttribute.Name ?? parameter.Name ?? string.Empty, false, parameter.ParameterType);
        }
        else if (attributes.OfType<IFromBodyMetadata>().FirstOrDefault() is { } fromBodyAttribute)
        {
            return (ParameterSource.Body, parameter.Name ?? string.Empty, fromBodyAttribute.AllowEmpty, parameter.ParameterType);
        }
        else if (attributes.OfType<IFromFormMetadata>().FirstOrDefault() is { } fromFormAttribute)
        {
            return (ParameterSource.FormFile, fromFormAttribute.Name ?? parameter.Name ?? string.Empty, false, parameter.ParameterType);
        }
        else if (parameter.CustomAttributes.Any(a => typeof(IFromServiceMetadata).IsAssignableFrom(a.AttributeType)) ||
                 parameter.ParameterType == typeof(HttpContext) ||
                 parameter.ParameterType == typeof(HttpRequest) ||
                 parameter.ParameterType == typeof(HttpResponse) ||
                 parameter.ParameterType == typeof(ClaimsPrincipal) ||
                 parameter.ParameterType == typeof(CancellationToken) ||
                 ParameterBindingMethodCache.HasBindAsyncMethod(parameter) ||
                 _serviceProviderIsService?.IsService(parameter.ParameterType) == true)
        {
            return (ParameterSource.Services, parameter.Name ?? string.Empty, false, parameter.ParameterType);
        }
        else if (parameter.ParameterType == typeof(string) || ParameterBindingMethodCache.HasTryParseMethod(parameter.ParameterType))
        {
            // complex types will display as strings since they use custom parsing via TryParse on a string
            var displayType = !parameter.ParameterType.IsPrimitive && Nullable.GetUnderlyingType(parameter.ParameterType)?.IsPrimitive != true
                ? typeof(string) : parameter.ParameterType;
            // Path vs query cannot be determined by RequestDelegateFactory at startup currently because of the layering, but can be done here.
            if (parameter.Name is { } name && pattern.GetParameter(name) is not null)
            {
                return (ParameterSource.Path, name, false, displayType);
            }
            else
            {
                return (ParameterSource.Query, parameter.Name ?? string.Empty, false, displayType);
            }
        }
        else if (parameter.ParameterType == typeof(IFormFile) || parameter.ParameterType == typeof(IFormFileCollection))
        {
            return (ParameterSource.FormFile, parameter.Name ?? string.Empty, false, parameter.ParameterType);
        }
        else if (disableInferredBody && (
                 (parameter.ParameterType.IsArray && ParameterBindingMethodCache.HasTryParseMethod(parameter.ParameterType.GetElementType()!)) ||
                 parameter.ParameterType == typeof(string[]) ||
                 parameter.ParameterType == typeof(StringValues)))
        {
            return (ParameterSource.Query, parameter.Name ?? string.Empty, false, parameter.ParameterType);
        }
        else
        {
            return (ParameterSource.Body, parameter.Name ?? string.Empty, false, parameter.ParameterType);
        }
    }

    private static void PopulateEndpointResponses(
        EndpointApiDescription apiDescription,
        Type returnType,
        EndpointMetadataCollection endpointMetadata)
    {
        var responseType = returnType;

        if (AwaitableInfo.IsTypeAwaitable(responseType, out var awaitableInfo))
        {
            responseType = awaitableInfo.ResultType;
        }

        // Can't determine anything about IResults yet that's not from extra metadata. IResult<T> could help here.
        if (typeof(IResult).IsAssignableFrom(responseType))
        {
            responseType = typeof(void);
        }

        // We support attributes (which implement the IApiResponseMetadataProvider) interface
        // and types added via the extension methods (which implement IProducesResponseTypeMetadata).
        var responseProviderMetadata = endpointMetadata.GetOrderedMetadata<IApiResponseMetadataProvider>();
        var producesResponseMetadata = endpointMetadata.GetOrderedMetadata<IProducesResponseTypeMetadata>();
        var errorMetadata = endpointMetadata.GetMetadata<ProducesErrorResponseTypeAttribute>();
        var defaultErrorType = errorMetadata?.Type ?? typeof(void);
        var contentTypes = new MediaTypeCollection();

        var responseProviderMetadataTypes = ApiResponseTypeProvider.ReadResponseMetadata(
            responseProviderMetadata, responseType, defaultErrorType, contentTypes);
        var producesResponseMetadataTypes = ReadResponseMetadata(producesResponseMetadata, responseType);

        // We favor types added via the extension methods (which implements IProducesResponseTypeMetadata)
        // over those that are added via attributes.
        var responseMetadataTypes = producesResponseMetadataTypes.Values.Concat(responseProviderMetadataTypes);

        if (responseMetadataTypes.Any())
        {
            foreach (var apiResponseType in responseMetadataTypes)
            {
                var endpointApiResponse = new EndpointApiResponse();
                // void means no response type was specified by the metadata, so use whatever we inferred.
                // ApiResponseTypeProvider should never return ApiResponseTypes with null Type, but it doesn't hurt to check.
                if (apiResponseType.Type is null || apiResponseType.Type == typeof(void))
                {
                    endpointApiResponse.ResponseType = responseType;
                }
                else
                {
                    endpointApiResponse.ResponseType = apiResponseType.Type;
                }

                if (endpointApiResponse.ResponseType is not null)
                {
                    endpointApiResponse.Items["ModelMetadata"] = CreateModelMetadata(endpointApiResponse.ResponseType);
                }

                if (contentTypes.Count > 0)
                {
                    endpointApiResponse.ContentTypes = contentTypes;
                    AddResponseContentTypes(apiResponseType.ApiResponseFormats, contentTypes);
                }
                // Only set the default response type if it hasn't already been set via a
                // ProducesResponseTypeAttribute.
                else if (apiResponseType.ApiResponseFormats.Count == 0 && CreateDefaultApiResponseFormat(endpointApiResponse.ResponseType!) is { } defaultResponseFormat)
                {
                    apiResponseType.ApiResponseFormats.Add(defaultResponseFormat);
                }

                endpointApiResponse.Items["IsDefaultResponse"] = apiResponseType.IsDefaultResponse;
                endpointApiResponse.Items["ApiResponseFormats"] = apiResponseType.ApiResponseFormats;

                if (!apiDescription.Responses.ContainsKey(apiResponseType.StatusCode))
                {
                    apiDescription.Responses.Add(apiResponseType.StatusCode, endpointApiResponse);
                }
            }
        }
        else
        {
            // Set the default response type only when none has already been set explicitly with metadata.
            var defaultApiResponseType = CreateDefaultApiResponseType(responseType);

            if (contentTypes.Count > 0)
            {
                // If metadata provided us with response formats, use that instead of the default.
                defaultApiResponseType.ContentTypes = contentTypes;
            }

            apiDescription.Responses.Add(200, defaultApiResponseType);
        }
    }

    private static Dictionary<int, ApiResponseType> ReadResponseMetadata(
        IReadOnlyList<IProducesResponseTypeMetadata> responseMetadata,
        Type? type)
    {
        var results = new Dictionary<int, ApiResponseType>();

        foreach (var metadata in responseMetadata)
        {
            var statusCode = metadata.StatusCode;

            var apiResponseType = new ApiResponseType
            {
                Type = metadata.Type,
                StatusCode = statusCode,
            };

            if (apiResponseType.Type == typeof(void))
            {
                if (type != null && (statusCode == StatusCodes.Status200OK || statusCode == StatusCodes.Status201Created))
                {
                    // Allow setting the response type from the return type of the method if it has
                    // not been set explicitly by the method.
                    apiResponseType.Type = type;
                }
            }

            var attributeContentTypes = new MediaTypeCollection();
            if (metadata.ContentTypes != null)
            {
                foreach (var contentType in metadata.ContentTypes)
                {
                    attributeContentTypes.Add(contentType);
                }
            }
            ApiResponseTypeProvider.CalculateResponseFormatForType(apiResponseType, attributeContentTypes, responseTypeMetadataProviders: null, modelMetadataProvider: null);

            if (apiResponseType.Type != null)
            {
                results[apiResponseType.StatusCode] = apiResponseType;
            }
        }

        return results;
    }

    private static EndpointApiResponse CreateDefaultApiResponseType(Type responseType)
    {
        return new EndpointApiResponse {
            StatusCode = 200,
            ResponseType = responseType,
            Items = new Dictionary<string, object?>() {
                { "ModelMetadata",   CreateModelMetadata(responseType) },
                { "ApiResponseFormats", responseType == typeof(void) ? new List<ApiResponseFormat>() : new List<ApiResponseFormat>() { responseType == typeof(string) ? new ApiResponseFormat { MediaType = "text/plain" } :
                new ApiResponseFormat { MediaType = "application/json"} } }
            },
            ContentTypes = responseType == typeof(string)
                ? new List<string>() { "text/plain" }
                : new List<string>() { "application/json" }
        };
    }

    private static ApiResponseFormat? CreateDefaultApiResponseFormat(Type responseType)
    {
        if (responseType == typeof(void))
        {
            return null;
        }
        else if (responseType == typeof(string))
        {
            // This uses HttpResponse.WriteAsync(string) method which doesn't set a content type. It could be anything,
            // but I think "text/plain" is a reasonable assumption if nothing else is specified with metadata.
            return new ApiResponseFormat { MediaType = "text/plain" };
        }
        else
        {
            // Everything else is written using HttpResponse.WriteAsJsonAsync<TValue>(T).
            return new ApiResponseFormat { MediaType = "application/json" };
        }
    }

    private static EndpointModelMetadata CreateModelMetadata(Type type) =>
        new(ModelMetadataIdentity.ForType(type));

    private static void AddResponseContentTypes(IList<ApiResponseFormat> apiResponseFormats, IReadOnlyList<string> contentTypes)
    {
        foreach (var contentType in contentTypes)
        {
            apiResponseFormats.Add(new ApiResponseFormat
            {
                MediaType = contentType,
            });
        }
    }
}
