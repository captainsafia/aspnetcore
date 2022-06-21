// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Authentication;

internal sealed class AuthenticationSchemeConfigurationOptions<T> : IConfigureNamedOptions<T> where T : AuthenticationSchemeOptions
{
    private readonly IAuthenticationConfigurationProvider _authenticationConfigurationProvider;

    /// <summary>
    /// Initializes a new <see cref="AuthenticationSchemeConfigurationOptions{T}"/> given the configuration
    /// provided by the <paramref name="configurationProvider"/>.
    /// </summary>
    /// <param name="configurationProvider">An <see cref="IAuthenticationConfigurationProvider"/> instance.</param>
    public AuthenticationSchemeConfigurationOptions(IAuthenticationConfigurationProvider configurationProvider)
    {
        _authenticationConfigurationProvider = configurationProvider;
    }

    /// <inheritdoc />
    [UnconditionalSuppressMessage("Trimmer", "IL2026", Justification = "Trimmer warnings are presented in AuthenticationSchemeConfigureOptions.")]
    public void Configure(string? name, T options)
    {
        if (string.IsNullOrEmpty(name))
        {
            return;
        }

        var configSection = _authenticationConfigurationProvider.GetSchemeConfiguration(name);

        if (configSection is null || !configSection.GetChildren().Any())
        {
            return;
        }

        configSection.Bind(options);
    }

    /// <inheritdoc />
    public void Configure(T options)
    {
        Configure(Options.DefaultName, options);
    }
}
