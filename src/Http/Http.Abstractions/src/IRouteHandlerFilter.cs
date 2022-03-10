// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;

namespace Microsoft.AspNetCore.Http;

/// <summary>
/// Provides an interface for implementing a filter targetting a route handler.
/// </summary>
public interface IRouteHandlerFilterFactory
{
    /// <summary>
    ///
    /// </summary>
    /// <param name="methodInfo"></param>
    /// <returns></returns>
    Func<RouteHandlerFilterContext, Func<RouteHandlerFilterContext, ValueTask<object?>>, ValueTask<object?>> BuildFilter(MethodInfo methodInfo);
}
