// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Extensions;

namespace Microsoft.AspNetCore.OpenApi;

public static class SwaggerUiEndpoints
{
    public static WebApplication MapSwaggerUI(this WebApplication endpoints, IOptions<OpenApiOptions> options)
    {
        endpoints.MapGet(options.Value.JsonRoute, (OpenApiDocumentService openApiDocument) =>
        {
            return openApiDocument.GetOpenApiDocuent().SerializeAsJson(OpenApiSpecVersion.OpenApi3_0);
        }).ExcludeFromDescription();
        endpoints.MapGet("/swagger-ui", (OpenApiDocumentService docService) =>
        {
            var document = docService.GetOpenApiDocuent();
            var swagger = $$$"""
<html>
	<head>
	<link rel="stylesheet" type="text/css" href="https://unpkg.com/swagger-ui-dist@3/swagger-ui.css">
	<script src="https://unpkg.com/swagger-ui-dist@3/swagger-ui-standalone-preset.js"></script>
	<script src="https://unpkg.com/swagger-ui-dist@3/swagger-ui-bundle.js" charset="UTF-8"></script>
	</head>
	<body>
	<div id="swagger-ui"></div>
	<script>
		window.addEventListener('load', (event) => {
			const ui = SwaggerUIBundle({
			    url: "{{{options.Value.JsonRoute}}}",
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
</html>
""";

            return TypedResults.Content(swagger, "text/html");
        }).ExcludeFromDescription();
        return endpoints;
    }
}
