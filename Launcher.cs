using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace VSLauncher
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            // Force modern TLS 1.2 / TLS 1.3 for Microsoft update endpoints
            try
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | (SecurityProtocolType)12288 | SecurityProtocolType.Tls12;
                ServicePointManager.ServerCertificateValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;
            }
            catch { }

            // Ensure running as Administrator
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
                        "Administrator privileges are required to run VS Launcher.\nPlease approve the administrator prompt to continue.",
                        "Elevation Required",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                    return;
                }
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
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

    public class InstanceInfo
    {
        public string Name { get; set; }
        public string DirectoryPath { get; set; }
        public string ExtensionMode { get; set; } // "Shared", "Isolated (Empty)", "Isolated (Copy)"
        public string BatPath { get; set; }
        public string JsonPath { get; set; }
        public long UserDataSize { get; set; }
        public long ExtensionsSize { get; set; }
        public DateTime LastModified { get; set; }

        public InstanceInfo()
        {
            UserDataSize = -1;
            ExtensionsSize = -1;
        }
    }

    public class MainForm : Form
    {
        private string rootDir;
        private string vscodeDir;
        private string codeExePath;
        private string sharedExtDir;

        private string currentVersion = null;
        private string latestVersion = null;
        private string latestDownloadUrl = null;
        private string coreDiskSizeStr = "Calculating...";
        private int coreExtensionCount = 0;
        private bool isCheckingUpdate = false;
        private bool isBusy = false;

        // Header controls
        private Label lblTitle;
        private Label lblSubtitle;
        private Panel pnlHeaderLeft;
        private Panel pnlHeaderRight;
        private Label lblCoreTitle;
        private Label lblCoreDetails;
        private Button btnOpenMain;
        private Button btnUpdate;

        // Instance list controls
        private ListView lvInstances;
        private Button btnLaunch;
        private Button btnOpenFolder;
        private Button btnShortcut;
        private Button btnDelete;
        private Button btnRefresh;

        // Create Instance controls
        private TextBox txtNewName;
        private RadioButton rbExtShared;
        private RadioButton rbExtIsolatedEmpty;
        private RadioButton rbExtIsolatedCopy;
        private RadioButton rbProfileEmpty;
        private RadioButton rbProfileClone;
        private ComboBox cbCloneSource;
        private CheckBox chkLaunchNow;
        private CheckBox chkCreateShortcut;
        private Button btnCreate;

        // Status controls
        private StatusStrip statusStrip;
        private ToolStripStatusLabel lblStatus;
        private ToolStripProgressBar progressBar;
        private ToolStripStatusLabel lblRoot;

        private Icon appIcon;

        public MainForm()
        {
            rootDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
            vscodeDir = Path.Combine(rootDir, "vscode");
            codeExePath = Path.Combine(vscodeDir, "Code.exe");
            sharedExtDir = Path.Combine(vscodeDir, "data", "extensions");

            InitializeUI();
            LoadIcon();
            RefreshCoreHeaderStats();
            RefreshInstances();

            // Background tasks: calculate core disk size and check online version
            CalculateCoreStatsAsync();
            CheckOnlineVersion(false);
        }

        private void InitializeUI()
        {
            this.Text = "VS Launcher (Administrator)";
            this.Size = new Size(930, 550);
            this.MinimumSize = new Size(840, 480);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            this.BackColor = Color.FromArgb(246, 247, 249);

            // Header Panel (Black background)
            Panel pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 62,
                BackColor = Color.FromArgb(30, 30, 30),
                Padding = new Padding(14, 0, 14, 0)
            };

            // Header Left: App Title and Subtitle
            pnlHeaderLeft = new Panel
            {
                Dock = DockStyle.Left,
                Width = 240,
                BackColor = Color.FromArgb(30, 30, 30)
            };

            lblTitle = new Label
            {
                Text = "VS Launcher",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                Location = new Point(0, 9),
                AutoSize = true
            };

            lblSubtitle = new Label
            {
                Text = "Portable Multi-Instance Manager",
                ForeColor = Color.FromArgb(160, 160, 160),
                Font = new Font("Segoe UI", 8F),
                Location = new Point(2, 34),
                AutoSize = true
            };

            pnlHeaderLeft.Controls.Add(lblTitle);
            pnlHeaderLeft.Controls.Add(lblSubtitle);

            // Header Right: Floating summary details & actions
            pnlHeaderRight = new Panel
            {
                Dock = DockStyle.Right,
                Width = 630,
                BackColor = Color.FromArgb(30, 30, 30)
            };

            // Buttons pinned to the right
            btnUpdate = new Button
            {
                Text = "🔄 Update",
                ForeColor = Color.White,
                BackColor = Color.FromArgb(55, 55, 55),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(526, 15),
                Size = new Size(96, 30),
                Cursor = Cursors.Hand
            };
            btnUpdate.FlatAppearance.BorderSize = 0;
            btnUpdate.Click += (s, e) => HandleUpdateOrInstallClick();

            btnOpenMain = new Button
            {
                Text = "▶ Open Main",
                ForeColor = Color.White,
                BackColor = Color.FromArgb(0, 122, 204),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(426, 15),
                Size = new Size(94, 30),
                Cursor = Cursors.Hand
            };
            btnOpenMain.FlatAppearance.BorderSize = 0;
            btnOpenMain.Click += (s, e) => LaunchMainVSCode();

            // Floating right text summary
            lblCoreTitle = new Label
            {
                Text = "VS Code Core: Detecting...",
                ForeColor = Color.FromArgb(78, 201, 176),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Location = new Point(0, 9),
                Size = new Size(418, 20),
                TextAlign = ContentAlignment.MiddleRight
            };

            lblCoreDetails = new Label
            {
                Text = "Checking extensions and size...",
                ForeColor = Color.FromArgb(170, 170, 170), // Slightly grey text as summary
                Font = new Font("Segoe UI", 8.25F, FontStyle.Regular),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Location = new Point(0, 31),
                Size = new Size(418, 18),
                TextAlign = ContentAlignment.MiddleRight
            };

            pnlHeaderRight.Controls.Add(lblCoreTitle);
            pnlHeaderRight.Controls.Add(lblCoreDetails);
            pnlHeaderRight.Controls.Add(btnOpenMain);
            pnlHeaderRight.Controls.Add(btnUpdate);

            pnlHeader.Controls.Add(pnlHeaderLeft);
            pnlHeader.Controls.Add(pnlHeaderRight);
            this.Controls.Add(pnlHeader);

            // Status Strip
            statusStrip = new StatusStrip { BackColor = Color.FromArgb(238, 239, 242) };
            lblStatus = new ToolStripStatusLabel { Text = "Ready", Spring = false };
            progressBar = new ToolStripProgressBar { Visible = false, Width = 130, Height = 14 };
            lblRoot = new ToolStripStatusLabel { Text = rootDir, Spring = true, TextAlign = ContentAlignment.MiddleRight, ForeColor = Color.Gray };
            statusStrip.Items.AddRange(new ToolStripItem[] { lblStatus, progressBar, lblRoot });
            this.Controls.Add(statusStrip);

            // Main Content Area
            Panel pnlMain = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10, 10, 10, 6)
            };
            this.Controls.Add(pnlMain);
            pnlMain.BringToFront();

            // Right Panel (Compact New Instance)
            Panel pnlRight = new Panel
            {
                Dock = DockStyle.Right,
                Width = 295,
                Padding = new Padding(6, 0, 0, 0)
            };
            pnlMain.Controls.Add(pnlRight);

            GroupBox gbCreate = new GroupBox
            {
                Text = " New Instance ",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(30, 30, 30),
                Padding = new Padding(10, 8, 10, 8)
            };
            pnlRight.Controls.Add(gbCreate);

            Panel pnlCreateContent = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular)
            };
            gbCreate.Controls.Add(pnlCreateContent);

            int y = 4;
            Label lblNamePrompt = new Label
            {
                Text = "Instance Name:",
                Location = new Point(2, y),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
            };
            pnlCreateContent.Controls.Add(lblNamePrompt);
            y += 18;

            txtNewName = new TextBox
            {
                Location = new Point(2, y),
                Size = new Size(255, 23),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            pnlCreateContent.Controls.Add(txtNewName);
            y += 28;

            // Extension Isolation Options
            GroupBox gbExt = new GroupBox
            {
                Text = "Extension Mode",
                Location = new Point(2, y),
                Size = new Size(255, 112),
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 102, 204)
            };
            pnlCreateContent.Controls.Add(gbExt);

            rbExtShared = new RadioButton
            {
                Text = "Shared with Main (Zero space)",
                Location = new Point(8, 18),
                AutoSize = true,
                Checked = true,
                Font = new Font("Segoe UI", 8F, FontStyle.Regular),
                ForeColor = Color.FromArgb(30, 30, 30)
            };
            Label lblDescShared = new Label
            {
                Text = "Uses vscode\\data\\extensions.",
                Location = new Point(24, 34),
                AutoSize = true,
                ForeColor = Color.FromArgb(120, 120, 120),
                Font = new Font("Segoe UI", 7.5F)
            };

            rbExtIsolatedEmpty = new RadioButton
            {
                Text = "Isolated (Empty folder)",
                Location = new Point(8, 50),
                AutoSize = true,
                Font = new Font("Segoe UI", 8F, FontStyle.Regular),
                ForeColor = Color.FromArgb(30, 30, 30)
            };

            rbExtIsolatedCopy = new RadioButton
            {
                Text = "Isolated (Copy from Main)",
                Location = new Point(8, 72),
                AutoSize = true,
                Font = new Font("Segoe UI", 8F, FontStyle.Regular),
                ForeColor = Color.FromArgb(30, 30, 30)
            };
            Label lblDescCopy = new Label
            {
                Text = "Clones current main extensions.",
                Location = new Point(24, 88),
                AutoSize = true,
                ForeColor = Color.FromArgb(120, 120, 120),
                Font = new Font("Segoe UI", 7.5F)
            };

            gbExt.Controls.Add(rbExtShared);
            gbExt.Controls.Add(lblDescShared);
            gbExt.Controls.Add(rbExtIsolatedEmpty);
            gbExt.Controls.Add(rbExtIsolatedCopy);
            gbExt.Controls.Add(lblDescCopy);

            y += 118;

            // Profile group
            GroupBox gbProfile = new GroupBox
            {
                Text = "Profile Settings",
                Location = new Point(2, y),
                Size = new Size(255, 80),
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = Color.FromArgb(60, 60, 60)
            };
            pnlCreateContent.Controls.Add(gbProfile);

            rbProfileEmpty = new RadioButton
            {
                Text = "Fresh Profile (Empty settings)",
                Location = new Point(8, 16),
                AutoSize = true,
                Checked = true,
                Font = new Font("Segoe UI", 8F, FontStyle.Regular),
                ForeColor = Color.FromArgb(30, 30, 30)
            };

            rbProfileClone = new RadioButton
            {
                Text = "Clone from:",
                Location = new Point(8, 36),
                AutoSize = true,
                Font = new Font("Segoe UI", 8F, FontStyle.Regular),
                ForeColor = Color.FromArgb(30, 30, 30)
            };

            cbCloneSource = new ComboBox
            {
                Location = new Point(95, 34),
                Size = new Size(150, 21),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Enabled = false,
                Font = new Font("Segoe UI", 8F)
            };
            rbProfileClone.CheckedChanged += (s, e) => cbCloneSource.Enabled = rbProfileClone.Checked;

            gbProfile.Controls.Add(rbProfileEmpty);
            gbProfile.Controls.Add(rbProfileClone);
            gbProfile.Controls.Add(cbCloneSource);

            y += 86;

            chkLaunchNow = new CheckBox
            {
                Text = "Launch on creation",
                Checked = true,
                Location = new Point(4, y),
                AutoSize = true
            };
            pnlCreateContent.Controls.Add(chkLaunchNow);
            y += 20;

            chkCreateShortcut = new CheckBox
            {
                Text = "Create Desktop shortcut",
                Checked = false,
                Location = new Point(4, y),
                AutoSize = true
            };
            pnlCreateContent.Controls.Add(chkCreateShortcut);
            y += 26;

            btnCreate = new Button
            {
                Text = "➕ Create Instance",
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Location = new Point(2, y),
                Size = new Size(255, 32),
                Cursor = Cursors.Hand
            };
            btnCreate.FlatAppearance.BorderSize = 0;
            btnCreate.Click += (s, e) => CreateInstance();
            pnlCreateContent.Controls.Add(btnCreate);

            // Left Panel (Instances List & Management)
            Panel pnlLeft = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 0, 6, 0)
            };
            pnlMain.Controls.Add(pnlLeft);

            GroupBox gbInstances = new GroupBox
            {
                Text = " Instances ",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(30, 30, 30),
                Padding = new Padding(8, 6, 8, 8)
            };
            pnlLeft.Controls.Add(gbInstances);

            // Action buttons row below list
            Panel pnlListActions = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 36,
                Padding = new Padding(0, 4, 0, 0)
            };
            gbInstances.Controls.Add(pnlListActions);

            btnLaunch = new Button
            {
                Text = "▶ Launch",
                BackColor = Color.FromArgb(46, 125, 50),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Size = new Size(95, 30),
                Location = new Point(0, 4),
                Cursor = Cursors.Hand
            };
            btnLaunch.FlatAppearance.BorderSize = 0;
            btnLaunch.Click += (s, e) => LaunchSelectedInstance();

            btnOpenFolder = new Button
            {
                Text = "📁 Folder",
                Size = new Size(80, 30),
                Location = new Point(100, 4),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand
            };
            btnOpenFolder.Click += (s, e) => OpenSelectedFolder();

            btnShortcut = new Button
            {
                Text = "🔗 Shortcut",
                Size = new Size(90, 30),
                Location = new Point(185, 4),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand
            };
            btnShortcut.Click += (s, e) => CreateSelectedShortcut();

            btnDelete = new Button
            {
                Text = "🗑️ Delete",
                Size = new Size(75, 30),
                Location = new Point(280, 4),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand
            };
            btnDelete.Click += (s, e) => DeleteSelectedInstance();

            btnRefresh = new Button
            {
                Text = "↻ Refresh",
                Size = new Size(75, 30),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(480, 4),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand
            };
            btnRefresh.Click += (s, e) =>
            {
                CalculateCoreStatsAsync();
                RefreshInstances();
                CheckOnlineVersion(true);
            };

            pnlListActions.Controls.AddRange(new Control[] { btnLaunch, btnOpenFolder, btnShortcut, btnDelete, btnRefresh });

            // Compact ListView
            lvInstances = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = false,
                HideSelection = false,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular)
            };
            lvInstances.Columns.Add("Name", 125);
            lvInstances.Columns.Add("Extension Mode", 130);
            lvInstances.Columns.Add("User Data", 80);
            lvInstances.Columns.Add("Ext Size", 75);
            lvInstances.Columns.Add("Last Modified", 115);

            lvInstances.DoubleClick += (s, e) => LaunchSelectedInstance();
            lvInstances.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) { LaunchSelectedInstance(); e.Handled = true; }
                else if (e.KeyCode == Keys.F5) { RefreshInstances(); e.Handled = true; }
                else if (e.KeyCode == Keys.Delete) { DeleteSelectedInstance(); e.Handled = true; }
            };

            gbInstances.Controls.Add(lvInstances);
            lvInstances.BringToFront();
        }

        private void LoadIcon()
        {
            try
            {
                if (File.Exists(Path.Combine(rootDir, "app.ico")))
                {
                    appIcon = new Icon(Path.Combine(rootDir, "app.ico"));
                    this.Icon = appIcon;
                }
                else if (File.Exists(codeExePath))
                {
                    appIcon = Icon.ExtractAssociatedIcon(codeExePath);
                    this.Icon = appIcon;
                }
            }
            catch { }
        }

        private void CalculateCoreStatsAsync()
        {
            Task.Run(() =>
            {
                if (File.Exists(codeExePath))
                {
                    // Count extensions
                    int count = 0;
                    if (Directory.Exists(sharedExtDir))
                    {
                        try
                        {
                            count = Directory.GetDirectories(sharedExtDir).Length;
                        }
                        catch { }
                    }
                    coreExtensionCount = count;

                    // Calculate total size of vscode folder
                    long totalBytes = 0;
                    try
                    {
                        foreach (string file in Directory.EnumerateFiles(vscodeDir, "*.*", SearchOption.AllDirectories))
                        {
                            try { totalBytes += new FileInfo(file).Length; } catch { }
                        }
                    }
                    catch { }

                    coreDiskSizeStr = FormatBytes(totalBytes);
                }
                else
                {
                    coreExtensionCount = 0;
                    coreDiskSizeStr = "0 B";
                }

                if (!this.IsDisposed && this.IsHandleCreated)
                {
                    this.BeginInvoke((MethodInvoker)delegate
                    {
                        RefreshCoreHeaderStats();
                    });
                }
            });
        }

        public void RefreshCoreHeaderStats()
        {
            if (File.Exists(codeExePath))
            {
                try
                {
                    FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(codeExePath);
                    currentVersion = fvi.ProductVersion ?? fvi.FileVersion ?? "Installed";
                }
                catch
                {
                    currentVersion = "Installed";
                }

                btnOpenMain.Visible = true;
                btnOpenMain.Enabled = true;
                btnOpenMain.Location = new Point(426, 15);
                btnUpdate.Location = new Point(526, 15);
                btnUpdate.Size = new Size(96, 30);
                lblCoreTitle.Size = new Size(418, 20);
                lblCoreDetails.Size = new Size(418, 18);

                // Update status indicator
                string statusText = "";
                Color titleColor = Color.FromArgb(78, 201, 176);

                if (!string.IsNullOrEmpty(latestVersion))
                {
                    if (currentVersion.Equals(latestVersion, StringComparison.OrdinalIgnoreCase))
                    {
                        statusText = "● Up to date";
                        titleColor = Color.FromArgb(78, 201, 176);
                        btnUpdate.Text = "🔄 Update";
                        btnUpdate.BackColor = Color.FromArgb(55, 55, 55);
                    }
                    else
                    {
                        statusText = string.Format("● Update Available: v{0}", latestVersion);
                        titleColor = Color.FromArgb(255, 215, 0); // Gold
                        btnUpdate.Text = "🔄 Update Now";
                        btnUpdate.BackColor = Color.FromArgb(0, 122, 204);
                    }
                }
                else
                {
                    statusText = isCheckingUpdate ? "● Checking update..." : "● Installed";
                    titleColor = Color.FromArgb(78, 201, 176);
                    btnUpdate.Text = "🔄 Update";
                    btnUpdate.BackColor = Color.FromArgb(55, 55, 55);
                }

                lblCoreTitle.Text = string.Format("VS Code v{0}  {1}", currentVersion, statusText);
                lblCoreTitle.ForeColor = titleColor;

                // Slightly grey text as summary: extensions count & size from hard drive
                lblCoreDetails.Text = string.Format("Extensions: {0} installed  •  Size: {1}", coreExtensionCount, coreDiskSizeStr);
                lblCoreDetails.ForeColor = Color.FromArgb(175, 175, 175);
            }
            else
            {
                currentVersion = null;
                btnOpenMain.Visible = false;

                lblCoreTitle.Text = "VS Code Core: Not Installed";
                lblCoreTitle.ForeColor = Color.FromArgb(240, 160, 40);

                lblCoreDetails.Text = "Portable installation missing in vscode\\ folder";
                lblCoreDetails.ForeColor = Color.FromArgb(170, 170, 170);

                // Adjust Install button position & styling
                btnUpdate.Text = "⬇ Install VS Code";
                btnUpdate.BackColor = Color.FromArgb(46, 125, 50); // Green
                btnUpdate.Size = new Size(136, 30);
                btnUpdate.Location = new Point(486, 15);
                lblCoreTitle.Size = new Size(478, 20);
                lblCoreDetails.Size = new Size(478, 18);
            }
        }

        private void CheckOnlineVersion(bool showPromptIfLatest)
        {
            if (isCheckingUpdate || isBusy) return;
            isCheckingUpdate = true;
            RefreshCoreHeaderStats();

            Task.Run(() =>
            {
                try
                {
                    ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | (SecurityProtocolType)12288 | SecurityProtocolType.Tls12;
                    string updateUrl = "https://update.code.visualstudio.com/latest/win32-x64-archive/stable";
                    HttpWebRequest req = (HttpWebRequest)WebRequest.Create(updateUrl);
                    req.Method = "HEAD";
                    req.AllowAutoRedirect = false;
                    req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) VS-Launcher";
                    req.Timeout = 10000;

                    string loc = "";
                    using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                    {
                        loc = resp.GetResponseHeader("Location");
                    }

                    if (!string.IsNullOrEmpty(loc))
                    {
                        latestDownloadUrl = loc;
                        Match m = Regex.Match(loc, @"VSCode-win32-x64-([0-9.]+)\.zip", RegexOptions.IgnoreCase);
                        if (m.Success)
                        {
                            latestVersion = m.Groups[1].Value;
                        }
                    }

                    this.Invoke((MethodInvoker)delegate
                    {
                        isCheckingUpdate = false;
                        RefreshCoreHeaderStats();

                        if (showPromptIfLatest && File.Exists(codeExePath))
                        {
                            if (currentVersion != null && currentVersion.Equals(latestVersion, StringComparison.OrdinalIgnoreCase))
                            {
                                MessageBox.Show(string.Format("Your portable VS Code core is already up-to-date (v{0}).", currentVersion), "Up to Date", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                        }
                    });
                }
                catch (Exception ex)
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        isCheckingUpdate = false;
                        RefreshCoreHeaderStats();
                        if (showPromptIfLatest)
                        {
                            MessageBox.Show("Could not check for online updates: " + ex.Message, "Check Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    });
                }
            });
        }

        public void RefreshInstances()
        {
            lvInstances.Items.Clear();
            cbCloneSource.Items.Clear();

            if (!Directory.Exists(rootDir)) return;

            string[] dirs = Directory.GetDirectories(rootDir);
            int count = 0;
            int nextIndex = 1;

            List<InstanceInfo> instances = new List<InstanceInfo>();

            foreach (string dir in dirs)
            {
                string dirName = Path.GetFileName(dir);
                string lower = dirName.ToLowerInvariant();
                if (lower == "vscode" || lower == "_template" || lower.StartsWith(".") || lower == "$recycle.bin" || lower == "vscode_update_temp")
                    continue;

                string batPath = Path.Combine(dir, "launch.bat");
                string jsonPath = Path.Combine(dir, "instance.json");

                bool hasBat = File.Exists(batPath);
                bool hasJson = File.Exists(jsonPath);

                if (!hasBat && !hasJson) continue;

                count++;
                if (lower.StartsWith("instance"))
                {
                    int idx;
                    if (int.TryParse(lower.Substring(8), out idx) && idx >= nextIndex)
                        nextIndex = idx + 1;
                }

                // Detect extension mode
                string extMode = "Shared";
                if (hasJson)
                {
                    try
                    {
                        string json = File.ReadAllText(jsonPath);
                        if (json.Contains("\"isolated-copy\"")) extMode = "Isolated (Copy)";
                        else if (json.Contains("\"isolated-empty\"") || json.Contains("\"isolated\"")) extMode = "Isolated (Empty)";
                        else if (json.Contains("\"shared\"")) extMode = "Shared";
                    }
                    catch { }
                }
                else if (hasBat)
                {
                    string batContent = File.ReadAllText(batPath);
                    if (batContent.IndexOf(@"vscode\data\extensions", StringComparison.OrdinalIgnoreCase) >= 0)
                        extMode = "Shared";
                    else
                        extMode = "Isolated (Empty)";

                    try
                    {
                        string jsonMode = (extMode == "Shared") ? "shared" : "isolated-empty";
                        string genJson = string.Format("{{\r\n  \"name\": \"{0}\",\r\n  \"extensionMode\": \"{1}\",\r\n  \"createdAt\": \"{2}\"\r\n}}\r\n",
                            dirName, jsonMode, DateTime.UtcNow.ToString("o"));
                        File.WriteAllText(jsonPath, genJson);
                        hasJson = true;
                    }
                    catch { }
                }

                if (!hasBat)
                {
                    try
                    {
                        string batContent = (extMode == "Shared")
                            ? "@echo off\r\nstart \"\" \"%~dp0..\\vscode\\Code.exe\" --user-data-dir \"%~dp0user-data\" --extensions-dir \"%~dp0..\\vscode\\data\\extensions\" %*\r\n"
                            : "@echo off\r\nstart \"\" \"%~dp0..\\vscode\\Code.exe\" --user-data-dir \"%~dp0user-data\" --extensions-dir \"%~dp0extensions\" %*\r\n";
                        File.WriteAllText(batPath, batContent);
                        hasBat = true;
                    }
                    catch { }
                }

                DateTime lastMod = Directory.GetLastWriteTime(dir);

                InstanceInfo info = new InstanceInfo
                {
                    Name = dirName,
                    DirectoryPath = dir,
                    ExtensionMode = extMode,
                    BatPath = batPath,
                    JsonPath = jsonPath,
                    LastModified = lastMod
                };
                instances.Add(info);

                ListViewItem item = new ListViewItem(info.Name);
                item.SubItems.Add(info.ExtensionMode);
                item.SubItems.Add("...");
                item.SubItems.Add("...");
                item.SubItems.Add(info.LastModified.ToString("yyyy-MM-dd HH:mm"));
                item.Tag = info;

                lvInstances.Items.Add(item);
                cbCloneSource.Items.Add(info.Name);
            }

            if (cbCloneSource.Items.Count > 0)
                cbCloneSource.SelectedIndex = 0;

            txtNewName.Text = "instance" + nextIndex;
            lblStatus.Text = string.Format("{0} instance(s) found.", count);

            Task.Run(() => CalculateSizes(instances));
        }

        private void CalculateSizes(List<InstanceInfo> instances)
        {
            foreach (var inst in instances)
            {
                long udSize = 0;
                string udDir = Path.Combine(inst.DirectoryPath, "user-data");
                if (Directory.Exists(udDir))
                    udSize = GetDirectorySize(udDir);

                long extSize = 0;
                string localExtDir = Path.Combine(inst.DirectoryPath, "extensions");
                if (Directory.Exists(localExtDir))
                    extSize = GetDirectorySize(localExtDir);
                else if (inst.ExtensionMode.StartsWith("Shared"))
                {
                    extSize = -2;
                }

                if (!this.IsDisposed && this.IsHandleCreated)
                {
                    this.BeginInvoke((MethodInvoker)delegate
                    {
                        foreach (ListViewItem item in lvInstances.Items)
                        {
                            if (item.Tag == inst)
                            {
                                item.SubItems[2].Text = FormatBytes(udSize);
                                item.SubItems[3].Text = (extSize == -2) ? "Shared" : FormatBytes(extSize);
                                break;
                            }
                        }
                    });
                }
            }
        }

        private long GetDirectorySize(string path)
        {
            long size = 0;
            try
            {
                var files = Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories);
                foreach (var f in files)
                {
                    try { size += new FileInfo(f).Length; } catch { }
                }
            }
            catch { }
            return size;
        }

        private string FormatBytes(long bytes)
        {
            if (bytes <= 0) return "0 B";
            string[] suffixes = { "B", "KB", "MB", "GB" };
            int i = 0;
            double d = bytes;
            while (d >= 1024 && i < suffixes.Length - 1)
            {
                d /= 1024;
                i++;
            }
            return string.Format("{0:0.#} {1}", d, suffixes[i]);
        }

        private void LaunchMainVSCode()
        {
            if (!File.Exists(codeExePath))
            {
                DialogResult dr = MessageBox.Show("VS Code core not found. Would you like to download and install it now?", "VS Code Missing", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (dr == DialogResult.Yes)
                {
                    DownloadAndSetupVSCode();
                }
                return;
            }

            try
            {
                string mainUserData = Path.Combine(vscodeDir, "data", "user-data");
                string mainExt = Path.Combine(vscodeDir, "data", "extensions");
                Directory.CreateDirectory(mainUserData);
                Directory.CreateDirectory(mainExt);

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = codeExePath,
                    Arguments = string.Format("--user-data-dir \"{0}\" --extensions-dir \"{1}\"", mainUserData, mainExt),
                    WorkingDirectory = vscodeDir,
                    UseShellExecute = true
                };
                Process.Start(psi);
                lblStatus.Text = "Main VS Code launched.";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to launch main VS Code: " + ex.Message, "Launch Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LaunchSelectedInstance()
        {
            if (lvInstances.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select an instance to launch.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!File.Exists(codeExePath))
            {
                DialogResult dr = MessageBox.Show("VS Code core executable not found in vscode\\Code.exe.\n\nWould you like to download and setup core VS Code now?", "Core Missing", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (dr == DialogResult.Yes)
                {
                    DownloadAndSetupVSCode();
                }
                return;
            }

            InstanceInfo info = lvInstances.SelectedItems[0].Tag as InstanceInfo;
            if (info == null || !File.Exists(info.BatPath))
            {
                MessageBox.Show("Launcher script not found for this instance.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = info.BatPath,
                    WorkingDirectory = info.DirectoryPath,
                    UseShellExecute = true
                };
                Process.Start(psi);
                lblStatus.Text = string.Format("Launched: {0}", info.Name);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to launch instance: " + ex.Message, "Launch Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OpenSelectedFolder()
        {
            if (lvInstances.SelectedItems.Count == 0) return;
            InstanceInfo info = lvInstances.SelectedItems[0].Tag as InstanceInfo;
            if (info != null && Directory.Exists(info.DirectoryPath))
            {
                Process.Start("explorer.exe", info.DirectoryPath);
            }
        }

        private void CreateSelectedShortcut()
        {
            if (lvInstances.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select an instance to create a shortcut.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            InstanceInfo info = lvInstances.SelectedItems[0].Tag as InstanceInfo;
            if (info != null)
            {
                CreateShortcutForInstance(info.Name, info.BatPath, info.DirectoryPath);
                MessageBox.Show(string.Format("Shortcut created on Desktop for '{0}'!", info.Name), "Shortcut Created", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void DeleteSelectedInstance()
        {
            if (lvInstances.SelectedItems.Count == 0) return;
            InstanceInfo info = lvInstances.SelectedItems[0].Tag as InstanceInfo;
            if (info == null) return;

            DialogResult res = MessageBox.Show(
                string.Format("Are you sure you want to permanently delete instance '{0}' and its user data?\n\nFolder: {1}", info.Name, info.DirectoryPath),
                "Confirm Delete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2
            );

            if (res == DialogResult.Yes)
            {
                try
                {
                    Directory.Delete(info.DirectoryPath, true);
                    lblStatus.Text = string.Format("Deleted instance '{0}'.", info.Name);
                    RefreshInstances();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to delete instance: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void CreateInstance()
        {
            string name = txtNewName.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Please enter an instance name.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtNewName.Focus();
                return;
            }

            char[] invalid = Path.GetInvalidFileNameChars();
            if (name.IndexOfAny(invalid) >= 0)
            {
                MessageBox.Show("Instance name contains invalid characters.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string targetDir = Path.Combine(rootDir, name);
            if (Directory.Exists(targetDir))
            {
                MessageBox.Show(string.Format("Folder '{0}' already exists.", name), "Already Exists", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnCreate.Enabled = false;
            lblStatus.Text = string.Format("Creating instance '{0}'...", name);

            string extMode = "shared";
            if (rbExtIsolatedEmpty.Checked) extMode = "isolated-empty";
            else if (rbExtIsolatedCopy.Checked) extMode = "isolated-copy";

            bool cloneProfile = rbProfileClone.Checked;
            string cloneSource = cbCloneSource.SelectedItem as string;

            bool launchNow = chkLaunchNow.Checked;
            bool makeShortcut = chkCreateShortcut.Checked;

            Task.Run(() =>
            {
                try
                {
                    Directory.CreateDirectory(targetDir);
                    string userDataDir = Path.Combine(targetDir, "user-data");
                    Directory.CreateDirectory(userDataDir);

                    // Extension setup
                    string batContent = "";
                    if (extMode == "shared")
                    {
                        batContent = "@echo off\r\nstart \"\" \"%~dp0..\\vscode\\Code.exe\" --user-data-dir \"%~dp0user-data\" --extensions-dir \"%~dp0..\\vscode\\data\\extensions\" %*\r\n";
                    }
                    else
                    {
                        string instanceExtDir = Path.Combine(targetDir, "extensions");
                        Directory.CreateDirectory(instanceExtDir);

                        if (extMode == "isolated-copy" && Directory.Exists(sharedExtDir))
                        {
                            CopyDirectoryRecursive(sharedExtDir, instanceExtDir);
                        }

                        batContent = "@echo off\r\nstart \"\" \"%~dp0..\\vscode\\Code.exe\" --user-data-dir \"%~dp0user-data\" --extensions-dir \"%~dp0extensions\" %*\r\n";
                    }

                    string batPath = Path.Combine(targetDir, "launch.bat");
                    File.WriteAllText(batPath, batContent);

                    // Metadata JSON
                    string json = string.Format("{{\r\n  \"name\": \"{0}\",\r\n  \"extensionMode\": \"{1}\",\r\n  \"createdAt\": \"{2}\"\r\n}}\r\n",
                        name, extMode, DateTime.UtcNow.ToString("o"));
                    File.WriteAllText(Path.Combine(targetDir, "instance.json"), json);

                    // Profile cloning
                    if (cloneProfile && !string.IsNullOrEmpty(cloneSource))
                    {
                        string srcUserData = Path.Combine(rootDir, cloneSource, "user-data");
                        if (Directory.Exists(srcUserData))
                        {
                            CopyDirectoryRecursive(srcUserData, userDataDir);
                        }
                    }

                    if (makeShortcut)
                    {
                        CreateShortcutForInstance(name, batPath, targetDir);
                    }

                    this.Invoke((MethodInvoker)delegate
                    {
                        lblStatus.Text = string.Format("Instance '{0}' created.", name);
                        btnCreate.Enabled = true;
                        RefreshInstances();

                        foreach (ListViewItem item in lvInstances.Items)
                        {
                            if (item.Text.Equals(name, StringComparison.OrdinalIgnoreCase))
                            {
                                item.Selected = true;
                                item.EnsureVisible();
                                break;
                            }
                        }

                        if (launchNow)
                        {
                            if (File.Exists(codeExePath))
                            {
                                Process.Start(new ProcessStartInfo
                                {
                                    FileName = batPath,
                                    WorkingDirectory = targetDir,
                                    UseShellExecute = true
                                });
                            }
                            else
                            {
                                MessageBox.Show("Instance created! However, VS Code core is not yet downloaded. Click 'Install VS Code' in the top bar.", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                        }
                    });
                }
                catch (Exception ex)
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        btnCreate.Enabled = true;
                        MessageBox.Show("Failed to create instance: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        lblStatus.Text = "Error creating instance.";
                    });
                }
            });
        }

        private void CopyDirectoryRecursive(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string dest = Path.Combine(targetDir, Path.GetFileName(file));
                File.Copy(file, dest, true);
            }
            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                string dest = Path.Combine(targetDir, Path.GetFileName(subDir));
                CopyDirectoryRecursive(subDir, dest);
            }
        }

        private void CreateShortcutForInstance(string instanceName, string batPath, string workingDir)
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return;
                dynamic shell = Activator.CreateInstance(shellType);
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string shortcutPath = Path.Combine(desktop, string.Format("{0} (VS Code).lnk", instanceName));
                dynamic shortcut = shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = batPath;
                shortcut.WorkingDirectory = workingDir;
                if (File.Exists(codeExePath))
                {
                    shortcut.IconLocation = codeExePath + ",0";
                }
                shortcut.WindowStyle = 7;
                shortcut.Save();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Shortcut creation error: " + ex.Message);
            }
        }

        private void HandleUpdateOrInstallClick()
        {
            if (isBusy) return;

            if (!File.Exists(codeExePath))
            {
                DownloadAndSetupVSCode();
            }
            else
            {
                if (!string.IsNullOrEmpty(latestDownloadUrl) && !string.IsNullOrEmpty(latestVersion))
                {
                    PromptAndPerformUpdate(latestDownloadUrl, latestVersion);
                }
                else
                {
                    btnUpdate.Enabled = false;
                    lblStatus.Text = "Checking latest version...";
                    Task.Run(() =>
                    {
                        try
                        {
                            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | (SecurityProtocolType)12288 | SecurityProtocolType.Tls12;
                            string updateUrl = "https://update.code.visualstudio.com/latest/win32-x64-archive/stable";
                            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(updateUrl);
                            req.Method = "HEAD";
                            req.AllowAutoRedirect = false;
                            req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) VS-Launcher";
                            req.Timeout = 10000;

                            string loc = "";
                            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                            {
                                loc = resp.GetResponseHeader("Location");
                            }

                            string remoteVer = "Latest";
                            Match m = Regex.Match(loc, @"VSCode-win32-x64-([0-9.]+)\.zip", RegexOptions.IgnoreCase);
                            if (m.Success) remoteVer = m.Groups[1].Value;

                            this.Invoke((MethodInvoker)delegate
                            {
                                btnUpdate.Enabled = true;
                                latestDownloadUrl = loc;
                                latestVersion = remoteVer;
                                PromptAndPerformUpdate(loc, remoteVer);
                            });
                        }
                        catch (Exception ex)
                        {
                            this.Invoke((MethodInvoker)delegate
                            {
                                btnUpdate.Enabled = true;
                                MessageBox.Show("Check update failed: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            });
                        }
                    });
                }
            }
        }

        private void PromptAndPerformUpdate(string downloadUrl, string remoteVer)
        {
            string prompt = "";
            if (currentVersion != null && currentVersion.Equals(remoteVer, StringComparison.OrdinalIgnoreCase))
            {
                prompt = string.Format("Your VS Code core is already up-to-date (v{0}).\n\nDo you want to re-download and reinstall anyway?", currentVersion);
            }
            else
            {
                prompt = string.Format("New version available!\n\nCurrent: v{0}\nLatest: v{1}\n\nDo you want to update now?", currentVersion ?? "None", remoteVer);
            }

            DialogResult dr = MessageBox.Show(prompt, "VS Code Update", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (dr == DialogResult.Yes)
            {
                ExecuteDownloadAndExtract(downloadUrl, remoteVer, isFreshInstall: false);
            }
        }

        private void DownloadAndSetupVSCode()
        {
            DialogResult dr = MessageBox.Show(
                "VS Code core was not found in 'vscode\\'.\n\nWould you like VS Launcher to download the official Windows x64 portable archive and set it up automatically?",
                "Install VS Code Core",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (dr != DialogResult.Yes) return;

            string targetUrl = !string.IsNullOrEmpty(latestDownloadUrl)
                ? latestDownloadUrl
                : "https://update.code.visualstudio.com/latest/win32-x64-archive/stable";

            ExecuteDownloadAndExtract(targetUrl, latestVersion ?? "Latest", isFreshInstall: true);
        }

        private void ExecuteDownloadAndExtract(string downloadUrl, string verString, bool isFreshInstall)
        {
            Process[] procs = Process.GetProcessesByName("Code");
            if (procs.Length > 0)
            {
                DialogResult dr = MessageBox.Show(
                    "VS Code is currently running.\n\nPlease close all open VS Code instances before continuing.\n\nClick OK once closed, or Cancel to abort.",
                    "VS Code Running",
                    MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Warning
                );
                if (dr != DialogResult.OK)
                {
                    lblStatus.Text = "Cancelled.";
                    return;
                }
            }

            isBusy = true;
            btnUpdate.Enabled = false;
            btnOpenMain.Enabled = false;
            progressBar.Visible = true;
            progressBar.Value = 0;
            lblStatus.Text = (isFreshInstall ? "Downloading VS Code Core..." : "Downloading update...");

            string tempZip = Path.Combine(rootDir, "vscode_portable_temp.zip");
            string tempExtract = Path.Combine(rootDir, "vscode_update_temp");

            WebClient client = new WebClient();
            client.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) VS-Launcher");

            client.DownloadProgressChanged += (s, e) =>
            {
                this.BeginInvoke((MethodInvoker)delegate
                {
                    progressBar.Value = Math.Min(100, e.ProgressPercentage);
                    lblStatus.Text = string.Format("Downloading ({0}%): {1} / {2}",
                        e.ProgressPercentage, FormatBytes(e.BytesReceived), FormatBytes(e.TotalBytesToReceive));
                });
            };

            client.DownloadFileCompleted += (s, e) =>
            {
                if (e.Error != null)
                {
                    this.BeginInvoke((MethodInvoker)delegate
                    {
                        isBusy = false;
                        progressBar.Visible = false;
                        CalculateCoreStatsAsync();
                        lblStatus.Text = "Download failed: " + e.Error.Message;
                        MessageBox.Show("Download failed: " + e.Error.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    });
                    return;
                }

                Task.Run(() =>
                {
                    try
                    {
                        this.Invoke((MethodInvoker)delegate { lblStatus.Text = "Extracting VS Code archive..."; });

                        if (Directory.Exists(tempExtract)) Directory.Delete(tempExtract, true);
                        Directory.CreateDirectory(tempExtract);

                        bool extracted = false;
                        try
                        {
                            ProcessStartInfo psi = new ProcessStartInfo
                            {
                                FileName = "tar.exe",
                                Arguments = string.Format("-xf \"{0}\" -C \"{1}\"", tempZip, tempExtract),
                                CreateNoWindow = true,
                                UseShellExecute = false
                            };
                            using (Process p = Process.Start(psi))
                            {
                                p.WaitForExit();
                                if (p.ExitCode == 0) extracted = true;
                            }
                        }
                        catch { }

                        if (!extracted)
                        {
                            ZipFile.ExtractToDirectory(tempZip, tempExtract);
                        }

                        this.Invoke((MethodInvoker)delegate { lblStatus.Text = "Configuring portable directories..."; });

                        Directory.CreateDirectory(vscodeDir);

                        CopyUpdateFilesSafe(tempExtract, vscodeDir);

                        Directory.CreateDirectory(Path.Combine(vscodeDir, "data", "extensions"));
                        Directory.CreateDirectory(Path.Combine(vscodeDir, "data", "user-data"));

                        try { File.Delete(tempZip); } catch { }
                        try { Directory.Delete(tempExtract, true); } catch { }

                        this.Invoke((MethodInvoker)delegate
                        {
                            isBusy = false;
                            progressBar.Visible = false;
                            LoadIcon();
                            CalculateCoreStatsAsync();
                            RefreshInstances();
                            string msg = isFreshInstall
                                ? "VS Code core successfully installed and configured in portable mode!"
                                : string.Format("VS Code core successfully updated to v{0}!\nAll extensions and instance data preserved.", verString);
                            lblStatus.Text = isFreshInstall ? "Core installed successfully." : "Update completed.";
                            MessageBox.Show(msg, "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        });
                    }
                    catch (Exception ex)
                    {
                        this.Invoke((MethodInvoker)delegate
                        {
                            isBusy = false;
                            progressBar.Visible = false;
                            CalculateCoreStatsAsync();
                            lblStatus.Text = "Extraction error: " + ex.Message;
                            MessageBox.Show("Failed to complete setup: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        });
                    }
                });
            };

            try
            {
                client.DownloadFileAsync(new Uri(downloadUrl), tempZip);
            }
            catch (Exception ex)
            {
                isBusy = false;
                progressBar.Visible = false;
                CalculateCoreStatsAsync();
                MessageBox.Show("Download start error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CopyUpdateFilesSafe(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);

            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string fileName = Path.GetFileName(file);
                string destFile = Path.Combine(targetDir, fileName);
                File.Copy(file, destFile, true);
            }

            foreach (string dir in Directory.GetDirectories(sourceDir))
            {
                string dirName = Path.GetFileName(dir);
                if (dirName.Equals("data", StringComparison.OrdinalIgnoreCase)) continue;

                string destDir = Path.Combine(targetDir, dirName);
                CopyUpdateFilesSafe(dir, destDir);
            }
        }
    }
}
