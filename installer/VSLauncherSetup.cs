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
            this.Text = "VS Launcher Setup";
            this.Size = new Size(540, 420);
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
                Text = "VS Launcher Setup",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                Location = new Point(14, 10),
                AutoSize = true
            };

            Label lblSubtitle = new Label
            {
                Text = "Install portable multi-instance VS Code manager",
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
            string defaultDir = Directory.Exists(@"D:\") ? @"D:\VSInstances" : @"C:\VSInstances";

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
                Size = new Size(484, 52),
                BackColor = Color.FromArgb(235, 243, 250),
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(10, 8, 10, 8)
            };
            Label lblNote = new Label
            {
                Text = "ℹ VS Code core is not bundled to keep this installer ultra-light (~60 KB).\r\nWhen first run, click 'Install VS Code' in VS Launcher to download the portable core.",
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
                Text = "Create Desktop Shortcut for VS Launcher",
                Checked = true,
                Location = new Point(4, y),
                AutoSize = true
            };
            pnlBody.Controls.Add(chkDesktopShortcut);
            y += 24;

            chkStartMenuShortcut = new CheckBox
            {
                Text = "Create Start Menu Shortcut",
                Checked = true,
                Location = new Point(4, y),
                AutoSize = true
            };
            pnlBody.Controls.Add(chkStartMenuShortcut);
            y += 24;

            chkLaunchNow = new CheckBox
            {
                Text = "Launch VS Launcher after installation",
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

            lblStatus = new Label
            {
                Text = "Ready to install.",
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
                Text = "Install",
                Location = new Point(pnlBottom.Width - 190, 10),
                Size = new Size(88, 28),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnInstall.FlatAppearance.BorderSize = 0;
            btnInstall.Click += (s, e) => PerformInstall();

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
            lblStatus.Text = "Installing VS Launcher files...";

            try
            {
                Directory.CreateDirectory(dest);

                // 1. Extract embedded Launcher.exe
                string exePath = Path.Combine(dest, "Launcher.exe");
                ExtractEmbeddedFile("Launcher.exe", exePath);

                // 2. Create _template folder & launch.bat
                string templateDir = Path.Combine(dest, "_template");
                Directory.CreateDirectory(templateDir);
                string templateBat = "@echo off\r\nstart \"\" \"%~dp0..\\vscode\\Code.exe\" --user-data-dir \"%~dp0user-data\" --extensions-dir \"%~dp0..\\vscode\\data\\extensions\" %*\r\n";
                File.WriteAllText(Path.Combine(templateDir, "launch.bat"), templateBat);

                // 3. Create instance1 folder, launch.bat & instance.json
                string inst1Dir = Path.Combine(dest, "instance1");
                Directory.CreateDirectory(inst1Dir);
                string inst1Bat = "@echo off\r\nstart \"\" \"%~dp0..\\vscode\\Code.exe\" --user-data-dir \"%~dp0user-data\" --extensions-dir \"%~dp0..\\vscode\\data\\extensions\" %*\r\n";
                File.WriteAllText(Path.Combine(inst1Dir, "launch.bat"), inst1Bat);

                string inst1Json = "{\r\n  \"name\": \"instance1\",\r\n  \"extensionMode\": \"shared\",\r\n  \"createdAt\": \"" + DateTime.UtcNow.ToString("o") + "\"\r\n}\r\n";
                File.WriteAllText(Path.Combine(inst1Dir, "instance.json"), inst1Json);

                // 4. Create README.md
                string readmeContent = "# VS Launcher (Portable Multi-Instance Visual Studio Code)\r\n\r\n"
                    + "Welcome to VS Launcher! To get started:\r\n"
                    + "1. Launch Launcher.exe\r\n"
                    + "2. Click 'Install VS Code' in the top-right header to automatically download and unpack the portable VS Code core.\r\n"
                    + "3. Create or launch instances with shared or isolated extensions.\r\n\r\n"
                    + "Select an instance and use Rename Selected to change its display name, or Change Extension Mode to switch between the main shared extensions and that instance's local extensions. Mode changes apply on the next launch; running VS Code windows must be restarted. Renaming does not move the instance folder or its existing shortcuts.\r\n";
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

                lblStatus.Text = "Installation complete!";
                progressBar.Visible = false;

                MessageBox.Show(
                    "VS Launcher has been successfully installed to:\n" + dest + "\n\nWhen VS Launcher opens, click '⬇ Install VS Code' in the top right to download and set up the portable core.",
                    "Installation Complete",
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
                MessageBox.Show("Failed to install: " + ex.Message, "Installation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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
