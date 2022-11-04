// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;

namespace Microsoft.AspNetCore.OpenApi;

public class OpenApiDocumentService
{
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IServiceProviderIsService _serviceProviderIsService;
    private readonly EndpointDataSource _endpointDataSource;
    private readonly IAuthenticationSchemeProvider? _authenticationSchemeProvider;
    private readonly OpenApiGenerator _generator;

    // This is mutable so that might be a problem -- create a builder?
    public OpenApiDocument Document { get; private set; } = new OpenApiDocument();
    public JsonSchemaGenerator SchemaGenerator { get; private set; }

    public OpenApiDocumentService(IHostEnvironment hostEnvironment, IServiceProviderIsService serviceProviderIsService, EndpointDataSource endpointDataSource, IAuthenticationSchemeProvider? authenticationSchemeProvider, IOptions<JsonOptions> jsonOptions)
    {
        _hostEnvironment = hostEnvironment;
        _serviceProviderIsService = serviceProviderIsService;
        _endpointDataSource = endpointDataSource;
        _authenticationSchemeProvider = authenticationSchemeProvider;
        _generator = new OpenApiGenerator(hostEnvironment, serviceProviderIsService);
        SchemaGenerator = new JsonSchemaGenerator(Document, jsonOptions);
    }

    public OpenApiDocument GetOpenApiDocuent()
    {

        _generator.GetOpenApiDocument(Document, SchemaGenerator, _endpointDataSource.Endpoints);
        var authSchemes = _authenticationSchemeProvider?.GetAllSchemesAsync().Result ?? Enumerable.Empty<AuthenticationScheme>();
        foreach (var scheme in authSchemes)
        {
            Document.Components ??= new OpenApiComponents();
            if (scheme.Name == "Cookies")
            {
                Document.Components.SecuritySchemes ??= new Dictionary<string, OpenApiSecurityScheme>();
                Document.Components.SecuritySchemes.TryAdd(scheme.Name, new OpenApiSecurityScheme
                {
                        Type = SecuritySchemeType.ApiKey,
                        In = ParameterLocation.Cookie,
                        Name = scheme.Name,
                });
            }
            if (scheme.HandlerType.IsAssignableFrom(typeof(OAuthHandler<OAuthOptions>)))
            {
                Document.Components.SecuritySchemes ??= new Dictionary<string, OpenApiSecurityScheme>();
                Document.Components.SecuritySchemes.TryAdd(scheme.Name, new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.OAuth2,

                });
            }
        }
        return Document;
    }

    public void PutOperation(string pattern, OperationType operationType, OpenApiOperation operation)
    {
        Document.Paths ??= new();
        if (!Document.Paths.ContainsKey(pattern))
        {
            var pathItem = new OpenApiPathItem();
            pathItem.AddOperation(operationType, operation);
            Document.Paths.Add(pattern, pathItem);
        }
        else
        {
            if (!Document.Paths[pattern].Operations.ContainsKey(operationType))
            {
                Document.Paths[pattern]
                .AddOperation(operationType, operation);
            }
        }
    }

}
