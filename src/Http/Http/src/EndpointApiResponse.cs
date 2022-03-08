// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Http;

/// <summary>
/// 
/// </summary>
public sealed class EndpointApiResponse
{
    /// <summary>
    /// 
    /// </summary>
    public int StatusCode { get; set; } = default!;
    /// <summary>
    /// 
    /// </summary>
    public string? Description { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public Type ResponseType { get; set; } = default!;
    /// <summary>
    /// 
    /// </summary>
    public IReadOnlyList<string>? ContentTypes { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public Dictionary<string, object?> Items = new();
}
