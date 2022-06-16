// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.AspNetCore.Authentication.JwtBearer.Tools;

internal sealed class KeyCommand
{
    public static void Register(ProjectCommandLineApplication app)
    {
        app.Command("key", cmd =>
        {
            cmd.Description = Resources.KeyCommand_Description;

            var resetOption = cmd.Option(
                "--reset",
                Resources.KeyCommand_ResetOption_Description,
                CommandOptionType.NoValue);

            var forceOption = cmd.Option(
                "--force",
                Resources.KeyCommand_ForceOption_Description,
                CommandOptionType.NoValue);

            cmd.HelpOption("-h|--help");

            cmd.OnExecute(() =>
            {
                return Execute(cmd.Reporter, cmd.OutputOption.Value(), cmd.ProjectOption.Value(), resetOption.HasValue(), forceOption.HasValue());
            });
        });
    }

    private static int Execute(IReporter reporter, string outputFormat, string projectPath, bool reset, bool force)
    {
        if (!DevJwtCliHelpers.GetProjectAndSecretsId(projectPath, reporter, out var _, out var userSecretsId))
        {
            return 1;
        }

        if (reset == true)
        {
            if (!force)
            {
                reporter.Output(Resources.KeyCommand_Permission);
                reporter.Error("[Y]es / [N]o");
                if (Console.ReadLine().Trim().ToUpperInvariant() != "Y" && outputFormat == null)
                {
                    reporter.Output(Resources.KeyCommand_Canceled);
                    return 0;
                }
            }

            var key = DevJwtCliHelpers.CreateSigningKeyMaterial(userSecretsId, reset: true);
            var encodedCode = new Dictionary<string, string>() {
                { DevJwtsDefaults.SigningKeyConfigurationKey, Convert.ToBase64String(key) }
            };
            if (outputFormat == null)
            {
                reporter.Output(Resources.FormatKeyCommand_KeyCreated(encodedCode));
            }
            else
            {
                reporter.Output(JsonSerializer.Serialize(encodedCode));
            }
            return 0; 
        }

        var projectConfiguration = new ConfigurationBuilder()
            .AddUserSecrets(userSecretsId)
            .Build();
        var signingKeyMaterial = projectConfiguration[DevJwtsDefaults.SigningKeyConfigurationKey];

        if (signingKeyMaterial is null && outputFormat == null)
        {
            reporter.Output(Resources.KeyCommand_KeyNotFound);
            return 0;
        }

        if (outputFormat == "json")
        {
            var encodedCode = new Dictionary<string, string>() {
                { DevJwtsDefaults.SigningKeyConfigurationKey, signingKeyMaterial }
            };
            reporter.Output(JsonSerializer.Serialize(encodedCode));
        }
        else
        {
            reporter.Output(Resources.FormatKeyCommand_Confirmed(signingKeyMaterial));
        }
        
        return 0;
    }
}
