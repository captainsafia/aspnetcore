// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;

namespace Microsoft.AspNetCore.Http;

internal sealed class DelegateRouteHandlerFilter : IRouteHandlerFilterFactory
{
    private readonly Func<RouteHandlerFilterContext, Func<RouteHandlerFilterContext, ValueTask<object?>>, ValueTask<object?>> _routeHandlerFilter;

    internal DelegateRouteHandlerFilter(Func<RouteHandlerFilterContext, Func<RouteHandlerFilterContext, ValueTask<object?>>, ValueTask<object?>> routeHandlerFilter)
    {
        _routeHandlerFilter = routeHandlerFilter;
    }

    public Func<RouteHandlerFilterContext, Func<RouteHandlerFilterContext, ValueTask<object?>>, ValueTask<object?>> BuildFilter(MethodInfo methodInfo)
    {
        return (context, next) => _routeHandlerFilter(context, next);
    }
}
