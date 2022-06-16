// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.AspNetCore.Authentication.JwtBearer.Tools;

internal sealed class RemoveCommand
{
    public static void Register(ProjectCommandLineApplication app)
    {
        app.Command("remove", cmd =>
        {
            cmd.Description = Resources.RemoveCommand_Description;

            var idArgument = cmd.Argument("[id]", Resources.RemoveCommand_IdArgument_Description);
            cmd.HelpOption("-h|--help");

            cmd.OnExecute(() =>
            {
                if (idArgument.Value is null)
                {
                    cmd.ShowHelp();
                    return 0;
                }
                return Execute(cmd.Reporter, cmd.OutputOption.HasValue(), cmd.ProjectOption.Value(), idArgument.Value);
            });
        });
    }

    private static int Execute(IReporter reporter, bool hasOutput, string projectPath, string id)
    {
        if (!DevJwtCliHelpers.GetProjectAndSecretsId(projectPath, reporter, out var project, out var userSecretsId))
        {
            return 1;
        }
        var jwtStore = new JwtStore(userSecretsId);

        if (!jwtStore.Jwts.ContainsKey(id))
        {
            if (!hasOutput)
            {
                reporter.Error(Resources.FormatRemoveCommand_NoJwtFound(id));
            }
            return 1;
        }

        var jwt = jwtStore.Jwts[id];
        var appsettingsFilePath = Path.Combine(Path.GetDirectoryName(project), "appsettings.Development.json");
        JwtAuthenticationSchemeSettings.RemoveScheme(appsettingsFilePath, jwt.Scheme);
        jwtStore.Jwts.Remove(id);
        jwtStore.Save();

        if (!hasOutput)
        {
            reporter.Output(Resources.FormatRemoveCommand_Confirmed(id));
        }
        

        return 0;
    }
}
