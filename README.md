# VS Launcher (Portable Multi-Instance Visual Studio Code)

`Launcher.exe` is a lightweight, native Windows GUI manager for portable VS Code instances. It automatically detects, creates, organizes, launches, and updates portable VS Code instances and the core installation without duplicating core files.

```text
vsinstances/
├── VSLauncherSetup.exe      # Self-contained Windows installer (~61 KB)
├── Launcher.exe             # VS Launcher application (Runs as Administrator)
├── installer/
│   ├── VSLauncherSetup.exe  # Standalone installer executable
│   ├── VSLauncherSetup.cs   # Installer source code
│   └── VSLauncher-Portable.zip # Lightweight portable package (~20 KB)
├── vscode/                  # Shared portable VS Code binaries & extensions
│   ├── Code.exe
│   └── data/
│       ├── user-data/       # Core portable user data
│       └── extensions/      # Central shared extensions directory
├── _template/               # Clean launcher script template
│   └── launch.bat
├── instance1/               # Instance 1
│   ├── launch.bat
│   └── instance.json
├── instance2/               # Instance 2
│   ├── launch.bat
│   └── instance.json
├── instance3/               # Instance 3
│   ├── launch.bat
│   └── instance.json
└── README.md
```

## Distribution & Installation

### 1. `VSLauncherSetup.exe` (Self-Contained Installer)
- **Ultra-lightweight (~61 KB):** Does not include the ~1 GB VS Code archive.
- Allows the user to select the installation destination (e.g. `D:\VSInstances` or `C:\VSInstances`).
- Installs `Launcher.exe`, `_template/`, and `instance1/`.
- Creates **Desktop** and **Start Menu** shortcuts with the VS Code icon.
- Automatically launches VS Launcher upon setup completion.
- On first launch, the user can click **`⬇ Install VS Code`** in the top-right header to automatically download and unpack the latest official portable core.

### 2. `VSLauncher-Portable.zip` (Portable Archive)
- **Size: ~20 KB.**
- Contains only `Launcher.exe`, `_template/`, `instance1/`, and documentation.
- Simply extract to any folder or flash drive and run `Launcher.exe`.
