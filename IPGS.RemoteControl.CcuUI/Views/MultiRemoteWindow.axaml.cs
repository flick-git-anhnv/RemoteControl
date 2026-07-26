using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using IPGS.RemoteControl.CcuClient;
using IPGS.RemoteControl.CcuUI.ViewModels;
using KztekComponentAvalonia.Controls;

namespace IPGS.RemoteControl.CcuUI.Views;

/// <summary>
/// Cửa sổ Multi-Remote Dashboard cho phép theo dõi và điều khiển đồng thời nhiều máy tính ZCU
/// ở chế độ Lưới (tùy chỉnh số Hàng x Cột) hoặc Thẻ Tab (Multi-Tab).
/// </summary>
public partial class MultiRemoteWindow : Window
{
    private class RemoteSessionItem
    {
        public string Host { get; set; } = "";
        public int Port { get; set; }
        public string Token { get; set; } = "";
        public string Title { get; set; } = "";
        public RemoteScreenViewModel ViewModel { get; set; } = null!;
        public RemoteScreenControl Control { get; set; } = null!;
        public Border ContainerCell { get; set; } = null!;
        public TabItem? TabItem { get; set; }
    }

    private readonly List<RemoteSessionItem> _sessions = new();
    private int _gridRows = 2;
    private int _gridColumns = 2;
    private bool _isTabViewMode = false;

    public MultiRemoteWindow()
    {
        InitializeComponent();

        if (this.FindControl<KzButton>("PART_BtnGrid2x2") is { } btn2x2)
            btn2x2.Click += (_, _) => SetViewMode(isTab: false, rows: 2, cols: 2);

        if (this.FindControl<KzButton>("PART_BtnGrid3x3") is { } btn3x3)
            btn3x3.Click += (_, _) => SetViewMode(isTab: false, rows: 3, cols: 3);

        if (this.FindControl<KzButton>("PART_BtnApplyCustomGrid") is { } btnApplyCustom)
            btnApplyCustom.Click += (_, _) => ApplyCustomGridDimensions();

        if (this.FindControl<KzButton>("PART_BtnTabView") is { } btnTab)
            btnTab.Click += (_, _) => SetViewMode(isTab: true, rows: _gridRows, cols: _gridColumns);

        if (this.FindControl<KzButton>("PART_BtnAddSession") is { } btnAdd)
            btnAdd.Click += OnAddSessionClick;

        if (this.FindControl<KzButton>("PART_BtnCloseAll") is { } btnCloseAll)
            btnCloseAll.Click += (_, _) => CloseAllSessions();

        // Q19: tab ẩn không cần decode JPEG full-rate — cập nhật cờ pause mỗi khi đổi tab.
        if (this.FindControl<TabControl>("PART_TabControl") is { } tabCtl)
            tabCtl.SelectionChanged += (_, _) => UpdateRenderPauseStates();

        Closed += OnWindowClosed;
    }

    public void AddSessions(IEnumerable<ComputerProfile> profiles)
    {
        foreach (var profile in profiles)
        {
            AddSession(profile.Host, profile.Port, profile.Token, profile.Name);
        }
    }

