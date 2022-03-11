// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.AspNetCore.Builder;

namespace Microsoft.AspNetCore.Http;

/// <summary>
/// Extension methods for adding <see cref="IRouteHandlerFilterFactory"/> to a route handler.
/// </summary>
public static class RouteHandlerFilterExtensions
{
    /// <summary>
    /// Registers a filter onto the route handler.
    /// </summary>
    /// <param name="builder">The <see cref="RouteHandlerBuilder"/>.</param>
    /// <param name="filter">The <see cref="IRouteHandlerFilterFactory"/> to register.</param>
    /// <returns>A <see cref="RouteHandlerBuilder"/> that can be used to further customize the route handler.</returns>
    public static RouteHandlerBuilder AddFilter(this RouteHandlerBuilder builder, IRouteHandlerFilterFactory filter)
    {
        builder.RouteHandlerFilters.Add(filter);
        return builder;
    }

    /// <summary>
    /// Registers a filter of type <typeparamref name="TFilterFactoryType"/> onto the route handler.
    /// </summary>
    /// <typeparam name="TFilterFactoryType">The type of the <see cref="IRouteHandlerFilterFactory"/> to register.</typeparam>
    /// <param name="builder">The <see cref="RouteHandlerBuilder"/>.</param>
    /// <returns>A <see cref="RouteHandlerBuilder"/> that can be used to further customize the route handler.</returns>
    public static RouteHandlerBuilder AddFilter<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TFilterFactoryType>(this RouteHandlerBuilder builder) where TFilterFactoryType : IRouteHandlerFilterFactory, new()
    {
        builder.RouteHandlerFilters.Add(new TFilterFactoryType());
        return builder;
    }

    /// <summary>
    /// Registers a filter given a delegate onto the route handler.
    /// </summary>
    /// <param name="builder">The <see cref="RouteHandlerBuilder"/>.</param>
    /// <param name="routeHandlerFilter">A <see cref="Delegate"/> representing the core logic of the filter.</param>
    /// <returns>A <see cref="RouteHandlerBuilder"/> that can be used to further customize the route handler.</returns>
    public static RouteHandlerBuilder AddFilter(this RouteHandlerBuilder builder, Func<RouteHandlerFilterContext, Func<RouteHandlerFilterContext, ValueTask<object?>>, ValueTask<object?>> routeHandlerFilter)
    {
        builder.RouteHandlerFilters.Add(new DelegateRouteHandlerFilterFactory((MethodInfo methodInfo) => routeHandlerFilter));
        return builder;
    }

    /// <summary>
    /// Registers a filter given a delegate representing the filter factory onto the route handler.
    /// </summary>
    /// <param name="builder">The <see cref="RouteHandlerBuilder"/>.</param>
    /// <param name="routeHandlerFilterFactory">A <see cref="Delegate"/> for constructing the filter from a <see cref="MethodInfo"/>.</param>
    /// <returns>A <see cref="RouteHandlerBuilder"/> that can be used to further customize the route handler.</returns>
    public static RouteHandlerBuilder AddFilter(this RouteHandlerBuilder builder, Func<MethodInfo, Func<RouteHandlerFilterContext, Func<RouteHandlerFilterContext, ValueTask<object?>>, ValueTask<object?>>> routeHandlerFilterFactory)
    {
        builder.RouteHandlerFilters.Add(new DelegateRouteHandlerFilterFactory(routeHandlerFilterFactory));
        return builder;
    }
}
