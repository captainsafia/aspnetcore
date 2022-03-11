// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Http;

/// <summary>
/// Provides an abstraction for wrapping the <see cref="HttpContext"/> and parameters
/// provided to a route handler.
/// </summary>
public class RouteHandlerFilterContext
{
    /// <summary>
    /// Creates a new instance of the <see cref="RouteHandlerFilterContext"/> for a given request.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> associated with the current request.</param>
    /// <param name="serviceProvider">The <see cref="IServiceProvider"/> associated with the current request.</param>
    /// <param name="parameters">A list of parameters provided in the current request.</param>
    public RouteHandlerFilterContext(HttpContext httpContext, IServiceProvider serviceProvider, params object[] parameters)
    {
        HttpContext = httpContext;
        Parameters = parameters;
        ServiceProvider = serviceProvider;
    }

    /// <summary>
    /// The <see cref="HttpContext"/> associated with the current request being processed by the filter.
    /// </summary>
    public HttpContext HttpContext { get; }

    /// <summary>
    /// A list of parameters provided in the current request to the filter.
    /// <remarks>
    /// This list is not read-only to permit modifying of existing parameters by filters.
    /// </remarks>
    /// </summary>
    public IList<object?> Parameters { get; }

    /// <summary>
    /// The <see cref="IServiceProvider"/> associated with the current request.
    /// </summary>
    public IServiceProvider ServiceProvider { get; }
}