    public void AddSession(string host, int port, string token, string? name)
    {
        string displayTitle = !string.IsNullOrWhiteSpace(name) ? $"{name} ({host})" : host;

        var vm = new RemoteScreenViewModel();
        var control = new RemoteScreenControl
        {
            DataContext = vm,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        // Q19: chấm trạng thái phải bind vào trạng thái kết nối THẬT của session —
        // bản cũ dùng emoji "🟢" tĩnh nên luôn xanh kể cả khi Faulted. Dùng ký tự "●"
        // (glyph thường, ăn Foreground) bind vào StatusBrush của ViewModel.
        var statusDot = new TextBlock
        {
            Text = "●",
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center
        };
        statusDot.Bind(TextBlock.ForegroundProperty,
            new Avalonia.Data.Binding(nameof(RemoteScreenViewModel.StatusBrush)) { Source = vm });
        statusDot.Bind(ToolTip.TipProperty,
            new Avalonia.Data.Binding(nameof(RemoteScreenViewModel.StatusText)) { Source = vm });

        // Create Grid Cell Container with header bar
        var cellHeader = new Border
        {
            Background = SolidColorBrush.Parse("#2B2B3D"),
            Padding = new Avalonia.Thickness(8, 4),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                Children =
                {
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 6,
                        VerticalAlignment = VerticalAlignment.Center,
                        Children =
                        {
                            statusDot,
                            new TextBlock
                            {
                                Text = displayTitle,
                                Foreground = Brushes.White,
                                FontSize = 12,
                                FontWeight = FontWeight.Bold,
                                VerticalAlignment = VerticalAlignment.Center
                            }
                        }
                    },
                    new Button
                    {
                        Content = "✕",
                        Padding = new Avalonia.Thickness(6, 2),
                        Background = Brushes.Transparent,
                        Foreground = SolidColorBrush.Parse("#FF6B6B"),
                        FontSize = 11,
                        Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
                        [Grid.ColumnProperty] = 1
                    }
                }
            }
        };

        var cellBorder = new Border
        {
            Background = SolidColorBrush.Parse("#11111B"),
            BorderBrush = SolidColorBrush.Parse("#313244"),
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new Avalonia.CornerRadius(6),
            Margin = new Avalonia.Thickness(4),
            Child = new Grid
            {
                RowDefinitions = new RowDefinitions("Auto,*"),
                Children =
                {
                    cellHeader,
                    new Border
                    {
                        [Grid.RowProperty] = 1,
                        Child = control
                    }
                }
            }
        };

        var closeButton = (Button)((Grid)cellHeader.Child).Children[1];

        var session = new RemoteSessionItem
        {
            Host = host,
            Port = port,
            Token = token,
            Title = displayTitle,
            ViewModel = vm,
            Control = control,
            ContainerCell = cellBorder
        };

        closeButton.Click += (_, _) => RemoveSession(session);

        _sessions.Add(session);

        // Start Remote Connection
        _ = vm.ConnectAsync(host, port, token);

