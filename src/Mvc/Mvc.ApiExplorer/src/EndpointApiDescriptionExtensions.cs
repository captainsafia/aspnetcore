// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;

namespace Microsoft.AspNetCore.Mvc.ApiExplorer;

internal static class EndpointApiDescriptionExtensions
{
    internal static ApiDescription TransformToApiDescription(this EndpointApiDescription original)
    {
        var apiDescription = new ApiDescription
        {
            HttpMethod = original.HttpMethod,
            GroupName = original.GroupName,
            RelativePath = original.Items.GetValueOrDefault("RelativePath")?.ToString(),
            ActionDescriptor = new Abstractions.ActionDescriptor
            {
                DisplayName = original.Items.GetValueOrDefault("DisplayName")?.ToString(),
                RouteValues =
                {
                    ["controller"] = original.Items.GetValueOrDefault("controllerName")?.ToString(),
                },
                EndpointMetadata = new List<object>(original.Metadata)
            }
        };
        var formats = (List<ApiRequestFormat>?)original.Items.GetValueOrDefault("SupportedRequestFormats") ?? new List<ApiRequestFormat>();
        foreach (var item in formats)
        {
            apiDescription.SupportedRequestFormats.Add(item);
        }
        AddParameterDescriptions(original, apiDescription);
        AddSupportedResponseTypes(original, apiDescription);
        return apiDescription;
    }

    private static void AddParameterDescriptions(EndpointApiDescription original, ApiDescription apiDescription)
    {
        foreach (var (parameterName, parameter) in original.Parameters)
        {
            var hasRouteConstraints = parameter.Items.GetValueOrDefault("Constraints") is not null;
            apiDescription.ParameterDescriptions.Add(new ApiParameterDescription
            {
                Name = parameter.Name,
                ModelMetadata = (ModelBinding.ModelMetadata)parameter.Items.GetValueOrDefault("ModelMetadata")!,
                Source = ToBindingSource(parameter.Source),
                DefaultValue = parameter.Items.GetValueOrDefault("DefaultValue"),
                Type = parameter.ParameterType,
                IsRequired = (bool?)parameter.Items.GetValueOrDefault("IsRequired") ?? false,
                ParameterDescriptor = new EndpointParameterDescriptor
                {
                    Name = parameter.Name,
                    ParameterInfo = parameter.ParameterInfo,
                    ParameterType = parameter.ParameterType,
                },
                RouteInfo =  hasRouteConstraints ? new ApiParameterRouteInfo
                {
                    Constraints = (List<IRouteConstraint>)parameter.Items.GetValueOrDefault("Constraints")!,
                    DefaultValue = parameter.Items.GetValueOrDefault("DefaultValue"),
                    IsOptional = !((bool?)parameter.Items.GetValueOrDefault("IsRequired") ?? false)
                } : null
            });
        }

        static BindingSource ToBindingSource(ParameterSource source)
        {
            return source switch
            {
                ParameterSource.Query => BindingSource.Query,
                ParameterSource.Body => BindingSource.Body,
                ParameterSource.Header => BindingSource.Header,
                ParameterSource.Path => BindingSource.Path,
                ParameterSource.FormFile => BindingSource.FormFile,
                _ => BindingSource.Query
            };
        }
    }

    private static void AddSupportedResponseTypes(EndpointApiDescription original, ApiDescription apiDescription)
    {
        foreach (var (statusCode, response) in original.Responses)
        {
            apiDescription.SupportedResponseTypes.Add(new ApiResponseType
            {
                ModelMetadata = (ModelMetadata?)response.Items.GetValueOrDefault("ModelMetadata"),
                Type = response.ResponseType,
                StatusCode = statusCode,
                ApiResponseFormats = (List<ApiResponseFormat>?)response.Items.GetValueOrDefault("ApiResponseFormats") ?? new(),
                IsDefaultResponse = (bool)(response.Items.GetValueOrDefault("IsDefaultResponse") ?? false)
            });
        }
    }
}
