using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security;
using System.Text;
using System.Windows.Forms;

namespace HOI4Toolbox
{
    public partial class MainForm : Form
    {
        private static readonly Color[] ButtonColors = 
        {
            Color.FromArgb(70, 130, 180),
            Color.FromArgb(46, 139, 87),
            Color.FromArgb(205, 133, 63),
            Color.FromArgb(178, 34, 34)
        };

        private static readonly string[] ButtonTexts = 
        {
            "1 下载游戏",
            "2 下载 MOD",
            "3 注册模组",
            "4 加载补丁"
        };

        public MainForm()
        {
            InitializeComponent();
            InitializeUI();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.AutoScaleDimensions = new SizeF(6F, 13F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(500, 350);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MinimumSize = new Size(500, 350);
            this.Name = "MainForm";
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Text = "钢铁雄心4 工具箱";
            this.ResumeLayout(false);
        }

        private void InitializeUI()
        {
            Icon? appIcon = LoadAppIcon();
            if (appIcon != null)
            {
                this.Icon = appIcon;
            }

            var mainPanel = new TableLayoutPanel
            {
                ColumnCount = 1,
                RowCount = ButtonTexts.Length,
                Dock = DockStyle.Fill,
                Padding = new Padding(30),
                RowStyles = {
                    new RowStyle(SizeType.Percent, 25F),
                    new RowStyle(SizeType.Percent, 25F),
                    new RowStyle(SizeType.Percent, 25F),
                    new RowStyle(SizeType.Percent, 25F)
                }
            };

            for (int i = 0; i < ButtonTexts.Length; i++)
            {
                int buttonIndex = i;
                int actionId = buttonIndex + 1;
                
                var button = CreateStyledButton(ButtonTexts[buttonIndex], ButtonColors[buttonIndex]);
                button.Click += (sender, e) => HandleButtonClick(actionId);
                mainPanel.Controls.Add(button);
            }

            this.Controls.Add(mainPanel);
        }

        private Button CreateStyledButton(string text, Color bgColor)
        {
            return new Button
            {
                Text = text,
                Font = new Font("微软雅黑", 12, FontStyle.Bold),
                BackColor = bgColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                Dock = DockStyle.Fill,
                Cursor = Cursors.Hand,
                Margin = new Padding(10, 15, 10, 15),
                Height = 70
            };
        }

        private Icon? LoadAppIcon()
        {
            try
            {
                Assembly? assembly = Assembly.GetExecutingAssembly();
                if (assembly == null) return null;
                
                using (var stream = assembly.GetManifestResourceStream("HOI4Toolbox.Resources.icon.ico"))
                {
                    return stream != null ? new Icon(stream) : null;
                }
            }
            catch
            {
                return null;
            }
        }

        private void HandleButtonClick(int actionId)
        {
            switch (actionId)
            {
                case 1:
                    StartGameDownload();
                    break;
                case 2:
                    DownloadMod();
                    break;
                case 3:
                    RegisterMod();
                    break;
                case 4:
                    LoadPatch();
                    break;
                default:
                    MessageBox.Show($"按钮 {actionId} 被点击", "调试信息");
                    break;
            }
        }

        #region 功能一：下载游戏
        private void StartGameDownload()
        {
            string outputDir = GetDownloadDirectory();
            if (string.IsNullOrEmpty(outputDir))
            {
                MessageBox.Show("无法确定下载目录", "错误", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                Directory.CreateDirectory(outputDir);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法创建下载目录: {ex.Message}", "错误", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string completionFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "win.txt");
            try
            {
                if (File.Exists(completionFile))
                {
                    File.Delete(completionFile);
                }
            }
            catch { }

            var progressDialog = new DownloadProgressDialog();
            progressDialog.Show(this);
            
            Task.Run(() => MonitorDownloadCompletion(progressDialog));
            
            Task.Run(() => RunExternalDownloadScript(
                "https://alloysa-my.sharepoint.com/personal/admin_hejincn_com/_layouts/15/download.aspx?UniqueId=71910ad2-300f-4412-956a-5e01e1fdf214",
                outputDir,
                progressDialog.UpdateProgress
            ));
        }

        private void MonitorDownloadCompletion(DownloadProgressDialog dialog)
        {
            string completionFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "win.txt");
            
            for (int i = 0; i < 1800; i++)
            {
                if (File.Exists(completionFile))
                {
                    this.Invoke(new Action(() =>
                    {
                        dialog.Close();
                        MessageBox.Show("下载已完成！", "完成", 
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }));
                    return;
                }
                System.Threading.Thread.Sleep(1000);
            }
            
            this.Invoke(new Action(() =>
            {
                dialog.Close();
                MessageBox.Show("下载超时，请检查网络连接", "错误", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }));
        }

        private void RunExternalDownloadScript(string url, string outputDir, Action<Aria2ProgressData> progressHandler)
        {
            try
            {
                string appDir = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory).TrimEnd('\\');

                string? ariaPath = ExtractAria2Binary();
                if (ariaPath == null)
                {
                    progressHandler(new Aria2ProgressData { ErrorMessage = "无法提取下载工具" });
                    return;
                }
                
                string batPath = Path.Combine(appDir, "download_game.bat");
                try
                {
                    using (var stream = Assembly.GetExecutingAssembly()
                        .GetManifestResourceStream("HOI4Toolbox.Resources.download_game.bat"))
                    {
                        if (stream == null) throw new FileNotFoundException("找不到嵌入的批处理文件");
                        using (var file = File.Create(batPath))
                        {
                            stream.CopyTo(file);
                        }
                    }
                }
                catch (Exception ex)
                {
                    progressHandler(new Aria2ProgressData { ErrorMessage = $"提取脚本失败: {ex.Message}" });
                    return;
                }

                string arguments = $"\"{url}\" \"{outputDir}\" \"{ariaPath}\" \"{appDir}\"";

                var startInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"\"{batPath}\" {arguments}\"",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Normal,
                    CreateNoWindow = false,
                    WorkingDirectory = appDir
                };

                using (var process = new Process { StartInfo = startInfo })
                {
                    process.Start();
                    process.WaitForExit();

                    if (process.ExitCode != 0)
                    {
                        progressHandler(new Aria2ProgressData { ErrorMessage = $"下载失败，错误代码: {process.ExitCode}" });
                    }
                }
            }
            catch (Exception ex)
            {
                progressHandler(new Aria2ProgressData { ErrorMessage = $"下载失败: {ex.Message}" });
            }
            finally
            {
                try
                {
                    string tempDir = Path.Combine(Path.GetTempPath(), "HOI4Download");
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
                catch { }
            }
        }

        private string GetDownloadDirectory()
        {
            try
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Downloads"
                );
            }
            catch
            {
                return string.Empty;
            }
        }

        private string? ExtractAria2Binary()
        {
            try
            {
                bool is64Bit = Environment.Is64BitOperatingSystem;
                string resourceName = is64Bit ? "aria2-64.exe" : "aria2-32.exe";
                string appDir = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory).TrimEnd('\\');
                string targetPath = Path.Combine(appDir, resourceName);
                
                using (var stream = Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream($"HOI4Toolbox.Resources.{resourceName}"))
                {
                    if (stream == null) return null;
                    using (var file = File.Create(targetPath))
                    {
                        stream.CopyTo(file);
                    }
                }
                return targetPath;
            }
            catch
            {
                return null;
            }
        }
        #endregion

