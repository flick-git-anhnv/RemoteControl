using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using IPGS.RemoteControl.CcuClient;
using KztekComponentAvalonia.Controls;

namespace IPGS.RemoteControl.CcuUI.Views
{
    public partial class ZcuSetupWizardWindow : Window
    {
        private readonly ZcuRemoteInstallerService _installerService;
        public ComputerProfile? CreatedProfile { get; private set; }

        // Thông tin SSH lấy từ hồ sơ máy tính đã lưu (nhập ở "Thêm/Sửa máy tính") —
        // không còn ô nhập SSH riêng trong wizard này để tránh trùng lặp dữ liệu.
        private readonly string _sshHost;
        private readonly int _sshPort;
        private readonly string _sshUser;
        private readonly string _sshPassword;

        public ZcuSetupWizardWindow() : this(new ComputerProfile())
        {
        }

        public ZcuSetupWizardWindow(ComputerProfile prefill)
        {
            InitializeComponent();
            _installerService = new ZcuRemoteInstallerService();

            _sshHost = prefill.Host;
            _sshPort = prefill.SshPort > 0 ? prefill.SshPort : 22;
            _sshUser = prefill.SshUsername ?? "";
            _sshPassword = prefill.SshPassword ?? "";

            PART_TargetHostText.Text = string.IsNullOrWhiteSpace(_sshHost)
                ? "Đang cài đặt cho: —"
                : $"Đang cài đặt cho: {_sshUser}@{_sshHost}:{_sshPort}";

            PART_BtnGenToken.Click += OnGenTokenClick;
            PART_BtnStartInstall.Click += OnStartInstallClick;

            // Generate initial random token
            GenerateRandomToken();

            if (prefill.Port > 0) PART_AgentPort.Text = prefill.Port.ToString();
            if (!string.IsNullOrWhiteSpace(prefill.Token)) PART_AgentToken.Text = prefill.Token;
        }

        private void GenerateRandomToken()
        {
            byte[] bytes = new byte[16];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            StringBuilder sb = new StringBuilder();
            foreach (byte b in bytes)
                sb.Append(b.ToString("x2"));
            PART_AgentToken.Text = sb.ToString();
        }

        private void OnGenTokenClick(object? sender, RoutedEventArgs e)
        {
            GenerateRandomToken();
        }

        private async void OnStartInstallClick(object? sender, RoutedEventArgs e)
        {
            if (!int.TryParse(PART_AgentPort.Text?.Trim(), out int agentPort)) agentPort = 17600;
            string token = PART_AgentToken.Text?.Trim() ?? "";
            string allowedIps = PART_AllowedIPs.Text?.Trim() ?? "0.0.0.0/0";
            if (!int.TryParse(PART_TargetFps.Text?.Trim(), out int targetFps)) targetFps = 15;
            if (!int.TryParse(PART_JpegQuality.Text?.Trim(), out int jpegQuality)) jpegQuality = 70;

            if (string.IsNullOrEmpty(_sshHost) || string.IsNullOrEmpty(_sshUser) || string.IsNullOrEmpty(token))
            {
                PART_StatusMsg.Foreground = Avalonia.Media.Brushes.Red;
                PART_StatusMsg.Text = "Thiếu IP/SSH user (vào 'Sửa' máy tính để bổ sung) hoặc Token Agent.";
                return;
            }

            PART_BtnStartInstall.IsEnabled = false;
            PART_LogConsole.Text = "";
            PART_ProgressBar.Value = 0;

            // Search for local ZcuAgent publish directory to upload if available
            string? publishDir = FindZcuAgentPublishDir();
            if (publishDir != null)
            {
                Log("📁 Đã tìm thấy thư mục publish ZcuAgent tại: " + publishDir);
            }

            var options = new SshInstallerOptions
            {
                Host = _sshHost,
                SshPort = _sshPort,
                Username = _sshUser,
                Password = _sshPassword,
                AgentPort = agentPort,
                AgentToken = token,
                AllowedClientIPs = allowedIps,
                TargetFps = targetFps,
                JpegQuality = jpegQuality,
                PublishSourceDir = publishDir
            };

            try
            {
                await _installerService.InstallZcuAgentAsync(options, (msg, percent) =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        PART_ProgressBar.Value = percent;
                        PART_ProgressText.Text = $"{percent:0}%";
                        Log(msg);
                    });
                });

