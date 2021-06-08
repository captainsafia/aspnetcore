**Problem**
Codespaces doesn't clone the repo with submodules included so they must be pulled in manually via `git submodule update --init --recursive`.

**Problem**
The Omnisharp VS Code extension assumes that dotnet is installed globally on the machine. For aspnetcore and other repos that install dotnet to a local installation, this causes a lot of issues with the O# extension, particularly around resolving the correct version of the installation.

**Possible Solutions**
- Add option in O# to provide default installation path for extensions.
- Update devcontainer configuration to add installation location to the path.

**Problem**
The VS Code debugging experience only populates the dropdown with the launch configurations that are located in the top-level of the project directory. `launch.json` configurations that are located in sub-directories won't appear. This mean's that it's not possible for a user to run project-specific configs after launching a CodeSpace.

**Possible Solutions**
- Move all configs to the top-level. However, this would mean that they wouldn't be visible if a user opened an individual project.
- See if VS Code provides an option for populating the debugger with all `launch.json` files in a repo.

