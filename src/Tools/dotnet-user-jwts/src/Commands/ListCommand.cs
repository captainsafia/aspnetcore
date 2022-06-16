// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.AspNetCore.Authentication.JwtBearer.Tools;

internal sealed class ListCommand
{
    public static void Register(ProjectCommandLineApplication app)
    {
        app.Command("list", cmd =>
        {
            cmd.Description = Resources.ListCommand_Description;

            var showTokensOption = cmd.Option(
                "--show-tokens",
                Resources.ListCommand_ShowTokenOption_Description,
                CommandOptionType.NoValue);

            cmd.HelpOption("-h|--help");

            cmd.OnExecute(() =>
            {
                return Execute(cmd.Reporter, cmd.OutputOption.Value(), cmd.ProjectOption.Value(), showTokensOption.HasValue());
            });
        });
    }

    private static int Execute(IReporter reporter, string format, string projectPath, bool showTokens)
    {
        if (!DevJwtCliHelpers.GetProjectAndSecretsId(projectPath, reporter, out var project, out var userSecretsId))
        {
            return 1;
        }
        var jwtStore = new JwtStore(userSecretsId);

        if (format is null)
        {
            reporter.Output(Resources.FormatListCommand_Project(project));
            reporter.Output(Resources.FormatListCommand_UserSecretsId(userSecretsId));
        }

        if (jwtStore.Jwts is { Count: > 0 } jwts)
        {
            if (format == "json")
            {
                reporter.Output(JsonSerializer.Serialize(jwts.Values));
            }
            else
            {
                PrintJwtsTable(reporter, jwts, showTokens);
            }
        }
        else
        {
            if (format == "json")
            {
                reporter.Output("[]");
            }
            else
            {
                reporter.Output(Resources.ListCommand_NoJwts);
            }
        }

        return 0;
    }

    private static void PrintJwtsTable(IReporter reporter, IDictionary<string, Jwt> jwts, bool showTokens)
    {
        var table = new ConsoleTable(reporter);
        table.AddColumns(Resources.JwtPrint_Id, Resources.JwtPrint_Scheme, Resources.JwtPrint_Audiences, Resources.JwtPrint_IssuedOn, Resources.JwtPrint_ExpiresOn);

        if (showTokens)
        {
            table.AddColumns(Resources.JwtPrint_Token);
        }

        foreach (var jwtRow in jwts)
        {
            var jwt = jwtRow.Value;
            if (showTokens)
            {
                table.AddRow(jwt.Id, jwt.Scheme, jwt.Audience, jwt.Issued.ToString("O"), jwt.Expires.ToString("O"), jwt.Token);
            }
            else
            {
                table.AddRow(jwt.Id, jwt.Scheme, jwt.Audience, jwt.Issued.ToString("O"), jwt.Expires.ToString("O"));
            }
        }

        table.Write();
    }
}
