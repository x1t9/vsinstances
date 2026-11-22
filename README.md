# VS Launcher

VS Launcher v1.2.0 is a Windows desktop app for managing portable Visual Studio Code instances. Each instance has its own user-data/profile folder, including its own sign-ins, extension sessions, and VS Code account state, and can use either the shared extension installation folder or its own local extensions folder.

Version 1.2.0 improves session isolation and update safety. The launcher detects and migrates legacy portable `vscode/data` state before launch, normalizes instance launch scripts to use per-instance `--user-data-dir` and the correct extensions directory, and prevents the portable data folder from overriding isolated profiles. The installer can detect an existing VS Launcher installation or VS Code core, let you browse for the install directory if needed, and patch/update in place while preserving existing VS Code core, instances, profiles, extensions, and login sessions.

## Install and update

- **Installer:** Run `VSLauncherSetup.exe`. It will try to detect an existing VS Launcher installation and preselect it; choose or browse to the correct install folder if needed, then install/update. Existing instances, user data, extensions, shortcuts, and VS Code core are preserved.
- **Portable:** Extract `installer/VSLauncher-Portable.zip` to a writable folder and run `Launcher.exe`. To update a portable installation, replace `Launcher.exe` (and the installer package if desired) or run the v1.2.0 installer against the existing folder.
- If the portable VS Code core is not installed yet, click **Install VS Code** in the top-right of the launcher. VS Launcher downloads and extracts the official Windows x64 archive into the `vscode/` subfolder.

The installer and launcher request administrator privileges; approve the Windows UAC prompt. Use a writable install folder to keep application data out of protected directories.

## Use instances

The Instances list shows the detected instances and their extension mode. Select an instance and use the buttons below the list:

- **Launch** starts the selected instance. Double-clicking an instance does the same.
- **Rename Selected** changes the instance's display name in the launcher and its metadata. Its folder, profile, and existing shortcuts are left in place.
- **Change Extension Mode** chooses between **Shared with Main** (the root `extensions/` folder) and **Isolated (Custom)** (the instance's `extensions/` folder). Switching modes does not delete either extension folder; existing local extensions remain available if you switch back.
- **Folder** opens the instance folder, **Shortcut** creates a desktop shortcut, and **Delete** permanently removes the selected instance and its user data.
- **Refresh** rescans instances and checks for VS Code updates.

Extension mode changes are saved immediately and apply the next time the instance is launched. If it is already running, close and relaunch its VS Code window to use the new extension folder. Shared mode uses the same extensions as **Open Main**.

To create an instance, enter a name, choose its extension mode, and optionally clone a profile or create a desktop shortcut. **Isolated (Copy from Main)** copies the current main extensions once; later changes to the main extension folder are not synchronized.

## Files and data

- `Launcher.exe` is the VS Launcher application.
- `vscode/` contains the downloaded VS Code core. It is not included in the repository or portable package.
- Root `user-data/` and `extensions/` contain the main profile and shared extension installations. Each instance's `user-data/` contains its own settings, authentication, and extension session data.
- Each instance folder contains `launch.bat` and `instance.json`; `user-data/` and optional `extensions/` are created locally and are ignored by Git.
- `_template/launch.bat` is a sample launch script.

## Build

Build the Windows launcher from source with the .NET SDK:

```powershell
dotnet build Launcher.csproj -c Release
```

The project targets .NET Framework 4.8 and restores its reference assemblies through NuGet. The resulting executable is written to `bin/Release/net48/Launcher.exe`.

To build the installer after building `Launcher.csproj`, run:

```powershell
Copy-Item bin\Release\net48\Launcher.exe Launcher.exe
dotnet build installer/VSLauncherSetup.csproj -c Release
```

The installer project embeds the root `Launcher.exe` as its payload. The built installer is written to `installer/bin/Release/net48/VSLauncherSetup.exe`.

## License

VS Launcher is licensed under the [MIT License](LICENSE).
