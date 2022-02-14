// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Http;

public class EndpointFilterContext
{
    public EndpointFilterContext(HttpContext httpContext, params object[] parameters)
    {
        HttpContext = httpContext;
        Parameters = parameters;
    }
    public HttpContext HttpContext { get; }
    public IList<object?> Parameters { get; } // Not read-only to premit modifying of parameters by filters
}