                PART_StatusMsg.Foreground = Avalonia.Media.Brushes.Green;
                PART_StatusMsg.Text = "🎉 Cài đặt ZcuAgent hoàn tất!";

                // Save to computer profile if checked
                if (PART_CheckSaveProfile.IsChecked == true)
                {
                    var profile = new ComputerProfile
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Name = $"ZCU ({_sshHost})",
                        Host = _sshHost,
                        Port = agentPort,
                        Token = token,
                        SshPort = _sshPort,
                        SshUsername = _sshUser,
                        SshPassword = _sshPassword,
                        Notes = "Tự động tạo bởi ZCU Setup Wizard",
                        LastConnectedAt = DateTimeOffset.Now
                    };
                    ComputerProfileStore.Instance.Save(profile);
                    CreatedProfile = profile;
                }

                await Task.Delay(1500);
                Close(CreatedProfile);
            }
            catch (Exception ex)
            {
                PART_StatusMsg.Foreground = Avalonia.Media.Brushes.Red;
                PART_StatusMsg.Text = "❌ Cài đặt thất bại: " + ex.Message;
                Log("❌ LỖI: " + ex.Message);
            }
            finally
            {
                PART_BtnStartInstall.IsEnabled = true;
            }
        }

        private string? FindZcuAgentPublishDir()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string candidate1 = Path.Combine(baseDir, "publish", "linux-x64");
            if (Directory.Exists(candidate1) && File.Exists(Path.Combine(candidate1, "IPGS.RemoteControl.ZcuAgent")))
                return candidate1;

            string candidate2 = Path.Combine(Directory.GetCurrentDirectory(), "IPGS.RemoteControl.ZcuAgent", "publish", "linux-x64");
            if (Directory.Exists(candidate2) && File.Exists(Path.Combine(candidate2, "IPGS.RemoteControl.ZcuAgent")))
                return candidate2;

            // Search parent directories up to 3 levels for IPGS.RemoteControl.ZcuAgent/publish/linux-x64
            string? dir = Directory.GetCurrentDirectory();
            for (int i = 0; i < 4 && dir != null; i++)
            {
                string path = Path.Combine(dir, "IPGS.RemoteControl.ZcuAgent", "publish", "linux-x64");
                if (Directory.Exists(path) && File.Exists(Path.Combine(path, "IPGS.RemoteControl.ZcuAgent")))
                    return path;
                dir = Directory.GetParent(dir)?.FullName;
            }

            // Auto-trigger dotnet publish if project exists locally
            string? projDir = Directory.GetCurrentDirectory();
            for (int i = 0; i < 4 && projDir != null; i++)
            {
                string csprojPath = Path.Combine(projDir, "IPGS.RemoteControl.ZcuAgent", "IPGS.RemoteControl.ZcuAgent.csproj");
                if (File.Exists(csprojPath))
                {
                    try
                    {
                        Log("⚙️ Đang tự động chạy 'dotnet publish' để tạo file thực thi ZcuAgent linux-x64...");
                        string outDir = Path.Combine(projDir, "IPGS.RemoteControl.ZcuAgent", "publish", "linux-x64");
                        var psi = new System.Diagnostics.ProcessStartInfo("dotnet", $"publish \"{csprojPath}\" -c Release -r linux-x64 --self-contained false -o \"{outDir}\"")
                        {
                            CreateNoWindow = true,
                            UseShellExecute = false
                        };
                        var proc = System.Diagnostics.Process.Start(psi);
                        proc?.WaitForExit(30000);

                        if (Directory.Exists(outDir) && File.Exists(Path.Combine(outDir, "IPGS.RemoteControl.ZcuAgent")))
                            return outDir;
                    }
                    catch (Exception ex)
                    {
                        Log("⚠️ Không thể tự động publish ZcuAgent: " + ex.Message);
                    }
                }
                projDir = Directory.GetParent(projDir)?.FullName;
            }

            return null;
        }

        private void Log(string text)
        {
            PART_LogConsole.Text += $"[{DateTime.Now:HH:mm:ss}] {text}\n";
            PART_LogConsole.CaretIndex = PART_LogConsole.Text.Length;
        }
    }
}
