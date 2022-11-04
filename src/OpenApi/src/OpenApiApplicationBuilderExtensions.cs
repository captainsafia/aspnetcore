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
    public static WebApplicationBuilder UseOpenApi(this WebApplicationBuilder builder)
    {
        builder.Services.TryAddSingleton<OpenApiDocumentService, OpenApiDocumentService>();
        return builder;
    }

    public static WebApplication UseOpenApi(this WebApplication app)
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
}