        RefreshLayout();
    }

    private void RemoveSession(RemoteSessionItem session)
    {
        session.ViewModel.Dispose();
        _sessions.Remove(session);
        RefreshLayout();
    }

    private void CloseAllSessions()
    {
        foreach (var s in _sessions.ToList())
        {
            s.ViewModel.Dispose();
        }
        _sessions.Clear();
        RefreshLayout();
    }

    private void ApplyCustomGridDimensions()
    {
        string rText = this.FindControl<KzTextBox>("PART_RowsInput")?.Text?.Trim() ?? "2";
        string cText = this.FindControl<KzTextBox>("PART_ColsInput")?.Text?.Trim() ?? "2";

        int rows = int.TryParse(rText, out int r) && r > 0 ? r : 2;
        int cols = int.TryParse(cText, out int c) && c > 0 ? c : 2;

        SetViewMode(isTab: false, rows: rows, cols: cols);
    }

    private void SetViewMode(bool isTab, int rows, int cols)
    {
        _isTabViewMode = isTab;
        _gridRows = rows;
        _gridColumns = cols;

        if (this.FindControl<KzTextBox>("PART_RowsInput") is { } rBox)
            rBox.Text = rows.ToString();

        if (this.FindControl<KzTextBox>("PART_ColsInput") is { } cBox)
            cBox.Text = cols.ToString();

        if (this.FindControl<KzButton>("PART_BtnGrid2x2") is { } btn2x2)
            btn2x2.Classes.Set("kz-primary", !isTab && rows == 2 && cols == 2);

        if (this.FindControl<KzButton>("PART_BtnGrid3x3") is { } btn3x3)
            btn3x3.Classes.Set("kz-primary", !isTab && rows == 3 && cols == 3);

        if (this.FindControl<KzButton>("PART_BtnTabView") is { } btnTab)
            btnTab.Classes.Set("kz-primary", isTab);

        RefreshLayout();
    }

    private static void DetachControl(Control control)
    {
        if (control.Parent is Panel panel)
        {
            panel.Children.Remove(control);
        }
        else if (control.Parent is ContentControl cc)
        {
            cc.Content = null;
        }
        else if (control.Parent is ContentPresenter cp)
        {
            cp.Content = null;
        }
    }

    private void RefreshLayout()
    {
        if (this.FindControl<TextBlock>("PART_SessionCountText") is { } countText)
        {
            countText.Text = $"{_sessions.Count} phiên đang kết nối";
        }

        if (this.FindControl<StackPanel>("PART_EmptyState") is { } emptyState)
        {
            emptyState.IsVisible = _sessions.Count == 0;
        }

        var gridScroll = this.FindControl<ScrollViewer>("PART_GridScrollViewer");
        var tabControl = this.FindControl<TabControl>("PART_TabControl");
        var uniformGrid = this.FindControl<UniformGrid>("PART_UniformGrid");

        // Step 1: Detach all controls cleanly from previous containers (TabControl or UniformGrid)
        if (tabControl != null)
        {
            foreach (var item in tabControl.Items.OfType<TabItem>())
            {
                item.Content = null;
            }
            tabControl.Items.Clear();
        }

        if (uniformGrid != null)
        {
            uniformGrid.Children.Clear();
        }

        foreach (var session in _sessions)
        {
            DetachControl(session.ContainerCell);
            session.TabItem = null;
        }

        // Step 2: Show appropriate container
        if (gridScroll != null) gridScroll.IsVisible = !_isTabViewMode && _sessions.Count > 0;
        if (tabControl != null) tabControl.IsVisible = _isTabViewMode && _sessions.Count > 0;

        // Step 3: Populate Grid or Tab view
        if (!_isTabViewMode && uniformGrid != null)
        {
            uniformGrid.Rows = _gridRows;
            uniformGrid.Columns = _gridColumns;

            foreach (var session in _sessions)
            {
                uniformGrid.Children.Add(session.ContainerCell);
            }
        }
        else if (_isTabViewMode && tabControl != null)
        {
            foreach (var session in _sessions)
            {
                var tabItem = new TabItem
                {
                    Header = session.Title,
                    Content = session.ContainerCell
                };
                session.TabItem = tabItem;
                tabControl.Items.Add(tabItem);
            }
        }

        UpdateRenderPauseStates();
    }

    /// <summary>
    /// Q19: session ở tab ẩn vẫn nhận frame TCP nhưng KHÔNG decode JPEG + alloc
    /// WriteableBitmap (SkiaSharp decode full-rate × N session rất tốn CPU/GC).
    /// Grid mode: mọi cell đều hiển thị → không pause session nào.
    /// </summary>
    private void UpdateRenderPauseStates()
    {
        var tabControl = this.FindControl<TabControl>("PART_TabControl");
        var selected = tabControl?.SelectedItem as TabItem;

        foreach (var session in _sessions)
        {
            session.ViewModel.IsRenderPaused =
                _isTabViewMode && selected != null && !ReferenceEquals(session.TabItem, selected);
        }
    }

    private async void OnAddSessionClick(object? sender, RoutedEventArgs e)
    {
        // A6: KHÔNG mở ConnectionEntryWindow bằng ShowDialog — đó là main window, không
        // bao giờ Close(result) nên dialog không trả gì về; bấm "Kết nối" trong đó chỉ mở
        // RemoteScreenWindow độc lập, không thêm session nào vào grid. Dùng dialog chọn
        // máy chuyên dụng (SessionPickerWindow) trả về danh sách profile đã chọn.
        var picker = new SessionPickerWindow(_sessions.Select(s => s.Host));
        var picked = await picker.ShowDialog<List<ComputerProfile>?>(this);
        if (picked is { Count: > 0 })
        {
            AddSessions(picked);
        }
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        CloseAllSessions();
    }
}
