using System;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Models;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.OpenApi.Extensions;
using Microsoft.AspNetCore.Http;

namespace Microsoft.AspNetCore.OpenApi;

public static class OpenApiApplicationBuilderExtensions
{

    public static IServiceCollection AddOpenApiService(this IServiceCollection service, Action<OpenApiDocument>? customizeGlobalDoc = null)
    {
        service.TryAddSingleton<OpenApiDocumentService>(s =>
        new OpenApiDocumentService(s.GetRequiredService<IHostEnvironment>(),
        s.GetRequiredService<IServiceProviderIsService>(), s.GetRequiredService<EndpointDataSource>(), s.GetService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>(),
        customizeGlobalDoc));

        return service;
    }



    /*
    public static WebApplication MapSwaggerJson(this WebApplication app)
    {
        app.MapGet("/swagger.json", (OpenApiDocumentService openApiDocument) =>
        {
            return openApiDocument.GetOpenApiDocuent().SerializeAsJson(OpenApiSpecVersion.OpenApi3_0);
        }).ExcludeFromDescription();
        return app;
    }

    
    public static WebApplication MapSwaggerUI(this WebApplication app)
    {
        app.MapGet("/swagger.json", (OpenApiDocumentService openApiDocument) =>
        {
            return openApiDocument.GetOpenApiDocuent().SerializeAsJson(OpenApiSpecVersion.OpenApi3_0);
        }).ExcludeFromDescription();
        app.MapGet("/swagger-ui", (OpenApiDocumentService docService) =>
        {
            var document = docService.GetOpenApiDocuent();
            var swagger = @"<html>
	<head>
	<link rel=""stylesheet"" type=""text/css"" href=""https://unpkg.com/swagger-ui-dist@3/swagger-ui.css"">
	<script src=""https://unpkg.com/swagger-ui-dist@3/swagger-ui-standalone-preset.js""></script>
	<script src=""https://unpkg.com/swagger-ui-dist@3/swagger-ui-bundle.js"" charset=""UTF-8""></script>
	</head>
	<body>
	<div id=""swagger-ui""></div>
	<script>
		window.addEventListener('load', (event) => {
			const ui = SwaggerUIBundle({
			    url: ""/swagger.json"",
			    dom_id: '#swagger-ui',
			    presets: [
			      SwaggerUIBundle.presets.apis,
			      SwaggerUIBundle.SwaggerUIStandalonePreset
			    ],
				plugins: [
                	SwaggerUIBundle.plugins.DownloadUrl
            	],
				deepLinking: true
			  })
			window.ui = ui
		});
	</script>
	</body>
</html>";
            return TypedResults.Content(swagger, "text/html");
        }).ExcludeFromDescription();
        return app;
    }
    */
}