        #region 功能二：下载MOD
        private void DownloadMod()
        {
            try
            {
                string tempExePath = Path.GetTempFileName() + ".exe";
                
                // 根据系统架构提取正确的资源
                string resourceName = Environment.Is64BitOperatingSystem ? 
                    "mod64.exe" : "mod32.exe";
                
                using (var stream = Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream($"HOI4Toolbox.Resources.{resourceName}"))
                {
                    if (stream == null)
                    {
                        MessageBox.Show("找不到MOD下载程序", "资源错误",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    
                    using (var file = File.Create(tempExePath))
                    {
                        stream.CopyTo(file);
                    }
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = tempExePath,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                
                using (var process = new Process { StartInfo = startInfo })
                {
                    process.Start();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"MOD下载失败: {ex.Message}", "错误", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        #endregion

        #region 功能三：注册模组
        private void RegisterMod()
        {
            string targetDir = PromptDialog.Show("模组注册", "请输入模组安装目录路径:", "");
            if (string.IsNullOrWhiteSpace(targetDir)) return;

            try
            {
                targetDir = Path.GetFullPath(targetDir).TrimEnd('\\');
                
                if (!Directory.Exists(targetDir))
                {
                    MessageBox.Show("指定目录不存在", "路径错误",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // 从资源中提取批处理文件
                string batPath = Path.Combine(targetDir, "mod.bat");
                using (var stream = Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream("HOI4Toolbox.Resources.mod.bat"))
                {
                    if (stream == null)
                    {
                        MessageBox.Show("找不到模组注册脚本", "资源错误",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    
                    using (var file = File.Create(batPath))
                    {
                        stream.CopyTo(file);
                    }
                }

                MessageBox.Show($"mod.bat 已复制到: {batPath}", "操作成功",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show("没有写入权限，请尝试管理员身份运行", "权限不足",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"注册失败: {ex.Message}", "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        #endregion

        #region 功能四：加载补丁
        private void LoadPatch()
        {
            string gamePath = PromptDialog.Show("加载补丁", "请输入游戏安装路径:", "");
            if (string.IsNullOrWhiteSpace(gamePath)) return;

            try
            {
                gamePath = Path.GetFullPath(gamePath).TrimEnd('\\');
                
                // 验证游戏路径
                if (!Directory.Exists(gamePath))
                {
                    MessageBox.Show("指定的游戏目录不存在", "路径错误",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // 可选：确认是否为有效的HOI4路径
                string exePath = Path.Combine(gamePath, "hoi4.exe");
                if (!File.Exists(exePath))
                {
                    var result = MessageBox.Show("未找到hoi4.exe，确认继续吗？", "路径确认",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (result != DialogResult.Yes) return;
                }

                // 创建临时ZIP文件路径
                string tempZipPath = Path.GetTempFileName() + ".zip";
                try
                {
                    // 从资源中提取ZIP文件
                    using (var stream = Assembly.GetExecutingAssembly()
                        .GetManifestResourceStream("HOI4Toolbox.Resources.buding.zip"))
                    {
                        if (stream == null)
                            throw new FileNotFoundException("找不到补丁资源文件");
                        
                        using (var file = File.Create(tempZipPath))
                        {
                            stream.CopyTo(file);
                        }
                    }

                    // 安全解压ZIP文件
                    using (var archive = ZipFile.OpenRead(tempZipPath))
                    {
                        foreach (var entry in archive.Entries)
                        {
                            // 防止路径遍历攻击
                            string destPath = Path.GetFullPath(Path.Combine(gamePath, entry.FullName));
                            if (!destPath.StartsWith(gamePath, StringComparison.OrdinalIgnoreCase))
                            {
                                throw new SecurityException("检测到非法的文件路径: " + entry.FullName);
                            }

                            // 创建目标目录（如果不存在）
                            string? dirPath = Path.GetDirectoryName(destPath);
                            if (!string.IsNullOrEmpty(dirPath) && !Directory.Exists(dirPath))
                            {
                                Directory.CreateDirectory(dirPath);
                            }

                            // 如果是文件而非目录
                            if (!string.IsNullOrEmpty(entry.Name))
                            {
                                entry.ExtractToFile(destPath, overwrite: true);
                            }
                        }
                    }

                    MessageBox.Show("补丁已成功加载！", "操作完成",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"解压补丁失败: {ex.Message}", "错误",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    // 清理临时文件
                    try
                    {
                        if (File.Exists(tempZipPath))
                            File.Delete(tempZipPath);
                    }
                    catch { }
                }
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show("需要管理员权限来修改游戏文件", "权限不足",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载补丁失败: {ex.Message}", "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        #endregion
    }

    #region 辅助类和对话框

    public struct Aria2ProgressData
    {
        public int Percent;
        public float DownloadedMB;
        public float TotalMB;
        public float SpeedMB;
        
        public bool IsComplete;
        public string? Message;
        public string? WarningMessage;
        public string? ErrorMessage;
        public string? LogMessage;
    }
    
    public class DownloadProgressDialog : Form
    {
        private ProgressBar progressBar;
        private Label statusLabel;
        private Label speedLabel;
        private Label percentLabel;
        private TextBox logBox;
        
        public DownloadProgressDialog()
        {
            InitializeComponent();
        }
        
        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.Text = "下载进度";
            this.ClientSize = new Size(500, 350);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            
            var mainPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 5,
                Padding = new Padding(15)
            };
            
            // 进度条区域
            var progressPanel = new Panel { Height = 50 };
            progressBar = new ProgressBar { Height = 25, Maximum = 100 };
            percentLabel = new Label { Text = "0%", AutoSize = true };
            
            var progressLayout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight
            };
            progressLayout.Controls.Add(progressBar);
            progressLayout.Controls.Add(percentLabel);
            progressPanel.Controls.Add(progressLayout);
            mainPanel.Controls.Add(progressPanel);
            
            // 速度显示
            speedLabel = new Label { Text = "速度: 0 MB/s", Height = 20 };
            mainPanel.Controls.Add(speedLabel);
            
            // 状态显示
            statusLabel = new Label { Text = "准备下载...", Height = 20 };
            mainPanel.Controls.Add(statusLabel);
            
            // 日志区域
            logBox = new TextBox 
            { 
                Multiline = true, 
                ScrollBars = ScrollBars.Vertical,
                Height = 150,
                ReadOnly = true
            };
            mainPanel.Controls.Add(logBox);
            
            // 关闭按钮
            var closeButton = new Button 
            { 
                Text = "关闭", 
                Dock = DockStyle.Bottom,
                DialogResult = DialogResult.Cancel
            };
            closeButton.Click += (sender, e) => this.Close();
            mainPanel.Controls.Add(closeButton);
            
            this.Controls.Add(mainPanel);
            this.ResumeLayout(false);
        }
        
        public void UpdateProgress(Aria2ProgressData data)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => UpdateProgress(data)));
                return;
            }
            
            if (!string.IsNullOrEmpty(data.ErrorMessage))
            {
                logBox.AppendText($"[错误] {data.ErrorMessage}\r\n");
                return;
            }
            
            if (data.Percent > 0)
            {
                progressBar.Value = Math.Min(Math.Max(data.Percent, 0), 100);
                percentLabel.Text = $"{data.Percent}%";
            }
            
            if (data.SpeedMB > 0)
            {
                speedLabel.Text = $"下载速度: {data.SpeedMB:F1} MB/s";
            }
            
            if (data.DownloadedMB > 0 && data.TotalMB > 0)
            {
                statusLabel.Text = $"已下载: {data.DownloadedMB:F1}MB / {data.TotalMB:F1}MB";
            }
            
            if (data.IsComplete && !string.IsNullOrEmpty(data.Message))
            {
                statusLabel.Text = data.Message;
                progressBar.Value = 100;
                logBox.AppendText($"[完成] {data.Message}\r\n");
            }
        }
    }
    
    public static class PromptDialog
    {
        public static string? Show(string title, string prompt, string defaultValue)
        {
            var form = new Form()
            {
                Width = 350,
                Height = 150,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = title,
                StartPosition = FormStartPosition.CenterScreen
            };
            
            var label = new Label() { Left = 20, Top = 20, Text = prompt, AutoSize = true };
            var textBox = new TextBox() { Left = 20, Top = 45, Width = 300, Text = defaultValue };
            var okButton = new Button() { Text = "确定", Left = 180, Width = 70, Top = 80, DialogResult = DialogResult.OK };
            var cancelButton = new Button() { Text = "取消", Left = 260, Width = 70, Top = 80, DialogResult = DialogResult.Cancel };
            
            form.AcceptButton = okButton;
            form.CancelButton = cancelButton;
            
            form.Controls.Add(label);
            form.Controls.Add(textBox);
            form.Controls.Add(okButton);
            form.Controls.Add(cancelButton);
            
            return form.ShowDialog() == DialogResult.OK ? textBox.Text : null;
        }
    }
    
    #endregion
}