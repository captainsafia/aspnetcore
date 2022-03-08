// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;

namespace Microsoft.AspNetCore.Http;

/// <summary>
/// 
/// </summary>
public sealed class EndpointApiParameter
{
    /// <summary>
    /// 
    /// </summary>
    public string Name { get; set; } = default!;
    /// <summary>
    /// 
    /// </summary>
    public Type ParameterType { get; set; } = default!;
    /// <summary>
    /// 
    /// </summary>
    public string? Description { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public ParameterSource Source { get; set; } = default!;
    /// <summary>
    /// 
    /// </summary>
    public ParameterInfo ParameterInfo { get; set; } = default!;
    /// <summary>
    /// 
    /// </summary>
    public Dictionary<string, object?> Items = new();

    /// <summary>
    /// 
    /// </summary>
    public IReadOnlyList<string>? ContentTypes { get; set; }
}
