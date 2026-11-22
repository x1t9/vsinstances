using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Windows.Forms;

namespace VSLauncherInstaller
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            // Ensure running as Administrator for shortcuts / filesystem access
            if (!IsAdministrator())
            {
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = Application.ExecutablePath,
                        UseShellExecute = true,
                        Verb = "runas"
                    };
                    Process.Start(psi);
                    return;
                }
                catch
                {
                    MessageBox.Show(
                        "Administrator privileges are required to run the installer.\nPlease approve the prompt to continue.",
                        "Elevation Required",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                    return;
                }
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SetupForm());
        }

        private static bool IsAdministrator()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }
    }

    public class SetupForm : Form
    {
        private TextBox txtPath;
        private Button btnBrowse;
        private CheckBox chkDesktopShortcut;
        private CheckBox chkStartMenuShortcut;
        private CheckBox chkLaunchNow;
        private Button btnInstall;
        private Button btnCancel;
        private ProgressBar progressBar;
        private Label lblStatus;

        public SetupForm()
        {
            InitializeUI();
            LoadIcon();
        }

        private void InitializeUI()
        {
            this.Text = "VS Launcher Setup v1.3.0";
            this.Size = new Size(540, 440);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = true;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            this.BackColor = Color.FromArgb(246, 247, 249);

            // Banner Header
            Panel pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 68,
                BackColor = Color.FromArgb(30, 30, 30),
                Padding = new Padding(16, 10, 16, 10)
            };

            Label lblTitle = new Label
            {
                Text = "VS Launcher Setup v1.3.0",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                Location = new Point(14, 10),
                AutoSize = true
            };

            Label lblSubtitle = new Label
            {
                Text = "Install or update VS Launcher with isolated instance profiles",
                ForeColor = Color.FromArgb(170, 170, 170),
                Font = new Font("Segoe UI", 8.5F),
                Location = new Point(16, 36),
                AutoSize = true
            };

            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblSubtitle);
            this.Controls.Add(pnlHeader);

            // Body Container
            Panel pnlBody = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20, 14, 20, 14)
            };
            this.Controls.Add(pnlBody);
            pnlBody.BringToFront();

            int y = 8;

            Label lblPrompt = new Label
            {
                Text = "Destination Directory:",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Location = new Point(0, y),
                AutoSize = true
            };
            pnlBody.Controls.Add(lblPrompt);
            y += 22;

            // Default directory choice
            string detectedDir = DetectExistingInstallDirectory();
            string defaultDir = !string.IsNullOrEmpty(detectedDir)
                ? detectedDir
                : (Directory.Exists(@"D:\") ? @"D:\VSInstances" : @"C:\VSInstances");

            txtPath = new TextBox
            {
                Text = defaultDir,
                Location = new Point(0, y),
                Size = new Size(390, 24),
                Font = new Font("Segoe UI", 9.5F)
            };
            pnlBody.Controls.Add(txtPath);

            btnBrowse = new Button
            {
                Text = "Browse...",
                Location = new Point(398, y - 1),
                Size = new Size(86, 26),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand
            };
            btnBrowse.Click += (s, e) =>
            {
                using (FolderBrowserDialog fbd = new FolderBrowserDialog())
                {
                    fbd.Description = "Select Destination Folder for VS Launcher";
                    fbd.SelectedPath = txtPath.Text;
                    if (fbd.ShowDialog() == DialogResult.OK)
                    {
                        txtPath.Text = fbd.SelectedPath;
                    }
                }
            };
            pnlBody.Controls.Add(btnBrowse);
            y += 38;

            // Info note panel
            Panel pnlNote = new Panel
            {
                Location = new Point(0, y),
                Size = new Size(484, 66),
                BackColor = Color.FromArgb(235, 243, 250),
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(10, 8, 10, 8)
            };
            Label lblNote = new Label
            {
                Text = "ℹ Existing VS Launcher installations are updated in place, preserving VS Code core, instances, profiles, extensions, and login sessions.\r\nIf VS Code core is not found in the selected folder, install it from VS Launcher after setup.",
                ForeColor = Color.FromArgb(20, 80, 140),
                Font = new Font("Segoe UI", 8.25F),
                Dock = DockStyle.Fill
            };
            pnlNote.Controls.Add(lblNote);
            pnlBody.Controls.Add(pnlNote);
            y += 62;

            // Options
            chkDesktopShortcut = new CheckBox
            {
                Text = "Create or update Desktop shortcut for VS Launcher",
                Checked = true,
                Location = new Point(4, y),
                AutoSize = true
            };
            pnlBody.Controls.Add(chkDesktopShortcut);
            y += 24;

            chkStartMenuShortcut = new CheckBox
            {
                Text = "Create or update Start Menu shortcut",
                Checked = true,
                Location = new Point(4, y),
                AutoSize = true
            };
            pnlBody.Controls.Add(chkStartMenuShortcut);
            y += 24;

            chkLaunchNow = new CheckBox
            {
                Text = "Launch VS Launcher after installation/update",
                Checked = true,
                Location = new Point(4, y),
                AutoSize = true
            };
            pnlBody.Controls.Add(chkLaunchNow);
            y += 30;

            progressBar = new ProgressBar
            {
                Location = new Point(0, y),
                Size = new Size(484, 14),
                Visible = false,
                Style = ProgressBarStyle.Marquee
            };
            pnlBody.Controls.Add(progressBar);

            string statusText = "Ready to install or update.";
            try
            {
                if (Directory.Exists(defaultDir))
                {
                    bool hasLauncher = File.Exists(Path.Combine(defaultDir, "Launcher.exe"));
                    bool hasVsCode = File.Exists(Path.Combine(defaultDir, "vscode", "Code.exe"));
                    if (hasLauncher && hasVsCode) statusText = "Existing VS Launcher installation and VS Code core detected.";
                    else if (hasLauncher) statusText = "Existing VS Launcher installation detected.";
                    else if (hasVsCode) statusText = "Existing VS Code core detected in selected folder.";
                }
            }
            catch { }

            lblStatus = new Label
            {
                Text = statusText,
                Location = new Point(0, y + 16),
                AutoSize = true,
                ForeColor = Color.Gray,
                Font = new Font("Segoe UI", 8F)
            };
            pnlBody.Controls.Add(lblStatus);

            // Bottom Buttons
            Panel pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 48,
                BackColor = Color.FromArgb(238, 239, 242),
                Padding = new Padding(14, 8, 14, 8)
            };
            this.Controls.Add(pnlBottom);

            btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(pnlBottom.Width - 95, 10),
                Size = new Size(80, 28),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                FlatStyle = FlatStyle.System
            };
            btnCancel.Click += (s, e) => this.Close();

            btnInstall = new Button
            {
                Text = "Install / Update",
                Location = new Point(pnlBottom.Width - 210, 10),
                Size = new Size(108, 28),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnInstall.FlatAppearance.BorderSize = 0;
            btnInstall.Click += (s, e) => PerformInstall();

            txtPath.TextChanged += (s, e) => UpdateStatusForPath();

            pnlBottom.Controls.Add(btnInstall);
            pnlBottom.Controls.Add(btnCancel);
        }

        private void LoadIcon()
        {
            try
            {
                this.Icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
            }
            catch { }
        }

        private void PerformInstall()
        {
            string dest = txtPath.Text.Trim();
            if (string.IsNullOrEmpty(dest))
            {
                MessageBox.Show("Please select a destination folder.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnInstall.Enabled = false;
            btnBrowse.Enabled = false;
            txtPath.Enabled = false;
            progressBar.Visible = true;
            lblStatus.Text = "Installing/updating VS Launcher files...";

            try
            {
                bool existingInstall = LooksLikeExistingInstall(dest);
                bool hasAnyInstance = HasAnyInstance(dest);
                bool hasVsCodeCore = File.Exists(Path.Combine(dest, "vscode", "Code.exe"));

                Directory.CreateDirectory(dest);

                // 1. Extract/update embedded Launcher.exe
                string exePath = Path.Combine(dest, "Launcher.exe");
                ExtractEmbeddedFile("Launcher.exe", exePath);

                // 2. Create/update _template folder & launch.bat
                string templateDir = Path.Combine(dest, "_template");
                Directory.CreateDirectory(templateDir);
                string templateBat = "@echo off\r\nstart \"\" \"%~dp0..\\vscode\\Code.exe\" --user-data-dir \"%~dp0user-data\" --extensions-dir \"%~dp0..\\extensions\" --new-window %*\r\n";
                File.WriteAllText(Path.Combine(templateDir, "launch.bat"), templateBat);

                // 3. Create sample instance1 only for a fresh install with no existing instances
                if (!hasAnyInstance)
                {
                    string inst1Dir = Path.Combine(dest, "instance1");
                    Directory.CreateDirectory(inst1Dir);

                    string inst1BatPath = Path.Combine(inst1Dir, "launch.bat");
                    if (!File.Exists(inst1BatPath))
                    {
                        string inst1Bat = "@echo off\r\nstart \"\" \"%~dp0..\\vscode\\Code.exe\" --user-data-dir \"%~dp0user-data\" --extensions-dir \"%~dp0..\\extensions\" --new-window %*\r\n";
                        File.WriteAllText(inst1BatPath, inst1Bat);
                    }

                    string inst1JsonPath = Path.Combine(inst1Dir, "instance.json");
                    if (!File.Exists(inst1JsonPath))
                    {
                        string inst1Json = "{\r\n  \"name\": \"instance1\",\r\n  \"extensionMode\": \"shared\",\r\n  \"createdAt\": \"" + DateTime.UtcNow.ToString("o") + "\"\r\n}\r\n";
                        File.WriteAllText(inst1JsonPath, inst1Json);
                    }
                }

                // 4. Create/update README.md
                string readmeContent = "# VS Launcher v1.3.0 (Portable Multi-Instance Visual Studio Code)\r\n\r\n"
                    + "Welcome to VS Launcher! To get started:\r\n"
                    + "1. Launch Launcher.exe\r\n"
                    + "2. If VS Code core is not installed, click 'Install VS Code' in the top-right header to download and unpack the portable VS Code core.\r\n"
                    + "3. Create or launch instances; each instance keeps its own persistent VS Code sign-in and extension session. Sign in once per instance.\r\n\r\n"
                    + "Cloning a profile copies preferences only, not login/session data. If instances were created by cloning in v1.2.0, sign out of affected instances and sign in to each again once to establish independent sessions. Updates preserve existing VS Code core, instances, profiles, extensions, and login sessions. Select an instance and use Rename Selected to change its display name, or Change Extension Mode to switch between the main shared extensions and that instance's local extensions. Mode changes apply on the next launch; running VS Code windows must be restarted. Renaming does not move the instance folder or its existing shortcuts.\r\n";
                File.WriteAllText(Path.Combine(dest, "README.md"), readmeContent);

                // 5. Create Desktop shortcut
                if (chkDesktopShortcut.Checked)
                {
                    string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    CreateShortcut(Path.Combine(desktop, "VS Launcher.lnk"), exePath, dest);
                }

                // 6. Create Start Menu shortcut
                if (chkStartMenuShortcut.Checked)
                {
                    string startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), "VS Launcher");
                    Directory.CreateDirectory(startMenu);
                    CreateShortcut(Path.Combine(startMenu, "VS Launcher.lnk"), exePath, dest);
                }

                string action = existingInstall ? "updated" : "installed";
                lblStatus.Text = existingInstall ? "Update complete." : "Installation complete.";
                progressBar.Visible = false;

                string postMessage = hasVsCodeCore
                    ? "Existing VS Code core was detected and preserved."
                    : "When VS Launcher opens, click '⬇ Install VS Code' in the top right if the portable VS Code core is not installed yet.";

                MessageBox.Show(
                    "VS Launcher has been successfully " + action + " at:\n" + dest + "\n\n" + postMessage + "\n\nExisting instances, profiles, extensions, and login sessions were preserved.",
                    existingInstall ? "Update Complete" : "Installation Complete",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );

                if (chkLaunchNow.Checked)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = exePath,
                        WorkingDirectory = dest,
                        UseShellExecute = true
                    });
                }

                this.Close();
            }
            catch (Exception ex)
            {
                progressBar.Visible = false;
                btnInstall.Enabled = true;
                btnBrowse.Enabled = true;
                txtPath.Enabled = true;
                lblStatus.Text = "Installation failed: " + ex.Message;
                MessageBox.Show("Failed to install or update: " + ex.Message, "Installation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateStatusForPath()
        {
            string path = txtPath.Text.Trim();
            if (string.IsNullOrEmpty(path))
            {
                lblStatus.Text = "Ready to install or update.";
                return;
            }

            try
            {
                bool hasLauncher = File.Exists(Path.Combine(path, "Launcher.exe"));
                bool hasVsCode = File.Exists(Path.Combine(path, "vscode", "Code.exe"));
                bool hasInstances = HasAnyInstance(path);

                if (hasLauncher && hasVsCode) lblStatus.Text = "Existing VS Launcher installation and VS Code core detected. Instances and login sessions will be preserved.";
                else if (hasLauncher || hasInstances) lblStatus.Text = "Existing VS Launcher installation detected. Instances and login sessions will be preserved.";
                else if (hasVsCode) lblStatus.Text = "Existing VS Code core detected in selected folder. It will be preserved.";
                else lblStatus.Text = "Ready to install or update.";
            }
            catch
            {
                lblStatus.Text = "Ready to install or update.";
            }
        }

        private string DetectExistingInstallDirectory()
        {
            try
            {
                string setupDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string[] candidates = new string[]
                {
                    setupDir,
                    @"D:\VSInstances",
                    @"C:\VSInstances",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VSLauncher"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "VSLauncher")
                };

                foreach (string candidate in candidates)
                {
                    if (LooksLikeExistingInstall(candidate))
                        return candidate;
                }

                foreach (string candidate in candidates)
                {
                    if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(Path.Combine(candidate, "vscode", "Code.exe")))
                        return candidate;
                }
            }
            catch { }
            return null;
        }

        private bool LooksLikeExistingInstall(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return false;
                if (File.Exists(Path.Combine(path, "Launcher.exe"))) return true;
                if (File.Exists(Path.Combine(path, "VSLauncherSetup.exe")) && Directory.Exists(Path.Combine(path, "vscode"))) return true;
                if (Directory.Exists(Path.Combine(path, "_template")) && (Directory.Exists(Path.Combine(path, "vscode")) || HasAnyInstance(path))) return true;
                return HasAnyInstance(path);
            }
            catch { return false; }
        }

        private bool HasAnyInstance(string root)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return false;
                foreach (string dir in Directory.GetDirectories(root))
                {
                    string name = Path.GetFileName(dir);
                    string lower = name.ToLowerInvariant();
                    if (lower == "vscode" || lower == "_template" || lower == "installer" || lower.StartsWith(".") || lower == "$recycle.bin") continue;
                    if (File.Exists(Path.Combine(dir, "launch.bat")) || File.Exists(Path.Combine(dir, "instance.json"))) return true;
                }
            }
            catch { }
            return false;
        }

        private void ExtractEmbeddedFile(string resourceName, string outputPath)
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            using (Stream s = asm.GetManifestResourceStream(resourceName))
            {
                if (s == null)
                {
                    throw new Exception("Embedded resource '" + resourceName + "' not found in setup archive.");
                }
                using (FileStream fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
                {
                    byte[] buffer = new byte[81920];
                    int read;
                    while ((read = s.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        fs.Write(buffer, 0, read);
                    }
                }
            }
        }

        private void CreateShortcut(string shortcutPath, string targetPath, string workingDir)
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return;
                dynamic shell = Activator.CreateInstance(shellType);
                dynamic shortcut = shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = targetPath;
                shortcut.WorkingDirectory = workingDir;
                shortcut.IconLocation = targetPath + ",0";
                shortcut.Save();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Shortcut error: " + ex.Message);
            }
        }
    }
}
