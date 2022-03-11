// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;

namespace Microsoft.AspNetCore.Http;

/// <summary>
/// Provides an interface for implementing a filter targeting a route handler.
/// </summary>
public interface IRouteHandlerFilterFactory
{
    /// <summary>
    /// When invoked, will return a filter that will target the handler associated with the provided <paramref name="methodInfo"/>. 
    /// </summary>
    /// <param name="methodInfo">The <see cref="MethodInfo"/> of the handler this filter will be applied to.</param>
    /// <returns>
    /// A <see cref="Func{T1, T2, TResult}"/> representing the filter to be invoked in the pipeline.
    /// </returns>
    Func<RouteHandlerFilterContext, Func<RouteHandlerFilterContext, ValueTask<object?>>, ValueTask<object?>> BuildFilter(MethodInfo methodInfo);
}
