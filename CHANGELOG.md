# Changelog

## v1.3.0

- Launch each instance directly with absolute, instance-specific user-data and extension paths so VS Code authentication and editor state remain scoped to that instance across launches.
- Clone only user preferences (settings, keybindings, tasks, and snippets); do not copy VS Code authentication/session storage into a new instance.
- Preserve existing instance profiles and sign-in state during updates.

## v1.2.0

- Improve instance session isolation and login retention by detecting/removing legacy portable `vscode/data` before launch, normalizing instance launch scripts, and ensuring each launch uses the correct per-instance `--user-data-dir` and extension directory.
- Make the installer update-safe: detect existing VS Launcher/VS Code installations, allow browsing for the install directory, patch/update in place, and preserve existing VS Code core, instances, profiles, extensions, and login sessions.
- Rebuild and include the v1.2 launcher, setup installer, and portable archive.

## v1.1.1

- Fix migration failures on deeply nested VS Code extension paths by using Robocopy's long-path-capable tree copy, preserving existing destination files, and retrying incomplete migrations safely.

## v1.1.0

- Isolate each VS Code instance's settings, sign-ins, and extension session data by preventing the shared portable `data/` folder from overriding per-instance paths.
- Migrate existing main profile and shared extensions without overwriting existing files; preserve the old portable data in a backup folder.
- Rebuild and include the v1.1 launcher, setup installer, and portable archive.
