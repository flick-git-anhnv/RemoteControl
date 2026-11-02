using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using KztekComponentAvalonia.Theme;

namespace KztekComponentAvalonia.Controls;

public partial class KzDeviceTree : UserControl
{
    // ── Styled Properties ─────────────────────────────────────────────────────

    public static readonly StyledProperty<string> HeaderTextProperty =
        AvaloniaProperty.Register<KzDeviceTree, string>(nameof(HeaderText), "Thiết bị");

    public static readonly StyledProperty<bool> ShowHeaderProperty =
        AvaloniaProperty.Register<KzDeviceTree, bool>(nameof(ShowHeader), false);

    public string HeaderText
    {
        get => GetValue(HeaderTextProperty);
        set => SetValue(HeaderTextProperty, value);
    }

    public bool ShowHeader
    {
        get => GetValue(ShowHeaderProperty);
        set => SetValue(ShowHeaderProperty, value);
    }

    // ── Backing collection ────────────────────────────────────────────────────
    private readonly ObservableCollection<KzDeviceNode> _items = new();

    public ObservableCollection<KzDeviceNode> Items => _items;

    // ── Events ────────────────────────────────────────────────────────────────
    public event EventHandler<KzDeviceNode?>? NodeSelected;
    public event EventHandler<KzDeviceNode?>? NodeDoubleClicked;
    public event EventHandler<KzDeviceNode?>? NodeRightClicked;

    // ── Constructor ───────────────────────────────────────────────────────────
    public KzDeviceTree()
    {
        InitializeComponent();
        _items.CollectionChanged += OnItemsCollectionChanged;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (this.FindControl<TreeView>("PART_Tree") is { } tree)
        {
            tree.ItemsSource = _items;

            tree.SelectionChanged += (_, args) =>
            {
                if (tree.SelectedItem is KzDeviceNode node)
                    NodeSelected?.Invoke(this, node);
            };

            tree.DoubleTapped += (_, _) =>
            {
                if (tree.SelectedItem is KzDeviceNode node)
                    NodeDoubleClicked?.Invoke(this, node);
            };

            tree.PointerReleased += (_, args) =>
            {
                if (args.InitialPressMouseButton != MouseButton.Right) return;
                var hit = (args.Source as Visual)?.GetSelfAndVisualAncestors()
                              .OfType<TreeViewItem>()
                              .FirstOrDefault();
                var node = hit?.DataContext as KzDeviceNode ?? tree.SelectedItem as KzDeviceNode;
                NodeRightClicked?.Invoke(this, node);
            };
        }

        ApplyHeader();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        foreach (var node in _items)
            node.PropertyChanged -= OnNodePropertyChanged;
    }

    // ── Reactive count ────────────────────────────────────────────────────────

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
            foreach (KzDeviceNode n in e.OldItems) n.PropertyChanged -= OnNodePropertyChanged;
        if (e.NewItems != null)
            foreach (KzDeviceNode n in e.NewItems) n.PropertyChanged += OnNodePropertyChanged;
        UpdateOnlineCount();
    }

    private void OnNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(KzDeviceNode.Status)) UpdateOnlineCount();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HeaderTextProperty || change.Property == ShowHeaderProperty)
            ApplyHeader();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Add a root-level device group or device.</summary>
    public KzDeviceNode AddDevice(string name, KzDeviceType type,
        KzDeviceStatus status = KzDeviceStatus.Unknown, string ip = "", string id = "")
    {
        var node = new KzDeviceNode
        {
            Name = name, DeviceType = type, Status = status,
            IpAddress = ip, DeviceId = id
        };
        _items.Add(node);
        return node;
    }

    /// <summary>Add a child device under a parent node.</summary>
    public KzDeviceNode AddDevice(KzDeviceNode parent, string name, KzDeviceType type,
        KzDeviceStatus status = KzDeviceStatus.Unknown, string ip = "", string id = "")
    {
        var node = new KzDeviceNode
        {
            Name = name, DeviceType = type, Status = status,
            IpAddress = ip, DeviceId = id
        };
        parent.Children.Add(node);
        return node;
    }

    /// <summary>Update status on a node; notifies UI via INPC.</summary>
    public void SetStatus(KzDeviceNode node, KzDeviceStatus status) =>
        node.Status = status;

    /// <summary>Expand all root-level group nodes by setting IsExpanded on the model.</summary>
    public void ExpandGroups()
    {
        foreach (var node in _items.Where(n => n.IsGroup))
            node.IsExpanded = true;
    }

    /// <summary>Collapse all root-level group nodes.</summary>
    public void CollapseGroups()
    {
        foreach (var node in _items.Where(n => n.IsGroup))
            node.IsExpanded = false;
    }

    /// <summary>Clear all nodes.</summary>
    public void Clear() => _items.Clear();

    // ── Private ───────────────────────────────────────────────────────────────
    private void ApplyHeader()
    {
        if (this.FindControl<Border>("HeaderArea") is { } hdr)
            hdr.IsVisible = ShowHeader;
        if (this.FindControl<TextBlock>("HeaderLabel") is { } lbl)
            lbl.Text = HeaderText;

        // Update online count
        UpdateOnlineCount();
    }

    private void UpdateOnlineCount()
    {
        if (this.FindControl<TextBlock>("OnlineCount") is not { } tb) return;
        int total  = CountAll(_items);
        int online = CountStatus(_items, KzDeviceStatus.Online);
        tb.Text = total > 0 ? $"{online}/{total} online" : "";
    }

    private static int CountAll(IEnumerable<KzDeviceNode> nodes)
    {
        int n = 0;
        foreach (var node in nodes)
        {
            if (node.ShowStatus) n++;
            n += CountAll(node.Children);
        }
        return n;
    }

    private static int CountStatus(IEnumerable<KzDeviceNode> nodes, KzDeviceStatus status)
    {
        int n = 0;
        foreach (var node in nodes)
        {
            if (node.ShowStatus && node.Status == status) n++;
            n += CountStatus(node.Children, status);
        }
        return n;
    }
}
