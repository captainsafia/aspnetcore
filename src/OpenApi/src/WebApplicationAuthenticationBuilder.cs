// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Reflection.Metadata;
using System.Linq;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Interfaces;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Microsoft.AspNetCore.Authentication;

public class WebApplicationAuthenticationBuilder : AuthenticationBuilder
{
    public bool IsAuthenticationConfigured { get; private set; }

    public WebApplicationAuthenticationBuilder(IServiceCollection services) : base(services) { }

    public override AuthenticationBuilder AddPolicyScheme(string authenticationScheme, string? displayName, Action<PolicySchemeOptions> configureOptions)
    {
        RegisterServices(authenticationScheme);
        return base.AddPolicyScheme(authenticationScheme, displayName, configureOptions);
    }

    public override AuthenticationBuilder AddRemoteScheme<TOptions, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(string authenticationScheme, string? displayName, Action<TOptions>? configureOptions)
    {
        RegisterServices(authenticationScheme);
        Services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<TOptions>, PopulateOpenApiSchemes<TOptions>>());
        return base.AddRemoteScheme<TOptions, THandler>(authenticationScheme, displayName, configureOptions);
    }

    public override AuthenticationBuilder AddScheme<TOptions, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(string authenticationScheme, string? displayName, Action<TOptions>? configureOptions)
    {
        RegisterServices(authenticationScheme);
        Services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<TOptions>, PopulateOpenApiSchemes<TOptions>>());
        return base.AddScheme<TOptions, THandler>(authenticationScheme, displayName, configureOptions);
    }

    public override AuthenticationBuilder AddScheme<TOptions, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(string authenticationScheme, Action<TOptions>? configureOptions)
    {
        RegisterServices(authenticationScheme);
        Services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<TOptions>, PopulateOpenApiSchemes<TOptions>>());
        return base.AddScheme<TOptions, THandler>(authenticationScheme, configureOptions);
    }

    private sealed class PopulateOpenApiSchemes<TOptions> : IPostConfigureOptions<TOptions> where TOptions : AuthenticationSchemeOptions
    {
        private readonly OpenApiDocumentService _docService;

        public PopulateOpenApiSchemes(OpenApiDocumentService docService)
        {
            _docService = docService;
        }

        public void PostConfigure(string? name, TOptions options)
        {
            _docService.Document.Components ??= new();
            _docService.Document.Components.SecuritySchemes ??= new Dictionary<string, OpenApiSecurityScheme>();

            if (options is CookieOptions cookieOptions)
            {
                _docService.Document.Components.SecuritySchemes.TryAdd(name, new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.ApiKey,
                    In = ParameterLocation.Cookie,
                    Name = name,
                });
            }
            if (options is OAuthOptions oAuthOptions)
            {
                _docService.Document.Components.SecuritySchemes.TryAdd(name, new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.OAuth2,
                    Name = "Authorization",
                    In = ParameterLocation.Cookie,
                    Scheme = name,
                    Extensions = new Dictionary<string, IOpenApiExtension>
                    {
                        {"x-tokenName", new OpenApiString("id_token")}
                    },
                    Flows = new()
                    {
                        AuthorizationCode = new()
                        {
                            AuthorizationUrl = new Uri(oAuthOptions.AuthorizationEndpoint),
                            TokenUrl = new Uri(oAuthOptions.TokenEndpoint),
                            Scopes = oAuthOptions.Scope.ToDictionary(s => s, s => s)
                        }
                    }

                });
            }
        }
    }

    private void RegisterServices(string authenticationScheme)
    {
        if (!IsAuthenticationConfigured)
        {
            IsAuthenticationConfigured = true;
            Services.AddAuthentication(authenticationScheme);
            Services.AddAuthorization();
        }
    }
}
