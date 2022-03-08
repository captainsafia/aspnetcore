// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Http;

/// <summary>
/// 
/// </summary>
public class EndpointApiDescription
{
    /// <summary>
    /// 
    /// </summary>
    public string? EndpointName { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public string? GroupName { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public string HttpMethod { get; set; } = default!;
    /// <summary>
    /// 
    /// </summary>
    public string? EndpointDescription { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public string[]? EndpointTags { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public EndpointMetadataCollection Metadata { get; set; } = default!;
    /// <summary>
    /// 
    /// </summary>
    public Dictionary<string, EndpointApiParameter> Parameters = new();
    /// <summary>
    /// 
    /// </summary>
    public Dictionary<int, EndpointApiResponse> Responses = new();
    /// <summary>
    /// 
    /// </summary>
    public Dictionary<string, object?> Items = new();
}
