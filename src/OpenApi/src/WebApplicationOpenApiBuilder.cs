// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi.Extensions;
using System.Diagnostics;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;


public class OpenApiOptions
{
    public string JsonRoute { get; set; } = "swagger.json";
}

public class WebApplicationOpenApiBuilder
{
    public bool IsOpenApiEnabled { get; private set; }
    private bool _optionsAdded = false;
    private readonly IServiceCollection _services;
    private Action<OpenApiDocument>? _modifyDocument = null;

    public OpenApiOptions Options { get; set; } = new();

    public WebApplicationOpenApiBuilder(IServiceCollection services)
    {
        _services = services;
    }

    public WebApplicationOpenApiBuilder Enable()
    {
        IsOpenApiEnabled = true;
        return this;
    }

    public WebApplicationOpenApiBuilder ConfigureOptions(Action<OpenApiOptions> modifyOptions)
    {
        _optionsAdded = true;
        _services.Configure<OpenApiOptions>(modifyOptions);
        return this;
    }

    public WebApplicationOpenApiBuilder WithDocument(Action<OpenApiDocument> modifyDocument)
    {
        _modifyDocument = modifyDocument;
        return this;
    }

    internal WebApplicationOpenApiBuilder Build()
    {
        if (IsOpenApiEnabled)
        {
            _services.AddOpenApiService(_modifyDocument);
            if (!_optionsAdded)
            {
                _services.AddOptions<OpenApiOptions>();
            }
        }
        return this;
    }
}

/// <summary>
/// A builder for web applications and services.
/// </summary>
public sealed class OpenApiWebApplicationBuilder
{
    private const string EndpointRouteBuilderKey = "__EndpointRouteBuilder";
    private const string AuthenticationMiddlewareSetKey = "__AuthenticationMiddlewareSet";
    private const string AuthorizationMiddlewareSetKey = "__AuthorizationMiddlewareSet";
    private const string UseRoutingKey = "__UseRouting";

    private readonly HostApplicationBuilder _hostApplicationBuilder;
    private readonly ServiceDescriptor _genericWebHostServiceDescriptor;
    private readonly WebApplicationOpenApiBuilder _openApiBuilder;
    private readonly WebApplicationAuthenticationBuilder _authBuilder;

    private WebApplication? _builtApplication;

