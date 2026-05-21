using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ImageRenamer
{
    public class VersionConfig
    {
        public string latest_version { get; set; }
        public string minimum_supported_version { get; set; }
        public string release_date { get; set; }
        public string download_url { get; set; }
        public string[] changelog { get; set; }
        public string announcement { get; set; }
        public string announcement_level { get; set; }
    }

    public class SemanticVersion : IComparable<SemanticVersion>
    {
        public int Major { get; private set; }
        public int Minor { get; private set; }
        public int Patch { get; private set; }

        public SemanticVersion(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                Major = 0;
                Minor = 0;
                Patch = 0;
                return;
            }

            string cleanVersion = version.Trim().TrimStart('v', 'V');
            string[] parts = cleanVersion.Split('.');
            
            int major, minor, patch;
            Major = parts.Length > 0 && int.TryParse(parts[0], out major) ? major : 0;
            Minor = parts.Length > 1 && int.TryParse(parts[1], out minor) ? minor : 0;
            Patch = parts.Length > 2 && int.TryParse(parts[2], out patch) ? patch : 0;
        }

        public int CompareTo(SemanticVersion other)
        {
            if (other == null) return 1;
            
            if (Major != other.Major)
                return Major.CompareTo(other.Major);
            if (Minor != other.Minor)
                return Minor.CompareTo(other.Minor);
            return Patch.CompareTo(other.Patch);
        }

        public static bool operator >(SemanticVersion a, SemanticVersion b)
        {
            return a.CompareTo(b) > 0;
        }

        public static bool operator <(SemanticVersion a, SemanticVersion b)
        {
            return a.CompareTo(b) < 0;
        }

        public static bool operator >=(SemanticVersion a, SemanticVersion b)
        {
            return a.CompareTo(b) >= 0;
        }

        public static bool operator <=(SemanticVersion a, SemanticVersion b)
        {
            return a.CompareTo(b) <= 0;
        }

        public override string ToString()
        {
            return string.Format("{0}.{1}.{2}", Major, Minor, Patch);
        }
    }

    public static class UpdateService
    {
        private const string VersionUrl = "https://raw.githubusercontent.com/cheng1260/Pic-Batch-Auto-Renamer-v1.0.1/main/version.json";
        private const int TimeoutSeconds = 15;
        private const int MaxRetryCount = 2;

        private static HttpClient CreateHttpClient()
        {
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
            
            HttpClient client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(TimeoutSeconds);
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Add("Accept", "application/json; charset=utf-8");
            return client;
        }

        private static string JsonGetString(string json, string key)
        {
            string search = "\"" + key + "\"";
            int idx = json.IndexOf(search, StringComparison.Ordinal);
            if (idx < 0) return null;
            idx = json.IndexOf(':', idx + search.Length);
            if (idx < 0) return null;
            idx++;
            while (idx < json.Length && (json[idx] == ' ' || json[idx] == '\t' || json[idx] == '\n' || json[idx] == '\r'))
                idx++;
            if (idx >= json.Length) return null;
            if (json[idx] == '"')
            {
                int start = idx + 1;
                int end = start;
                while (end < json.Length)
                {
                    end = json.IndexOf('"', end);
                    if (end < 0) return null;
                    if (end > start && json[end - 1] == '\\')
                    {
                        end++;
                        continue;
                    }
                    break;
                }
                if (end < 0) return null;
                return json.Substring(start, end - start)
                    .Replace("\\\"", "\"")
                    .Replace("\\\\", "\\")
                    .Replace("\\n", "\n")
                    .Replace("\\r", "\r")
                    .Replace("\\t", "\t");
            }
            return null;
        }

        private static string[] JsonGetStringArray(string json, string key)
        {
            string search = "\"" + key + "\"";
            int idx = json.IndexOf(search, StringComparison.Ordinal);
            if (idx < 0) return null;
            idx = json.IndexOf('[', idx + search.Length);
            if (idx < 0) return null;
            int end = json.IndexOf(']', idx);
            if (end < 0) return null;
            string arrayContent = json.Substring(idx + 1, end - idx - 1);
            var items = new List<string>();
            int pos = 0;
            while (pos < arrayContent.Length)
            {
                while (pos < arrayContent.Length &&
                       (arrayContent[pos] == ' ' || arrayContent[pos] == '\t' ||
                        arrayContent[pos] == '\n' || arrayContent[pos] == '\r' ||
                        arrayContent[pos] == ','))
                    pos++;
                if (pos >= arrayContent.Length) break;
                if (arrayContent[pos] == '"')
                {
                    int itemStart = pos + 1;
                    int itemEnd = arrayContent.IndexOf('"', itemStart);
                    if (itemEnd < 0) break;
                    items.Add(arrayContent.Substring(itemStart, itemEnd - itemStart));
                    pos = itemEnd + 1;
                }
                else
                {
                    break;
                }
            }
            return items.ToArray();
        }

        public static async Task<VersionConfig> GetVersionConfigAsync()
        {
            int retryCount = 0;
            while (retryCount <= MaxRetryCount)
            {
                if (retryCount > 0)
                {
                    await Task.Delay(1000 * retryCount);
                }
                try
                {
                    using (HttpClient httpClient = CreateHttpClient())
                    {
                        string json;
                        using (var response = await httpClient.GetAsync(VersionUrl))
                        {
                            response.EnsureSuccessStatusCode();
                            json = await response.Content.ReadAsStringAsync();
                        }
                        var config = new VersionConfig();
                        config.latest_version = JsonGetString(json, "latest_version");
                        config.minimum_supported_version = JsonGetString(json, "minimum_supported_version");
                        config.release_date = JsonGetString(json, "release_date");
                        config.download_url = JsonGetString(json, "download_url");
                        config.announcement = JsonGetString(json, "announcement");
                        config.announcement_level = JsonGetString(json, "announcement_level");
                        config.changelog = JsonGetStringArray(json, "changelog");
                        return config;
                    }
                }
                catch (System.Net.Http.HttpRequestException ex)
                {
                    retryCount++;
                    string errorMsg = string.Format("[重试 {0}/{1}] HTTP请求失败：\n异常类型：{2}\n错误信息：{3}\n堆栈跟踪：{4}",
                        retryCount, MaxRetryCount, ex.GetType().Name, ex.Message, ex.StackTrace);
                    System.Diagnostics.Debug.WriteLine(errorMsg);
                    System.Diagnostics.Debug.WriteLine(string.Format("内部异常：{0}",
                        ex.InnerException != null ? ex.InnerException.Message : ""));
                    if (retryCount > MaxRetryCount)
                    {
                        ShowErrorMessage(string.Format("HTTP请求失败\n错误信息：{0}", ex.Message));
                        return null;
                    }
                }
                catch (TaskCanceledException ex)
                {
                    retryCount++;
                    string errorMsg = string.Format("[重试 {0}/{1}] 请求超时：\n异常类型：{2}\n错误信息：{3}\n堆栈跟踪：{4}",
                        retryCount, MaxRetryCount, ex.GetType().Name, ex.Message, ex.StackTrace);
                    System.Diagnostics.Debug.WriteLine(errorMsg);
                    if (retryCount > MaxRetryCount)
                    {
                        ShowErrorMessage("请求超时，请检查网络连接");
                        return null;
                    }
                }
                catch (Exception ex)
                {
                    retryCount++;
                    string errorMsg = string.Format("[重试 {0}/{1}] 未知错误：\n异常类型：{2}\n错误信息：{3}\n堆栈跟踪：{4}",
                        retryCount, MaxRetryCount, ex.GetType().Name, ex.Message, ex.StackTrace);
                    System.Diagnostics.Debug.WriteLine(errorMsg);
                    System.Diagnostics.Debug.WriteLine(string.Format("内部异常：{0}",
                        ex.InnerException != null ? ex.InnerException.Message : ""));
                    if (retryCount > MaxRetryCount)
                    {
                        ShowErrorMessage(string.Format("未知错误：{0}\n{1}", ex.GetType().Name, ex.Message));
                        return null;
                    }
                }
            }
            return null;
        }

        private static void ShowErrorMessage(string message)
        {
            if (System.Windows.Forms.Application.OpenForms.Count > 0)
            {
                var mainForm = System.Windows.Forms.Application.OpenForms[0];
                if (mainForm.InvokeRequired)
                {
                    mainForm.Invoke(new Action(() => 
                    {
                        System.Windows.Forms.MessageBox.Show(mainForm, message, "更新错误", 
                            System.Windows.Forms.MessageBoxButtons.OK, 
                            System.Windows.Forms.MessageBoxIcon.Error);
                    }));
                }
                else
                {
                    System.Windows.Forms.MessageBox.Show(mainForm, message, "更新错误", 
                        System.Windows.Forms.MessageBoxButtons.OK, 
                        System.Windows.Forms.MessageBoxIcon.Error);
                }
            }
        }

        public static bool HasUpdate(VersionConfig config, string currentVersion)
        {
            if (config == null || string.IsNullOrWhiteSpace(config.latest_version))
                return false;

            SemanticVersion current = new SemanticVersion(currentVersion);
            SemanticVersion latest = new SemanticVersion(config.latest_version);
            return latest > current;
        }

        public static bool IsVersionSupported(VersionConfig config, string currentVersion)
        {
            if (config == null || string.IsNullOrWhiteSpace(config.minimum_supported_version))
                return true;

            SemanticVersion current = new SemanticVersion(currentVersion);
            SemanticVersion minimum = new SemanticVersion(config.minimum_supported_version);
            return current >= minimum;
        }

        public static string CurrentVersion { get { return "1.0.1"; } }
    }

    public class UpdateForm : Form
    {
        private VersionConfig config;
        private bool isForcedUpdate;

        public UpdateForm(VersionConfig config, bool isForcedUpdate = false)
        {
            this.config = config;
            this.isForcedUpdate = isForcedUpdate;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "发现新版本";
            this.Size = new Size(420, 320);
            this.MinimumSize = new Size(420, 320);
            this.MaximumSize = new Size(420, 320);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.ControlBox = true;
            this.ShowIcon = true;
            this.BackColor = Color.White;

            Label lblTitle = new Label();
            lblTitle.Text = "发现新版本";
            lblTitle.Font = new Font("Microsoft YaHei", 14, FontStyle.Bold);
            lblTitle.ForeColor = Color.FromArgb(0, 120, 215);
            lblTitle.Location = new Point(20, 15);
            lblTitle.Size = new Size(380, 30);

            Label lblVersion = new Label();
            lblVersion.Text = string.Format("最新版本: v{0}", config.latest_version);
            lblVersion.Font = new Font("Microsoft YaHei", 11, FontStyle.Bold);
            lblVersion.Location = new Point(20, 50);
            lblVersion.Size = new Size(380, 25);

            Label lblDate = new Label();
            lblDate.Text = string.Format("发布日期: {0}", config.release_date);
            lblDate.Font = new Font("Microsoft YaHei", 10);
            lblDate.ForeColor = Color.FromArgb(100, 100, 100);
            lblDate.Location = new Point(20, 78);
            lblDate.Size = new Size(380, 20);

            Label lblChangelog = new Label();
            lblChangelog.Text = "更新日志:";
            lblChangelog.Font = new Font("Microsoft YaHei", 10);
            lblChangelog.Location = new Point(20, 105);
            lblChangelog.Size = new Size(380, 20);

            ListBox lstChangelog = new ListBox();
            lstChangelog.Location = new Point(20, 130);
            lstChangelog.Size = new Size(380, 120);
            lstChangelog.Font = new Font("Microsoft YaHei", 9);
            lstChangelog.BackColor = Color.FromArgb(248, 248, 250);
            lstChangelog.BorderStyle = BorderStyle.FixedSingle;
            lstChangelog.Enabled = false;

            if (config.changelog != null)
            {
                foreach (string item in config.changelog)
                {
                    lstChangelog.Items.Add("• " + item);
                }
            }

            Panel buttonPanel = new Panel();
            buttonPanel.Location = new Point(20, 260);
            buttonPanel.Size = new Size(380, 35);
            buttonPanel.BackColor = Color.White;

            Button btnDownload = new Button();
            btnDownload.Text = "立即下载";
            btnDownload.Size = new Size(120, 30);
            btnDownload.Location = new Point(buttonPanel.Width - 125, 2);
            btnDownload.Font = new Font("Microsoft YaHei", 10, FontStyle.Bold);
            btnDownload.FlatStyle = FlatStyle.Flat;
            btnDownload.FlatAppearance.BorderSize = 0;
            btnDownload.BackColor = Color.FromArgb(0, 120, 215);
            btnDownload.ForeColor = Color.White;
            btnDownload.Click += BtnDownload_Click;

            Button btnLater = new Button();
            btnLater.Text = "稍后再说";
            btnLater.Size = new Size(100, 30);
            btnLater.Location = new Point(buttonPanel.Width - 230, 2);
            btnLater.Font = new Font("Microsoft YaHei", 10);
            btnLater.FlatStyle = FlatStyle.Flat;
            btnLater.FlatAppearance.BorderSize = 1;
            btnLater.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
            btnLater.BackColor = Color.White;
            btnLater.ForeColor = Color.Black;
            btnLater.Click += (s, e) => { this.Close(); };

            if (isForcedUpdate)
            {
                btnLater.Visible = false;
                btnDownload.Location = new Point((buttonPanel.Width - btnDownload.Width) / 2, 2);
            }

            buttonPanel.Controls.Add(btnDownload);
            buttonPanel.Controls.Add(btnLater);

            this.Controls.Add(lblTitle);
            this.Controls.Add(lblVersion);
            this.Controls.Add(lblDate);
            this.Controls.Add(lblChangelog);
            this.Controls.Add(lstChangelog);
            this.Controls.Add(buttonPanel);
        }

        private void BtnDownload_Click(object sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = config.download_url,
                    UseShellExecute = true
                });
            }
            catch { }

            if (isForcedUpdate)
            {
                Application.Exit();
            }
            else
            {
                this.Close();
            }
        }
    }

    public class WaitingForm : Form
    {
        public WaitingForm()
        {
            this.Text = "检查更新";
            this.Size = new Size(280, 100);
            this.MinimumSize = new Size(280, 100);
            this.MaximumSize = new Size(280, 100);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.ControlBox = false;
            this.ShowIcon = false;
            this.BackColor = Color.White;

            Label lblMessage = new Label();
            lblMessage.Text = "正在检查更新...";
            lblMessage.Font = new Font("Microsoft YaHei", 11);
            lblMessage.ForeColor = Color.FromArgb(80, 80, 80);
            lblMessage.Location = new Point(20, 25);
            lblMessage.Size = new Size(240, 25);
            lblMessage.TextAlign = ContentAlignment.MiddleCenter;

            ProgressBar progressBar = new ProgressBar();
            progressBar.Location = new Point(20, 55);
            progressBar.Size = new Size(240, 20);
            progressBar.Style = ProgressBarStyle.Marquee;
            progressBar.MarqueeAnimationSpeed = 150;

            this.Controls.Add(lblMessage);
            this.Controls.Add(progressBar);
        }
    }

    public class AnnouncementBar : Panel
    {
        public event Action OnClosed;

        public AnnouncementBar(string message, string level)
        {
            this.Dock = DockStyle.Top;
            this.Height = 30;
            this.BackColor = Color.FromArgb(0xf5, 0xf5, 0xf5);

            Label lblMessage = new Label();
            lblMessage.Text = message;
            lblMessage.Font = new Font("Microsoft YaHei", 9);
            lblMessage.ForeColor = Color.FromArgb(0x44, 0x44, 0x44);
            lblMessage.AutoSize = false;
            lblMessage.Dock = DockStyle.Fill;
            lblMessage.TextAlign = ContentAlignment.MiddleLeft;
            lblMessage.Padding = new Padding(10, 0, 0, 0);

            Button btnClose = new Button();
            btnClose.Text = "×";
            btnClose.Size = new Size(30, 30);
            btnClose.Dock = DockStyle.Right;
            btnClose.Font = new Font("Microsoft YaHei", 10);
            btnClose.FlatStyle = FlatStyle.Flat;
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.BackColor = Color.Transparent;
            btnClose.ForeColor = Color.FromArgb(0x66, 0x66, 0x66);
            btnClose.Click += (s, e) =>
            {
                this.Visible = false;
                var handler = OnClosed;
                if (handler != null)
                    handler();
            };

            this.Controls.Add(lblMessage);
            this.Controls.Add(btnClose);
        }
    }

    public partial class MainForm : Form
    {
        private TextBox[] nameParts = new TextBox[4];
        private ComboBox cmbNumbering;
        private FlowLayoutPanel flowPanel;
        private Label lblDropHint;
        private List<ImageItem> items = new List<ImageItem>();
        private Button btnSelectFiles, btnClearAll, btnExportSelected, btnExportAll;
        private Label lblCount;
        private AnnouncementBar announcementBar;
        private bool announcementClosed = false;
        private MenuStrip menuStrip;
        private Panel contentPanel;
        private LinkLabel lnkCheckUpdate;
        private Label lblVersion;

        public MainForm()
        {
            InitializeComponent();
            this.AllowDrop = true;
            this.Load += MainForm_Load;
        }

        private async void MainForm_Load(object sender, EventArgs e)
        {
            await CheckUpdateAsync(true);
        }

        private async Task CheckUpdateAsync(bool isSilent)
        {
            VersionConfig config = await UpdateService.GetVersionConfigAsync();
            if (config == null)
            {
                if (!isSilent)
                {
                    MessageBox.Show("无法连接到更新服务器，请检查网络连接", "检查更新", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return;
            }

            if (!string.IsNullOrWhiteSpace(config.announcement) && !announcementClosed)
            {
                ShowAnnouncement(config.announcement, config.announcement_level);
            }

            if (!UpdateService.IsVersionSupported(config, UpdateService.CurrentVersion))
            {
                UpdateForm updateForm = new UpdateForm(config, true);
                updateForm.ShowDialog();
                return;
            }

            if (UpdateService.HasUpdate(config, UpdateService.CurrentVersion))
            {
                UpdateForm updateForm = new UpdateForm(config);
                updateForm.ShowDialog();
            }
            else if (!isSilent)
            {
                MessageBox.Show("当前已是最新版本", "检查更新", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void ShowAnnouncement(string message, string level)
        {
            if (announcementBar != null)
            {
                this.Controls.Remove(announcementBar);
            }

            announcementBar = new AnnouncementBar(message, level);
            announcementBar.OnClosed += () => 
            { 
                announcementClosed = true; 
                if (contentPanel != null)
                {
                    contentPanel.Padding = new Padding(0, menuStrip.Height, 0, 0);
                }
            };
            
            this.Controls.Add(announcementBar);
            
            if (contentPanel != null)
            {
                contentPanel.Padding = new Padding(0, menuStrip.Height + 30, 0, 0);
            }
            
            announcementBar.BringToFront();
        }

        private async void MenuCheckUpdate_Click(object sender, EventArgs e)
        {
            using (var waitingForm = new WaitingForm())
            {
                waitingForm.Show();
                await Task.Delay(100);
                Application.DoEvents();

                await CheckUpdateAsync(false);

                waitingForm.Close();
            }
        }

        private void InitializeComponent()
        {
            this.Text = "批量图片重命名导出工具";
            this.Size = new Size(900, 720);
            this.MinimumSize = new Size(680, 500);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(245, 245, 248);
            this.Padding = new Padding(0);
            try
            {
                this.Icon = new System.Drawing.Icon("app.ico");
            }
            catch { }

            menuStrip = new MenuStrip();
            menuStrip.BackColor = Color.White;
            menuStrip.Font = new Font("Microsoft YaHei", 9);
            menuStrip.Dock = DockStyle.Top;
            menuStrip.Height = 24;

            ToolStripMenuItem menuHelp = new ToolStripMenuItem("帮助");
            ToolStripMenuItem menuContactAuthor = new ToolStripMenuItem("联系作者（GitHub）");
            menuContactAuthor.Click += (s, e) => 
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "https://github.com/cheng1260/PicBatchAutoRenamer",
                        UseShellExecute = true
                    });
                }
                catch { }
            };
            menuHelp.DropDownItems.Add(menuContactAuthor);
            menuStrip.Items.Add(menuHelp);

            this.MainMenuStrip = menuStrip;
            this.Controls.Add(menuStrip);

            contentPanel = new Panel();
            contentPanel.Dock = DockStyle.Fill;
            contentPanel.Padding = new Padding(0, menuStrip.Height, 0, 0);
            this.Controls.Add(contentPanel);

            TableLayoutPanel mainLayout = new TableLayoutPanel();
            mainLayout.Dock = DockStyle.Fill;
            mainLayout.ColumnCount = 1;
            mainLayout.RowCount = 3;
            mainLayout.Padding = new Padding(0);
            mainLayout.Margin = new Padding(0);
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 55F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 75F));
            contentPanel.Controls.Add(mainLayout);

            // Top panel
            Panel topPanel = new Panel();
            topPanel.Dock = DockStyle.Fill;
            topPanel.Height = 55;
            topPanel.Padding = new Padding(10, 6, 10, 0);

            string[] defaults = { "", "-", "AI", "-" };
            int[] widths = { 120, 50, 80, 50 };
            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                nameParts[i] = new TextBox();
                nameParts[i].Text = defaults[i];
                nameParts[i].Width = widths[i];
                nameParts[i].Height = 30;
                nameParts[i].Font = new Font("Microsoft YaHei", 11, FontStyle.Bold);
                nameParts[i].TextAlign = HorizontalAlignment.Center;
                nameParts[i].BorderStyle = BorderStyle.FixedSingle;
                nameParts[i].TextChanged += (s, e) => { RefreshPreview(); };
                string def = defaults[i];
                nameParts[i].Click += (s, e) =>
                {
                    if (nameParts[idx].Text == def)
                        nameParts[idx].SelectAll();
                };
            }

            Label[] plusLabels = new Label[4];
            for (int i = 0; i < 4; i++)
            {
                plusLabels[i] = new Label();
                plusLabels[i].Text = "+";
                plusLabels[i].AutoSize = true;
                plusLabels[i].Font = new Font("Microsoft YaHei", 12, FontStyle.Bold);
                plusLabels[i].ForeColor = Color.FromArgb(80, 80, 80);
                plusLabels[i].Padding = new Padding(3, 4, 3, 0);
            }

            cmbNumbering = new ComboBox();
            cmbNumbering.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbNumbering.Width = 130;
            cmbNumbering.Height = 30;
            cmbNumbering.Font = new Font("Microsoft YaHei", 10);
            cmbNumbering.Items.AddRange(new object[] { "数字(1,2,3...)", "字母(A,B,C...)" });
            cmbNumbering.SelectedIndex = 0;
            cmbNumbering.SelectedIndexChanged += (s, e) => { RefreshPreview(); };

            FlowLayoutPanel inputFlow = new FlowLayoutPanel();
            inputFlow.Dock = DockStyle.Fill;
            inputFlow.AutoSize = false;
            inputFlow.WrapContents = false;
            inputFlow.Height = 40;
            inputFlow.Controls.Add(nameParts[0]);
            inputFlow.Controls.Add(plusLabels[0]);
            inputFlow.Controls.Add(nameParts[1]);
            inputFlow.Controls.Add(plusLabels[1]);
            inputFlow.Controls.Add(nameParts[2]);
            inputFlow.Controls.Add(plusLabels[2]);
            inputFlow.Controls.Add(nameParts[3]);
            inputFlow.Controls.Add(plusLabels[3]);
            inputFlow.Controls.Add(cmbNumbering);

            topPanel.Controls.Add(inputFlow);
            mainLayout.Controls.Add(topPanel, 0, 0);

            // Middle: drop zone / list
            flowPanel = new FlowLayoutPanel();
            flowPanel.Dock = DockStyle.Fill;
            flowPanel.AutoScroll = true;
            flowPanel.BackColor = Color.White;
            flowPanel.Padding = new Padding(8);
            flowPanel.AllowDrop = true;
            flowPanel.BorderStyle = BorderStyle.FixedSingle;
            flowPanel.DragEnter += DropPanel_DragEnter;
            flowPanel.DragDrop += DropPanel_DragDrop;

            lblDropHint = new Label();
            lblDropHint.Text = "拖拽多张图片到此处\r\n或点击下方「选择文件」按钮";
            lblDropHint.TextAlign = ContentAlignment.MiddleCenter;
            lblDropHint.Font = new Font("Microsoft YaHei", 14);
            lblDropHint.ForeColor = Color.FromArgb(160, 160, 160);
            lblDropHint.Size = new Size(400, 80);
            lblDropHint.AutoSize = false;

            Panel listContainer = new Panel();
            listContainer.Dock = DockStyle.Fill;
            listContainer.BackColor = Color.White;
            listContainer.Controls.Add(flowPanel);
            listContainer.Controls.Add(lblDropHint);
            listContainer.Resize += (s, e) =>
            {
                lblDropHint.Left = (listContainer.ClientSize.Width - lblDropHint.Width) / 2;
                lblDropHint.Top = (listContainer.ClientSize.Height - lblDropHint.Height) / 2;
            };

            mainLayout.Controls.Add(listContainer, 0, 1);

            // Bottom panel
            Panel bottomPanel = new Panel();
            bottomPanel.Dock = DockStyle.Fill;
            bottomPanel.Height = 75;
            bottomPanel.Padding = new Padding(0);

            TableLayoutPanel bottomLayout = new TableLayoutPanel();
            bottomLayout.Dock = DockStyle.Fill;
            bottomLayout.ColumnCount = 1;
            bottomLayout.RowCount = 2;
            bottomLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            bottomLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F));

            btnSelectFiles = MakeButton("选择文件...", 100, Color.FromArgb(220, 220, 230), Color.Black);
            btnSelectFiles.Click += BtnSelectFiles_Click;

            btnClearAll = MakeButton("清除全部", 90, Color.FromArgb(255, 200, 200), Color.Black);
            btnClearAll.Click += (s, e) => { ClearAll(); };

            lblCount = new Label();
            lblCount.Text = "图片: 0";
            lblCount.AutoSize = true;
            lblCount.Font = new Font("Microsoft YaHei", 10);
            lblCount.ForeColor = Color.FromArgb(80, 80, 80);
            lblCount.Padding = new Padding(8, 6, 8, 0);

            btnExportSelected = MakeButton("导出选中图片", 120, Color.FromArgb(100, 180, 255), Color.White);
            btnExportSelected.Click += BtnExportSelected_Click;
            btnExportSelected.Enabled = false;

            btnExportAll = MakeButton("一键导出全部", 130, Color.FromArgb(0, 120, 215), Color.White);
            btnExportAll.Click += BtnExportAll_Click;

            FlowLayoutPanel bottomFlow = new FlowLayoutPanel();
            bottomFlow.Dock = DockStyle.Fill;
            bottomFlow.WrapContents = false;
            bottomFlow.FlowDirection = FlowDirection.LeftToRight;
            bottomFlow.Padding = new Padding(8, 6, 8, 2);
            bottomFlow.Controls.AddRange(new Control[] {
                btnSelectFiles, btnClearAll, lblCount,
                btnExportSelected, btnExportAll
            });

            bottomLayout.Controls.Add(bottomFlow, 0, 0);

            Panel checkUpdatePanel = new Panel();
            checkUpdatePanel.Dock = DockStyle.Fill;
            checkUpdatePanel.Padding = new Padding(8, 3, 8, 3);

            lnkCheckUpdate = new LinkLabel();
            lnkCheckUpdate.Text = "检查更新";
            lnkCheckUpdate.AutoSize = true;
            lnkCheckUpdate.Font = new Font("Microsoft YaHei", 8);
            lnkCheckUpdate.LinkBehavior = LinkBehavior.NeverUnderline;
            lnkCheckUpdate.LinkColor = Color.FromArgb(0x55, 0x55, 0x55);
            lnkCheckUpdate.VisitedLinkColor = Color.FromArgb(0x55, 0x55, 0x55);
            lnkCheckUpdate.LinkClicked += (s, e) => { MenuCheckUpdate_Click(s, e); };
            lnkCheckUpdate.Location = new Point(0, 6);

            lblVersion = new Label();
            lblVersion.Text = string.Format("当前版本: v{0}", UpdateService.CurrentVersion);
            lblVersion.AutoSize = true;
            lblVersion.Font = new Font("Microsoft YaHei", 7);
            lblVersion.ForeColor = Color.FromArgb(0x88, 0x88, 0x88);
            lblVersion.Location = new Point(lnkCheckUpdate.Width + 15, 8);

            checkUpdatePanel.Controls.Add(lnkCheckUpdate);
            checkUpdatePanel.Controls.Add(lblVersion);
            bottomLayout.Controls.Add(checkUpdatePanel, 0, 1);

            bottomPanel.Controls.Add(bottomLayout);
            mainLayout.Controls.Add(bottomPanel, 0, 2);
        }

        private Button MakeButton(string text, int width, Color backColor, Color foreColor)
        {
            Button btn = new Button();
            btn.Text = text;
            btn.Width = width;
            btn.Height = 30;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.BackColor = backColor;
            btn.ForeColor = foreColor;
            btn.Font = new Font("Microsoft YaHei", 9);
            btn.Margin = new Padding(2, 0, 2, 0);
            return btn;
        }

        internal string GetExportFileName(int index)
        {
            string part1 = nameParts[0].Text;
            string part2 = nameParts[1].Text;
            string part3 = nameParts[2].Text;
            string part4 = nameParts[3].Text;
            string seq;
            if (cmbNumbering.SelectedIndex == 0)
                seq = (index + 1).ToString();
            else
                seq = NumberToLetters(index + 1);
            return part1 + part2 + part3 + part4 + seq;
        }

        private string NumberToLetters(int n)
        {
            string result = "";
            while (n > 0)
            {
                n--;
                result = (char)('A' + n % 26) + result;
                n /= 26;
            }
            return result;
        }

        private void RefreshPreview()
        {
            for (int i = 0; i < flowPanel.Controls.Count; i++)
            {
                if (i >= items.Count) break;
                ImageItemControl ctrl = flowPanel.Controls[i] as ImageItemControl;
                if (ctrl != null)
                    ctrl.SetPreviewName(GetExportFileName(i));
            }
        }

        private void DropPanel_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        }

        private void DropPanel_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            AddFiles(files);
        }

        protected override void OnDragEnter(DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
            base.OnDragEnter(e);
        }

        protected override void OnDragDrop(DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0)
                AddFiles(files);
            base.OnDragDrop(e);
        }

        private void BtnSelectFiles_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Multiselect = true;
                dlg.Filter = "图片文件|*.jpg;*.jpeg;*.png;*.gif;*.bmp;*.webp|所有文件|*.*";
                if (dlg.ShowDialog() == DialogResult.OK)
                    AddFiles(dlg.FileNames);
            }
        }

        private void AddFiles(string[] files)
        {
            string[] exts = { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };
            foreach (string file in files)
            {
                string ext = Path.GetExtension(file).ToLower();
                if (Array.IndexOf(exts, ext) < 0) continue;
                if (items.Exists(i => i.FilePath == file)) continue;

                ImageItem imgItem = new ImageItem();
                imgItem.FilePath = file;
                imgItem.FileName = Path.GetFileName(file);
                items.Add(imgItem);
            }
            RebuildList();
        }

        private void RebuildList()
        {
            flowPanel.Controls.Clear();
            for (int i = 0; i < items.Count; i++)
            {
                int idx = i;
                ImageItemControl ctrl = new ImageItemControl(items[i], idx, this);
                int capturedIdx = i;
                ctrl.OnRemove += delegate(int id)
                {
                    items.RemoveAt(id);
                    RebuildList();
                };
                ctrl.OnCheckChanged += delegate()
                {
                    UpdateExportButton();
                };
                ctrl.OnExport += delegate(int id)
                {
                    ExportSingle(id);
                };
                flowPanel.Controls.Add(ctrl);
            }
            RefreshPreview();
            UpdateExportButton();
            UpdateCount();
            lblDropHint.Visible = (items.Count == 0);
        }

        private void UpdateExportButton()
        {
            int checkedCount = 0;
            foreach (ImageItem i in items)
                if (i.Checked) checkedCount++;
            btnExportSelected.Enabled = checkedCount > 0;
        }

        private void UpdateCount()
        {
            lblCount.Text = string.Format("图片: {0}", items.Count);
        }

        private void ClearAll()
        {
            items.Clear();
            RebuildList();
        }

        private void BtnExportAll_Click(object sender, EventArgs e)
        {
            if (items.Count == 0) { MessageBox.Show("没有图片可导出", "提示"); return; }
            ExportItems(0, items.Count - 1, null);
        }

        private void BtnExportSelected_Click(object sender, EventArgs e)
        {
            List<int> checkedItems = new List<int>();
            for (int i = 0; i < items.Count; i++)
                if (items[i].Checked) checkedItems.Add(i);
            if (checkedItems.Count == 0) { MessageBox.Show("请先勾选要导出的图片", "提示"); return; }
            ExportItems(checkedItems[0], checkedItems[checkedItems.Count - 1], checkedItems);
        }

        public void ExportSingle(int index)
        {
            if (index < 0 || index >= items.Count) return;
            List<int> indices = new List<int>();
            indices.Add(index);
            ExportItems(index, index, indices);
        }

        private void ExportItems(int startIdx, int endIdx, List<int> specificIndices)
        {
            using (FolderBrowserDialog dlg = new FolderBrowserDialog())
            {
                dlg.Description = "选择保存位置";
                if (dlg.ShowDialog() != DialogResult.OK) return;
                string saveDir = dlg.SelectedPath;

                List<int> indices;
                if (specificIndices != null)
                {
                    indices = specificIndices;
                }
                else
                {
                    indices = new List<int>();
                    for (int i = startIdx; i <= endIdx; i++)
                        indices.Add(i);
                }

                using (ProgressForm progress = new ProgressForm())
                {
                    progress.SetRange(0, indices.Count);
                    progress.Show();
                    progress.Refresh();
                    Application.DoEvents();

                    int count = 0;
                    foreach (int idx in indices)
                    {
                        if (idx < 0 || idx >= items.Count) continue;
                        string srcPath = items[idx].FilePath;
                        string ext = Path.GetExtension(srcPath);
                        string newName = GetExportFileName(idx) + ext;
                        string destPath = Path.Combine(saveDir, newName);

                        int collision = 1;
                        while (File.Exists(destPath))
                        {
                            destPath = Path.Combine(saveDir,
                                string.Format("{0}({1}){2}", GetExportFileName(idx), collision, ext));
                            collision++;
                        }

                        try
                        {
                            File.Copy(srcPath, destPath, overwrite: false);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(string.Format("导出失败: {0}\n{1}", items[idx].FileName, ex.Message), "错误");
                        }

                        count++;
                        progress.SetProgress(count);
                        Application.DoEvents();
                    }
                    progress.Close();
                }

                DialogResult result = MessageBox.Show(
                    string.Format("成功导出 {0} 张图片到:\n{1}\n\n要打开保存文件夹吗？",
                        indices.Count, saveDir),
                    "导出完成",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (result == DialogResult.Yes)
                    System.Diagnostics.Process.Start("explorer.exe", saveDir);
            }
        }
    }

    public class ImageItem
    {
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public bool Checked { get; set; }
        public ImageItem()
        {
            Checked = true;
        }
    }

    public class ImageItemControl : UserControl
    {
        private ImageItem item;
        private int index;
        private MainForm parentForm;
        private PictureBox thumbBox;
        private Label lblOriginal, lblPreview;
        private CheckBox chk;
        private Button btnRemove, btnExport;
        public event Action<int> OnRemove;
        public event Action OnCheckChanged;
        public event Action<int> OnExport;

        public ImageItemControl(ImageItem item, int index, MainForm parent)
        {
            this.item = item;
            this.index = index;
            this.parentForm = parent;
            InitializeControl();
        }

        private void InitializeControl()
        {
            this.Height = 56;
            this.Margin = new Padding(2, 3, 2, 3);
            this.BackColor = Color.FromArgb(250, 250, 252);
            this.BorderStyle = BorderStyle.FixedSingle;

            thumbBox = new PictureBox();
            thumbBox.Size = new Size(48, 48);
            thumbBox.Location = new Point(4, 4);
            thumbBox.SizeMode = PictureBoxSizeMode.Zoom;
            thumbBox.BackColor = Color.FromArgb(240, 240, 245);
            try
            {
                using (Image img = Image.FromFile(item.FilePath))
                {
                    int w = img.Width;
                    int h = img.Height;
                    if (w > 48)
                    {
                        h = h * 48 / w;
                        w = 48;
                        if (h > 48)
                        {
                            w = w * 48 / h;
                            h = 48;
                        }
                    }
                    if (h > 48)
                    {
                        w = w * 48 / h;
                        h = 48;
                        if (w > 48)
                        {
                            h = h * 48 / w;
                            w = 48;
                        }
                    }
                    if (w < 1) w = 1;
                    if (h < 1) h = 1;
                    thumbBox.Image = new Bitmap(img, w, h);
                }
            }
            catch { }

            chk = new CheckBox();
            chk.Location = new Point(56, 18);
            chk.Size = new Size(16, 16);
            chk.Checked = item.Checked;
            chk.FlatStyle = FlatStyle.Standard;
            chk.CheckedChanged += delegate(object s, EventArgs e)
            {
                item.Checked = chk.Checked;
                if (OnCheckChanged != null)
                    OnCheckChanged();
            };

            lblOriginal = new Label();
            lblOriginal.Text = TruncateText(item.FileName, 28);
            lblOriginal.Location = new Point(78, 4);
            lblOriginal.Size = new Size(220, 20);
            lblOriginal.Font = new Font("Microsoft YaHei", 9);
            lblOriginal.ForeColor = Color.FromArgb(80, 80, 80);
            lblOriginal.AutoEllipsis = true;

            lblPreview = new Label();
            lblPreview.Text = parentForm.GetExportFileName(index) + Path.GetExtension(item.FilePath);
            lblPreview.Location = new Point(78, 26);
            lblPreview.Size = new Size(260, 20);
            lblPreview.Font = new Font("Microsoft YaHei", 9, FontStyle.Bold);
            lblPreview.ForeColor = Color.FromArgb(0, 100, 200);
            lblPreview.AutoEllipsis = true;

            btnExport = new Button();
            btnExport.Text = "导出";
            btnExport.Size = new Size(50, 26);
            btnExport.Location = new Point(350, 14);
            btnExport.FlatStyle = FlatStyle.Flat;
            btnExport.FlatAppearance.BorderSize = 0;
            btnExport.BackColor = Color.FromArgb(0, 120, 215);
            btnExport.ForeColor = Color.White;
            btnExport.Font = new Font("Microsoft YaHei", 8, FontStyle.Bold);
            int capIdxExp = index;
            btnExport.Click += delegate(object s, EventArgs e)
            {
                if (OnExport != null)
                    OnExport(capIdxExp);
            };

            btnRemove = new Button();
            btnRemove.Text = "X";
            btnRemove.Size = new Size(24, 24);
            btnRemove.Location = new Point(410, 16);
            btnRemove.FlatStyle = FlatStyle.Flat;
            btnRemove.FlatAppearance.BorderSize = 0;
            btnRemove.BackColor = Color.FromArgb(255, 200, 200);
            btnRemove.ForeColor = Color.FromArgb(160, 40, 40);
            btnRemove.Font = new Font("Microsoft YaHei", 8, FontStyle.Bold);
            int capIdxRem = index;
            btnRemove.Click += delegate(object s, EventArgs e)
            {
                if (OnRemove != null)
                    OnRemove(capIdxRem);
            };

            ContextMenuStrip = new ContextMenuStrip();
            int ctxIdx = index;
            ContextMenuStrip.Items.Add("导出此图片", null, delegate(object s, EventArgs e)
            {
                if (OnExport != null) OnExport(ctxIdx);
            });
            ContextMenuStrip.Items.Add("从列表中移除", null, delegate(object s, EventArgs e)
            {
                if (OnRemove != null) OnRemove(ctxIdx);
            });
            ContextMenuStrip.Items.Add(new ToolStripSeparator());
            ContextMenuStrip.Items.Add("选中此图片", null, delegate(object s, EventArgs e)
            {
                chk.Checked = true;
            });
            ContextMenuStrip.Items.Add("取消选中此图片", null, delegate(object s, EventArgs e)
            {
                chk.Checked = false;
            });

            this.Controls.AddRange(new Control[] {
                thumbBox, chk, lblOriginal, lblPreview, btnExport, btnRemove
            });
        }

        public void SetPreviewName(string name)
        {
            string ext = Path.GetExtension(item.FilePath);
            lblPreview.Text = name + ext;
        }

        private string TruncateText(string text, int maxLen)
        {
            if (text.Length <= maxLen) return text;
            return text.Substring(0, maxLen - 3) + "...";
        }
    }

    public class ProgressForm : Form
    {
        private ProgressBar bar;
        private Label lbl;

        public ProgressForm()
        {
            this.Text = "导出中...";
            this.Size = new Size(350, 100);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.ControlBox = false;
            this.BackColor = Color.White;

            lbl = new Label();
            lbl.Text = "正在导出图片...";
            lbl.Location = new Point(15, 12);
            lbl.Size = new Size(320, 20);
            lbl.Font = new Font("Microsoft YaHei", 10);

            bar = new ProgressBar();
            bar.Location = new Point(15, 40);
            bar.Size = new Size(310, 24);
            bar.Style = ProgressBarStyle.Continuous;

            this.Controls.Add(lbl);
            this.Controls.Add(bar);
        }

        public void SetRange(int min, int max)
        {
            bar.Minimum = min;
            bar.Maximum = max;
        }

        public void SetProgress(int val)
        {
            bar.Value = val;
            lbl.Text = string.Format("正在导出... ({0}/{1})", val, bar.Maximum);
        }
    }

    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}