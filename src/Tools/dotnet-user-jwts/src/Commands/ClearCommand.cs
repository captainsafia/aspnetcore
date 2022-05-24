// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.CommandLineUtils;

namespace Microsoft.AspNetCore.Authentication.JwtBearer.Tools;

internal sealed class ClearCommand
{
    public static void Register(ProjectCommandLineApplication app)
    {
        app.Command("clear", cmd =>
        {
            cmd.Description = "Delete all issued JWTs for a project";

            var forceOption = cmd.Option(
                "--force",
                "Don't prompt for confirmation before deleting JWTs",
                CommandOptionType.NoValue);

            cmd.HelpOption("-h|--help");

            cmd.OnExecute(() =>
            {
                return Execute(app.ProjectOption.Value(), forceOption.HasValue());
            });
        });
    }

    private static int Execute(string projectPath, bool force)
    {
        var project = DevJwtCliHelpers.GetProject(projectPath);
        if (project == null)
        {
            Console.WriteLine($"No project found at `-p|--project` path or current directory.");
            return 1;
        }

        var userSecretsId = DevJwtCliHelpers.GetUserSecretsId(project);
        var jwtStore = new JwtStore(userSecretsId);

        var count = jwtStore.Jwts.Count;

        if (count == 0)
        {
            Console.WriteLine($"There are no JWTs to delete from {project}");
            return 0;
        }

        if (!force)
        {
            Console.WriteLine($"Are you sure you want to delete {count} JWT(s) for {project}? \n [Y]es / [N]o");
            if (Console.ReadKey().Key != ConsoleKey.Y)
            {
                Console.WriteLine("Cancelled, no JWTs were deleted");
                return 0;
            }
        }

        jwtStore.Jwts.Clear();
        jwtStore.Save();

        Console.WriteLine($"Deleted {count} token(s) from {project} successfully");

        return 0;
    }
}
