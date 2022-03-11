// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.Http;

/// <summary>
/// Extension methods for adding <see cref="IRouteHandlerFilter"/> to a route handler.
/// </summary>
public static class RouteHandlerFilterExtensions
{
    /// <summary>
    /// Registers a filter onto the route handler.
    /// </summary>
    /// <param name="builder">The <see cref="RouteHandlerBuilder"/>.</param>
    /// <param name="filter">The <see cref="IRouteHandlerFilter"/> to register.</param>
    /// <returns>A <see cref="RouteHandlerBuilder"/> that can be used to further customize the route handler.</returns>
    public static RouteHandlerBuilder AddFilter(this RouteHandlerBuilder builder, IRouteHandlerFilter filter)
    {
        builder.RouteHandlerFilterFactories.Add((methodInfo, next) => (context) => filter.InvokeAsync(context, next));
        return builder;
    }

    /// <summary>
    /// Registers a filter of type <typeparamref name="TFilterType"/> onto the route handler.
    /// </summary>
    /// <typeparam name="TFilterType">The type of the <see cref="IRouteHandlerFilter"/> to register.</typeparam>
    /// <param name="builder">The <see cref="RouteHandlerBuilder"/>.</param>
    /// <returns>A <see cref="RouteHandlerBuilder"/> that can be used to further customize the route handler.</returns>
    public static RouteHandlerBuilder AddFilter<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TFilterType>(this RouteHandlerBuilder builder) where TFilterType : IRouteHandlerFilter
    {
        builder.RouteHandlerFilterFactories.Add((methodInfo, next) => async (context) =>
        {
            var type = typeof(TFilterType);
            var filterFactory = ActivatorUtilities.CreateFactory(type, Array.Empty<Type>());
            IRouteHandlerFilter filter = (IRouteHandlerFilter)filterFactory.Invoke(context.ServiceProvider, Array.Empty<object>());
            return await filter.InvokeAsync(context, next);
        });
        return builder;
    }

    /// <summary>
    /// Registers a filter given a delegate onto the route handler.
    /// </summary>
    /// <param name="builder">The <see cref="RouteHandlerBuilder"/>.</param>
    /// <param name="routeHandlerFilter">A <see cref="Delegate"/> representing the core logic of the filter.</param>
    /// <returns>A <see cref="RouteHandlerBuilder"/> that can be used to further customize the route handler.</returns>
    public static RouteHandlerBuilder AddFilter(this RouteHandlerBuilder builder, Func<RouteHandlerFilterContext, RouteHandlerFilterDelegate, ValueTask<object?>> routeHandlerFilter)
    {
        builder.RouteHandlerFilterFactories.Add((methodInfo, next) => (context) => routeHandlerFilter(context, next));
        return builder;
    }

    /// <summary>
    /// Register a filter given a delegate representing the filter factory.
    /// </summary>
    /// <param name="builder">The <see cref="RouteHandlerBuilder"/>.</param>
    /// <param name="filterFactory">A <see cref="Delegate"/> representing the logic for constructing the filter.</param>
    /// <returns>A <see cref="RouteHandlerBuilder"/> that can be used to further customize the route handler.</returns>
    public static RouteHandlerBuilder AddFilter(this RouteHandlerBuilder builder, Func<MethodInfo, RouteHandlerFilterDelegate, RouteHandlerFilterDelegate> filterFactory)
    {
        builder.RouteHandlerFilterFactories.Add(filterFactory);
        return builder;
    }
}
