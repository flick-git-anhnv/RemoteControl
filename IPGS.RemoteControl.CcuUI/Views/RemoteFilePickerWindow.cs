using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using Renci.SshNet;

namespace IPGS.RemoteControl.CcuUI.Views
{
    /// <summary>
    /// Hộp thoại chọn 1 file ngay trên máy đích (duyệt qua SFTP). Trả về đường dẫn tuyệt đối, hoặc null nếu huỷ.
    /// Dùng ShowDialog&lt;string?&gt;(owner).
    /// </summary>
    public class RemoteFilePickerWindow : Window
    {
        private sealed record Entry(string Name, string FullName, bool IsDirectory)
        {
            public override string ToString() => (IsDirectory ? "📁 " : "📄 ") + Name;
        }

        private readonly string _host, _user, _password;
        private readonly int _port;
        private readonly TextBox _txtPath = new() { IsReadOnly = true, Height = 32, VerticalContentAlignment = VerticalAlignment.Center };
        private readonly ListBox _list = new();
        private readonly TextBlock _status = new() { FontSize = 12, Foreground = Avalonia.Media.Brushes.SlateGray, VerticalAlignment = VerticalAlignment.Center };
        private readonly Button _btnUp = new() { Content = "Lên", Padding = new Thickness(12, 6) };
        private readonly Button _btnSelect = new() { Content = "Chọn file", Padding = new Thickness(16, 8), IsEnabled = false };
        private SftpClient? _sftp;
        private string _currentDir = "/";
        private bool _closing;

        public RemoteFilePickerWindow(string host, int port, string user, string password)
        {
            _host = host; _port = port; _user = user; _password = password;

            Title = "Chọn file trên máy đích";
            Width = 560; Height = 520;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var top = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Margin = new Thickness(12, 12, 12, 6) };
            _btnUp.Margin = new Thickness(0, 0, 8, 0);
            Grid.SetColumn(_txtPath, 1);
            top.Children.Add(_btnUp);
            top.Children.Add(_txtPath);

            _list.Margin = new Thickness(12, 0);

            var btnCancel = new Button { Content = "Huỷ", Padding = new Thickness(16, 8), Margin = new Thickness(0, 0, 8, 0) };
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            buttons.Children.Add(btnCancel);
            buttons.Children.Add(_btnSelect);
            var bottom = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(12) };
            bottom.Children.Add(_status);
            Grid.SetColumn(buttons, 1);
            bottom.Children.Add(buttons);

            var root = new DockPanel();
            DockPanel.SetDock(top, Dock.Top);
            DockPanel.SetDock(bottom, Dock.Bottom);
            root.Children.Add(top);
            root.Children.Add(bottom);
            root.Children.Add(_list);
            Content = root;

            _btnUp.Click += async (_, _) =>
            {
                string norm = IPGS.RemoteControl.CcuClient.ShellQuote.NormalizeSftpPath(_currentDir);
                string parent = IPGS.RemoteControl.CcuClient.ShellQuote.GetParentSftpPath(norm);
                if (string.Equals(parent, norm, StringComparison.Ordinal)) return;
                await LoadAsync(parent);
            };
            _list.SelectionChanged += (_, _) => _btnSelect.IsEnabled = _list.SelectedItem is Entry { IsDirectory: false };
            _list.DoubleTapped += async (_, _) => await ActivateSelectedAsync();
            _btnSelect.Click += (_, _) => { if (_list.SelectedItem is Entry { IsDirectory: false } f) Close(f.FullName); };
            btnCancel.Click += (_, _) => Close(null);

            Opened += async (_, _) => await ConnectAsync();
            Closed += (_, _) =>
            {
                _closing = true;
                var c = _sftp; _sftp = null;
                try { c?.Dispose(); } catch { }
            };
        }

        private async Task ConnectAsync()
        {
            _status.Text = "Đang kết nối SFTP...";
            try
            {
                var client = await Task.Run(() =>
                {
                    var c = new SftpClient(_host, _port, _user, _password);
                    c.Connect();
                    return c;
                });
                if (_closing) { client.Dispose(); return; }
                _sftp = client;
                string workDir = IPGS.RemoteControl.CcuClient.ShellQuote.NormalizeSftpPath(client.WorkingDirectory);
                string home = !string.IsNullOrWhiteSpace(client.WorkingDirectory) && workDir != "/"
                    ? workDir
                    : (string.IsNullOrWhiteSpace(_user) ? "/" : (_user == "root" ? "/root" : $"/home/{_user}"));
                await LoadAsync(home);
            }
            catch (Exception ex)
            {
                _status.Text = "Lỗi kết nối SFTP: " + ex.Message;
            }
        }

        private async Task LoadAsync(string dir)
        {
            var sftp = _sftp;
            if (sftp == null || !sftp.IsConnected) { _status.Text = "Chưa kết nối SFTP."; return; }
            _status.Text = "Đang tải " + dir + "...";
            try
            {
                List<Entry> items = await Task.Run(() => sftp.ListDirectory(dir)
                    .Where(f => f.Name != "." && f.Name != "..")
                    .OrderByDescending(f => f.IsDirectory).ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(f => new Entry(f.Name, f.FullName, f.IsDirectory))
                    .ToList());
                if (_closing) return;
                Dispatcher.UIThread.Post(() =>
                {
                    _currentDir = dir;
                    _txtPath.Text = dir;
                    _list.ItemsSource = items;
                    _status.Text = $"{items.Count} mục. Nhấp đúp để mở thư mục / chọn file.";
                });
            }
            catch (Exception ex)
            {
                _status.Text = "Không mở được thư mục: " + ex.Message;
            }
        }

        private async Task ActivateSelectedAsync()
        {
            if (_list.SelectedItem is not Entry e) return;
            if (e.IsDirectory) await LoadAsync(e.FullName);
            else Close(e.FullName);
        }
    }
}
