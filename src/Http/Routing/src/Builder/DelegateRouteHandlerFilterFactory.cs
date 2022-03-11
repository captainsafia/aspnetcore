// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;

namespace Microsoft.AspNetCore.Http;

internal sealed class DelegateRouteHandlerFilterFactory : IRouteHandlerFilterFactory
{
    private readonly Func<MethodInfo, Func<RouteHandlerFilterContext, Func<RouteHandlerFilterContext, ValueTask<object?>>, ValueTask<object?>>> _routeHandlerFilter;

    internal DelegateRouteHandlerFilterFactory(Func<MethodInfo, Func<RouteHandlerFilterContext, Func<RouteHandlerFilterContext, ValueTask<object?>>, ValueTask<object?>>> routeHandlerFilter)
    {
        _routeHandlerFilter = routeHandlerFilter;
    }

    public Func<RouteHandlerFilterContext, Func<RouteHandlerFilterContext, ValueTask<object?>>, ValueTask<object?>> BuildFilter(MethodInfo methodInfo)
    {
        return _routeHandlerFilter(methodInfo);
    }
}