    public OpenApiWebApplicationBuilder(WebApplicationOptions options, Action<IHostBuilder>? configureDefaults = null)
    {
        var configuration = new ConfigurationManager();

        configuration.AddEnvironmentVariables(prefix: "ASPNETCORE_");

        _hostApplicationBuilder = new HostApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = options.Args,
            ApplicationName = options.ApplicationName,
            EnvironmentName = options.EnvironmentName,
            ContentRootPath = options.ContentRootPath,
            Configuration = configuration,
        });

        // Set WebRootPath if necessary
        if (options.WebRootPath is not null)
        {
            Configuration.AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string?>(WebHostDefaults.WebRootKey, options.WebRootPath),
            });
        }

        // Run methods to configure web host defaults early to populate services
        var bootstrapHostBuilder = new BootstrapHostBuilder(_hostApplicationBuilder);

        // This is for testing purposes
        configureDefaults?.Invoke(bootstrapHostBuilder);

        bootstrapHostBuilder.ConfigureWebHostDefaults(webHostBuilder =>
        {
            // Runs inline.
            webHostBuilder.Configure(ConfigureApplication);

            webHostBuilder.UseSetting(WebHostDefaults.ApplicationKey, _hostApplicationBuilder.Environment.ApplicationName ?? "");
            webHostBuilder.UseSetting(WebHostDefaults.PreventHostingStartupKey, Configuration[WebHostDefaults.PreventHostingStartupKey]);
            webHostBuilder.UseSetting(WebHostDefaults.HostingStartupAssembliesKey, Configuration[WebHostDefaults.HostingStartupAssembliesKey]);
            webHostBuilder.UseSetting(WebHostDefaults.HostingStartupExcludeAssembliesKey, Configuration[WebHostDefaults.HostingStartupExcludeAssembliesKey]);
        },
        options =>
        {
            // We've already applied "ASPNETCORE_" environment variables to hosting config
            options.SuppressEnvironmentConfiguration = true;
        });

        // This applies the config from ConfigureWebHostDefaults
        // Grab the GenericWebHostService ServiceDescriptor so we can append it after any user-added IHostedServices during Build();
        _genericWebHostServiceDescriptor = bootstrapHostBuilder.RunDefaultCallbacks();

        // Grab the WebHostBuilderContext from the property bag to use in the ConfigureWebHostBuilder. Then
        // grab the IWebHostEnvironment from the webHostContext. This also matches the instance in the IServiceCollection.
        var webHostContext = (WebHostBuilderContext)bootstrapHostBuilder.Properties[typeof(WebHostBuilderContext)];
        Environment = webHostContext.HostingEnvironment;

        Host = new ConfigureHostBuilder(bootstrapHostBuilder.Context, Configuration, Services);
        WebHost = new ConfigureWebHostBuilder(webHostContext, Configuration, Services);
        _openApiBuilder = new WebApplicationOpenApiBuilder(Services);
        _authBuilder = new WebApplicationAuthenticationBuilder(Services);
    }

    /// <summary>
    /// Provides information about the web hosting environment an application is running.
    /// </summary>
    public IWebHostEnvironment Environment { get; }

    /// <summary>
    /// A collection of services for the application to compose. This is useful for adding user provided or framework provided services.
    /// </summary>
    public IServiceCollection Services => _hostApplicationBuilder.Services;

    /// <summary>
    /// A collection of configuration providers for the application to compose. This is useful for adding new configuration sources and providers.
    /// </summary>
    public ConfigurationManager Configuration => _hostApplicationBuilder.Configuration;

    /// <summary>
    /// A collection of logging providers for the application to compose. This is useful for adding new logging providers.
    /// </summary>
    public ILoggingBuilder Logging => _hostApplicationBuilder.Logging;

    /// <summary>
    /// An <see cref="IWebHostBuilder"/> for configuring server specific properties, but not building.
    /// To build after configuration, call <see cref="Build"/>.
    /// </summary>
    public ConfigureWebHostBuilder WebHost { get; }

    /// <summary>
    /// An <see cref="IHostBuilder"/> for configuring host specific properties, but not building.
    /// To build after configuration, call <see cref="Build"/>.
    /// </summary>
    public ConfigureHostBuilder Host { get; }

    public WebApplicationOpenApiBuilder OpenApi => _openApiBuilder;

    public WebApplicationAuthenticationBuilder Authentication => _authBuilder;

    /// <summary>
    /// Builds the <see cref="WebApplication"/>.
    /// </summary>
    /// <returns>A configured <see cref="WebApplication"/>.</returns>
    public WebApplication Build()
    {
        // ConfigureContainer callbacks run after ConfigureServices callbacks including the one that adds GenericWebHostService by default.
        // One nice side effect is this gives a way to configure an IHostedService that starts after the server and stops beforehand.
        _openApiBuilder.Build();
        _hostApplicationBuilder.Services.Add(_genericWebHostServiceDescriptor);
        Host.ApplyServiceProviderFactory(_hostApplicationBuilder);
        _builtApplication = new WebApplication(_hostApplicationBuilder.Build());
        return _builtApplication;
    }

    private void ConfigureApplication(WebHostBuilderContext context, IApplicationBuilder app)
    {
        Debug.Assert(_builtApplication is not null);

        // UseRouting called before WebApplication such as in a StartupFilter
        // lets remove the property and reset it at the end so we don't mess with the routes in the filter
        if (app.Properties.TryGetValue(EndpointRouteBuilderKey, out var priorRouteBuilder))
        {
            app.Properties.Remove(EndpointRouteBuilderKey);
        }

        if (context.HostingEnvironment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        // Wrap the entire destination pipeline in UseRouting() and UseEndpoints(), essentially:
        // destination.UseRouting()
        // destination.Run(source)
        // destination.UseEndpoints()

        // Set the route builder so that UseRouting will use the WebApplication as the IEndpointRouteBuilder for route matching
        app.Properties.Add(WebApplication.GlobalEndpointRouteBuilderKey, _builtApplication);

        // Only call UseRouting() if there are endpoints configured and UseRouting() wasn't called on the global route builder already
        if (_builtApplication.DataSources.Count > 0)
        {
            // If this is set, someone called UseRouting() when a global route builder was already set
            if (!_builtApplication.Properties.TryGetValue(EndpointRouteBuilderKey, out var localRouteBuilder))
            {
                app.UseRouting();
                // Middleware the needs to re-route will use this property to call UseRouting()
                _builtApplication.Properties[UseRoutingKey] = app.Properties[UseRoutingKey];
            }
            else
            {
                // UseEndpoints will be looking for the RouteBuilder so make sure it's set
                app.Properties[EndpointRouteBuilderKey] = localRouteBuilder;
            }
        }

        // Process authorization and authentication middlewares independently to avoid
        // registering middlewares for services that do not exist
        var serviceProviderIsService = _builtApplication.Services.GetService<IServiceProviderIsService>();
        if (serviceProviderIsService?.IsService(typeof(IAuthenticationSchemeProvider)) is true)
        {
            // Don't add more than one instance of the middleware
            if (!_builtApplication.Properties.ContainsKey(AuthenticationMiddlewareSetKey))
            {
                // The Use invocations will set the property on the outer pipeline,
                // but we want to set it on the inner pipeline as well.
                _builtApplication.Properties[AuthenticationMiddlewareSetKey] = true;
                app.UseAuthentication();
            }
        }

        if (serviceProviderIsService?.IsService(typeof(IAuthorizationHandlerProvider)) is true)
        {
            if (!_builtApplication.Properties.ContainsKey(AuthorizationMiddlewareSetKey))
            {
                _builtApplication.Properties[AuthorizationMiddlewareSetKey] = true;
                app.UseAuthorization();
            }
        }

        if (OpenApi.IsOpenApiEnabled)
        {
            _builtApplication.MapSwaggerUI(_builtApplication.Services.GetService<IOptions<OpenApiOptions>>());
        }

        // Wire the source pipeline to run in the destination pipeline
        app.Use(next =>
        {
            _builtApplication.Run(next);
            return _builtApplication.BuildRequestDelegate();
        });

        if (_builtApplication.DataSources.Count > 0)
        {
            // We don't know if user code called UseEndpoints(), so we will call it just in case, UseEndpoints() will ignore duplicate DataSources
            app.UseEndpoints(_ => { });
        }

        // Copy the properties to the destination app builder
        foreach (var item in _builtApplication.Properties)
        {
            app.Properties[item.Key] = item.Value;
        }

        // Remove the route builder to clean up the properties, we're done adding routes to the pipeline
        app.Properties.Remove(WebApplication.GlobalEndpointRouteBuilderKey);

        // reset route builder if it existed, this is needed for StartupFilters
        if (priorRouteBuilder is not null)
        {
            app.Properties[EndpointRouteBuilderKey] = priorRouteBuilder;
        }
    }
}

