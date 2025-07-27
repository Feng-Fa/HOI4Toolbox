using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO.Compression;

namespace HOI4Toolbox
{
    [SupportedOSPlatform("windows")]
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
                    RunModExe();
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

        #region 功能一：下载游戏（使用外部脚本）

        private void StartGameDownload()
        {
            var outputDir = GetDownloadDirectory();
            if (string.IsNullOrEmpty(outputDir))
            {
                MessageBox.Show("无法确定下载目录", "错误", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // 删除旧的完成标记文件（如果存在）
            try
            {
                File.Delete(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "win.txt"));
            }
            catch { }

            var progressDialog = new DownloadProgressDialog();
            progressDialog.Show(this);
            
            // 启动监控线程
            Task.Run(() => MonitorDownloadCompletion(progressDialog, outputDir));
            
            // 启动下载脚本
            Task.Run(() => RunExternalDownloadScript(
                "https://data.alloyhe.top/d/Onedrive/Heart%20of%20Iron%20IV/Vanilla/Windows/Hearts%20of%20Iron%20IV%20v.1.16.9.zip",
                outputDir,
                progressDialog.UpdateProgress
            ));
        }

        private void MonitorDownloadCompletion(DownloadProgressDialog dialog, string outputDir)
        {
            string completionFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "win.txt");
            
            // 检查文件是否存在，最多等待30分钟
            for (int i = 0; i < 1800; i++)
            {
                if (File.Exists(completionFile))
                {
                    // 在主线程执行UI操作
                    this.Invoke(new Action(() =>
                    {
                        dialog.Close();
                        MessageBox.Show("下载已完成！", "完成", 
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                        
                        // 打开下载目录
                        try
                        {
                            Process.Start("explorer.exe", outputDir);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"无法打开下载目录: {ex.Message}", "警告", 
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }));
                    return;
                }
                System.Threading.Thread.Sleep(1000); // 每秒检查一次
            }
            
            // 超时处理
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
                // 提取aria2二进制文件
                string? ariaPath = ExtractAria2Binary();
                if (ariaPath == null)
                {
                    progressHandler?.Invoke(new Aria2ProgressData
                    {
                        ErrorMessage = "无法找到或提取下载工具"
                    });
                    return;
                }
                
                // 将批处理文件提取到应用程序目录（非临时目录）
                string batPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "download_game.bat");
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
                    progressHandler?.Invoke(new Aria2ProgressData
                    {
                        ErrorMessage = $"提取脚本失败: {ex.Message}"
                    });
                    return;
                }

                // 构建参数（不对URL编码）
                string arguments = $"\"{url}\" \"{outputDir}\" \"{ariaPath}\" \"{AppDomain.CurrentDomain.BaseDirectory}\"";

                var startInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",  // 使用cmd.exe作为宿主
                    Arguments = $"/c \"\"{batPath}\" {arguments}\"",  // 双重引号确保路径安全
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Normal,
                    CreateNoWindow = false
                };

                using (var process = new Process { StartInfo = startInfo })
                {
                    process.Start();
                    process.WaitForExit();

                    if (process.ExitCode != 0)
                    {
                        progressHandler?.Invoke(new Aria2ProgressData
                        {
                            ErrorMessage = $"下载失败，错误代码: {process.ExitCode}"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                progressHandler?.Invoke(new Aria2ProgressData
                {
                    ErrorMessage = $"下载失败: {ex.Message}"
                });
            }
            finally
            {
                try
                {
                    // 清理临时文件
                    Directory.Delete(Path.Combine(Path.GetTempPath(), "HOI4Download"), true);
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
                return ExtractResourceToTempFile(resourceName);
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region 功能二：运行MOD程序

        private void RunModExe()
        {
            try
            {
                var modExePath = ExtractResourceToTempFile("mod.exe");
                if (modExePath == null)
                {
                    MessageBox.Show("找不到MOD程序", "错误", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                
                var startInfo = new ProcessStartInfo
                {
                    FileName = modExePath,
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
                MessageBox.Show($"启动MOD程序失败: {ex.Message}", "错误", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion

        #region 功能三：注册模组

        private void RegisterMod()
        {
            var path = PromptDialog.Show("注册模组", "请输入模组安装路径:", "");
            if (string.IsNullOrWhiteSpace(path)) return;

            try
            {
                var batPath = ExtractResourceToTempFile("mod.bat");
                if (batPath == null)
                {
                    MessageBox.Show("找不到模组注册脚本", "错误", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = batPath,
                        WorkingDirectory = path,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                
                MessageBox.Show("模组注册成功", "成功", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            var path = PromptDialog.Show("加载补丁", "请输入游戏安装路径:", "");
            if (string.IsNullOrWhiteSpace(path)) return;

            try
            {
                var zipPath = ExtractResourceToTempFile("buding.zip");
                if (zipPath == null)
                {
                    MessageBox.Show("找不到补丁文件", "错误", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                MessageBox.Show("开始解压补丁...", "处理中", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                
                ZipFile.ExtractToDirectory(zipPath, path, true);
                MessageBox.Show($"成功加载补丁文件", "完成", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"补丁加载失败: {ex.Message}", "错误", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion

        #region 辅助方法

        private string? ExtractResourceToTempFile(string resourceName, string? extension = null)
        {
            try
            {
                Assembly? assembly = Assembly.GetExecutingAssembly();
                if (assembly == null) return null;
                
                var tempPath = Path.GetTempFileName();
                
                if (!string.IsNullOrEmpty(extension))
                {
                    tempPath = Path.ChangeExtension(tempPath, extension);
                }

                string fullResourceName = $"{assembly.GetName().Name}.Resources.{resourceName}";
                using (var stream = assembly.GetManifestResourceStream(fullResourceName))
                {
                    if (stream == null) 
                    {
                        // 列出所有资源帮助调试
                        Debug.WriteLine("可用的资源:");
                        foreach (var name in assembly.GetManifestResourceNames())
                        {
                            Debug.WriteLine(name);
                        }
                        return null;
                    }
                    
                    using (var file = File.Create(tempPath))
                    {
                        stream.CopyTo(file);
                    }
                }
                return tempPath;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"资源提取失败: {ex.Message}", "错误", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
        }

        #endregion
    }

    #region 辅助类和结构体
    
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
            
            // 窗体设置
            this.Text = "下载进度";
            this.ClientSize = new Size(500, 350);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            
            // 主容器
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
            
            if (!string.IsNullOrEmpty(data.WarningMessage))
            {
                logBox.AppendText($"[警告] {data.WarningMessage}\r\n");
                return;
            }
            
            if (!string.IsNullOrEmpty(data.LogMessage))
            {
                logBox.AppendText($"[信息] {data.LogMessage}\r\n");
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
            
            okButton.Click += (sender, e) => { form.Close(); };
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