// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;

namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// Extension methods for annotating OpenAPI descriptions on an <see cref="Endpoint" />.
/// </summary>
public static class OpenApiEndpointConventionBuilderExtensions
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static IEndpointRouteBuilder WithOpenApi(this IEndpointRouteBuilder builder, Func<OpenApiDocument, OpenApiDocument> configureDocument = null)
    {
        var applicationServices = builder.ServiceProvider;
        var hostEnvironment = applicationServices.GetService<IHostEnvironment>();
        var serviceProviderIsService = applicationServices.GetService<IServiceProviderIsService>();
        var generator = new OpenApiGenerator(hostEnvironment, serviceProviderIsService);
        //var document = generator.GetOpenApiDocument(builder.DataSources);
        //if (configureDocument != null)
        //{
        //    document = configureDocument(document);
        //}
        return builder;
    }

    public static IServiceCollection UseOpenApi(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IConfigureOptions<OpenApiDocument>, OpenApiDocumentConfigureOptions>());
        return services;
    }

    /// <summary>
    /// Adds an OpenAPI annotation to <see cref="Endpoint.Metadata" /> associated
    /// with the current endpoint.
    /// </summary>
    /// <param name="builder">The <see cref="IEndpointConventionBuilder"/>.</param>
    /// <returns>A <see cref="IEndpointConventionBuilder"/> that can be used to further customize the endpoint.</returns>
    public static TBuilder WithOpenApi<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder
    {
        builder.Finally(builder => AddAndConfigureOperationForEndpoint(builder));
        return builder;
    }

    /// <summary>
    /// Adds an OpenAPI annotation to <see cref="Endpoint.Metadata" /> associated
    /// with the current endpoint and modifies it with the given <paramref name="configureOperation"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IEndpointConventionBuilder"/>.</param>
    /// <param name="configureOperation">An <see cref="Func{T, TResult}"/> that returns a new OpenAPI annotation given a generated operation.</param>
    /// <returns>A <see cref="IEndpointConventionBuilder"/> that can be used to further customize the endpoint.</returns>
    public static TBuilder WithOpenApi<TBuilder>(this TBuilder builder, Func<OpenApiOperation, OpenApiOperation> configureOperation)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.Finally(endpointBuilder => AddAndConfigureOperationForEndpoint(endpointBuilder, configureOperation));
        return builder;
    }

    private static void AddAndConfigureOperationForEndpoint(EndpointBuilder endpointBuilder, Func<OpenApiOperation, OpenApiOperation>? configure = null)
    {
        foreach (var item in endpointBuilder.Metadata)
        {
            if (item is OpenApiOperation existingOperation)
            {
                if (configure is not null)
                {
                    var configuredOperation = configure(existingOperation);

                    if (!ReferenceEquals(configuredOperation, existingOperation))
                    {
                        endpointBuilder.Metadata.Remove(existingOperation);

                        // The only way configureOperation could be null here is if configureOperation violated it's signature and returned null.
                        // We could throw or something, removing the previous metadata seems fine.
                        if (configuredOperation is not null)
                        {
                            endpointBuilder.Metadata.Add(configuredOperation);
                        }
                    }
                }

                return;
            }
        }

        // We cannot generate an OpenApiOperation without routeEndpointBuilder.RoutePattern.
        if (endpointBuilder is not RouteEndpointBuilder routeEndpointBuilder)
        {
            return;
        }

        var pattern = routeEndpointBuilder.RoutePattern;
        var metadata = new EndpointMetadataCollection(routeEndpointBuilder.Metadata);
        var methodInfo = metadata.OfType<MethodInfo>().SingleOrDefault();

        if (methodInfo is null)
        {
            return;
        }

        var applicationServices = routeEndpointBuilder.ApplicationServices;
        var hostEnvironment = applicationServices.GetService<IHostEnvironment>();
        var serviceProviderIsService = applicationServices.GetService<IServiceProviderIsService>();
        var generator = new OpenApiGenerator(hostEnvironment, serviceProviderIsService);
        var newOperation = generator.GetOpenApiOperation(methodInfo, metadata, pattern);

        if (newOperation is not null)
        {
            if (configure is not null)
            {
                newOperation = configure(newOperation);
            }

            if (newOperation is not null)
            {
                routeEndpointBuilder.Metadata.Add(newOperation);
            }
        }
    }
}

internal sealed class OpenApiDocumentConfigureOptions : IConfigureOptions<OpenApiDocument>
{
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IServiceProviderIsService _serviceProviderIsService;
    private readonly EndpointDataSource _endpointDataSource;
    private readonly IAuthenticationSchemeProvider? _authenticationSchemeProvider;
    private readonly OpenApiGenerator _generator;

    public OpenApiDocumentConfigureOptions(IHostEnvironment hostEnvironment, IServiceProviderIsService serviceProviderIsService, EndpointDataSource endpointDataSource, IAuthenticationSchemeProvider? authenticationSchemeProvider)
    {
        _hostEnvironment = hostEnvironment;
        _serviceProviderIsService = serviceProviderIsService;
        _endpointDataSource = endpointDataSource;
        _authenticationSchemeProvider = authenticationSchemeProvider;
        _generator = new OpenApiGenerator(hostEnvironment, serviceProviderIsService);
    }

    public void Configure(OpenApiDocument document)
    {
        document = _generator.GetOpenApiDocument(document, _endpointDataSource.Endpoints);
        var authSchemes = _authenticationSchemeProvider?.GetAllSchemesAsync().Result ?? Enumerable.Empty<AuthenticationScheme>();
        foreach (var scheme in authSchemes)
        {
            document.Components.SecuritySchemes.Add(scheme.Name, new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                In = ParameterLocation.Header,
            });
        }
    }
}
