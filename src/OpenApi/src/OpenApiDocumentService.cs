// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Interfaces;
using Microsoft.OpenApi.Models;

namespace Microsoft.AspNetCore.OpenApi;

public class OpenApiDocumentService : IOpenApiDocumentService
{
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IServiceProviderIsService _serviceProviderIsService;
    private readonly EndpointDataSource _endpointDataSource;
    private readonly OpenApiGenerator _generator;
    private readonly Action<OpenApiDocument> _customizeDoc;

    // This is mutable so that might be a problem -- create a builder?
    public OpenApiDocument Document { get; private set; } = new OpenApiDocument();
    public JsonSchemaGenerator SchemaGenerator { get; private set; }

    public OpenApiDocumentService(IHostEnvironment hostEnvironment, IServiceProviderIsService serviceProviderIsService, EndpointDataSource endpointDataSource, IOptions<JsonOptions> jsonOptions,
        Action<OpenApiDocument> configureDoc)
    {
        _hostEnvironment = hostEnvironment;
        _serviceProviderIsService = serviceProviderIsService;
        _endpointDataSource = endpointDataSource;
        _generator = new OpenApiGenerator(hostEnvironment, serviceProviderIsService);
        SchemaGenerator = new JsonSchemaGenerator(Document, jsonOptions);
        _customizeDoc = configureDoc;
    }

    public OpenApiDocument GetOpenApiDocuent()
    {
        Document.Info ??= new();
        Document.Info.Title = System.AppDomain.CurrentDomain.FriendlyName;
        _generator.GetOpenApiDocument(Document, SchemaGenerator, _endpointDataSource.Endpoints);
        /*
        var authSchemes = _authenticationSchemeProvider?.GetAllSchemesAsync().Result ?? Enumerable.Empty<AuthenticationScheme>();
        foreach (var scheme in authSchemes)
        {
            Document.Components ??= new OpenApiComponents();
            var foo = authenticationOptionsResolver(scheme.Name);
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
            if (scheme.Name == "Google")
            {
                Document.Components.SecuritySchemes ??= new Dictionary<string, OpenApiSecurityScheme>();

                Document.Components.SecuritySchemes.TryAdd(scheme.Name, new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.OAuth2,
                    Name = "Authorization",
                    In = ParameterLocation.Cookie,
                    Scheme = "Google",
                    Extensions = new Dictionary<string, IOpenApiExtension>
                    {
                        {"x-tokenName", new OpenApiString("id_token")}
                    },
                    Flows = new()
                    {
                        AuthorizationCode = new()
                        {
                            AuthorizationUrl = new Uri("https://accounts.google.com/o/oauth2/auth"),
                            TokenUrl = new Uri("https://oauth2.googleapis.com/token"),
                            Scopes = new Dictionary<string, string>{
                                { "openid", "Open ID" },
                                { "profile", "Profile scope" },
                                {"email", "Email address"}
                            }
                        }
                    }

                });
            }
        }
        */
        if (_customizeDoc is not null)
            _customizeDoc(Document);
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
