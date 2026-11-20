# Changelog

## v1.1.1

- Fix migration failures on deeply nested VS Code extension paths by using Robocopy's long-path-capable tree copy, preserving existing destination files, and retrying incomplete migrations safely.

## v1.1.0

- Isolate each VS Code instance's settings, sign-ins, and extension session data by preventing the shared portable `data/` folder from overriding per-instance paths.
- Migrate existing main profile and shared extensions without overwriting existing files; preserve the old portable data in a backup folder.
- Rebuild and include the v1.1 launcher, setup installer, and portable archive.
