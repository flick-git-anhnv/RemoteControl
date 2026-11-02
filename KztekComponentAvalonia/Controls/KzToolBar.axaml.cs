using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using System;

namespace KztekComponentAvalonia.Controls;

/// <summary>
/// KZTEK branded toolbar with configurable Add / Edit / Delete / Refresh / Import / Export buttons.
/// Use ShowXxx properties to control visibility; subscribe to XxxClicked events for actions.
/// </summary>
public partial class KzToolBar : UserControl
{
    // ── Styled Properties ─────────────────────────────────────────────────────

    public static readonly StyledProperty<bool> ShowAddProperty =
        AvaloniaProperty.Register<KzToolBar, bool>(nameof(ShowAdd), true);
    public static readonly StyledProperty<bool> ShowEditProperty =
        AvaloniaProperty.Register<KzToolBar, bool>(nameof(ShowEdit), true);
    public static readonly StyledProperty<bool> ShowDeleteProperty =
        AvaloniaProperty.Register<KzToolBar, bool>(nameof(ShowDelete), true);
    public static readonly StyledProperty<bool> ShowRefreshProperty =
        AvaloniaProperty.Register<KzToolBar, bool>(nameof(ShowRefresh), true);
    public static readonly StyledProperty<bool> ShowImportProperty =
        AvaloniaProperty.Register<KzToolBar, bool>(nameof(ShowImport), false);
    public static readonly StyledProperty<bool> ShowExportProperty =
        AvaloniaProperty.Register<KzToolBar, bool>(nameof(ShowExport), false);
    public static readonly StyledProperty<bool> ShowBulkDeleteProperty =
        AvaloniaProperty.Register<KzToolBar, bool>(nameof(ShowBulkDelete), false);
    public static readonly StyledProperty<bool> IsBulkDeleteEnabledProperty =
        AvaloniaProperty.Register<KzToolBar, bool>(nameof(IsBulkDeleteEnabled), false);

    public static readonly StyledProperty<string> AddLabelProperty =
        AvaloniaProperty.Register<KzToolBar, string>(nameof(AddLabel), "Thêm");
    public static readonly StyledProperty<string> EditLabelProperty =
        AvaloniaProperty.Register<KzToolBar, string>(nameof(EditLabel), "Sửa");
    public static readonly StyledProperty<string> DeleteLabelProperty =
        AvaloniaProperty.Register<KzToolBar, string>(nameof(DeleteLabel), "Xóa");
    public static readonly StyledProperty<string> RefreshLabelProperty =
        AvaloniaProperty.Register<KzToolBar, string>(nameof(RefreshLabel), "Làm mới");
    public static readonly StyledProperty<string> ImportLabelProperty =
        AvaloniaProperty.Register<KzToolBar, string>(nameof(ImportLabel), "Import");
    public static readonly StyledProperty<string> ExportLabelProperty =
        AvaloniaProperty.Register<KzToolBar, string>(nameof(ExportLabel), "Export");
    public static readonly StyledProperty<string> BulkDeleteLabelProperty =
        AvaloniaProperty.Register<KzToolBar, string>(nameof(BulkDeleteLabel), "Xóa nhiều");

    public bool ShowAdd          { get => GetValue(ShowAddProperty);          set => SetValue(ShowAddProperty, value); }
    public bool ShowEdit         { get => GetValue(ShowEditProperty);         set => SetValue(ShowEditProperty, value); }
    public bool ShowDelete       { get => GetValue(ShowDeleteProperty);       set => SetValue(ShowDeleteProperty, value); }
    public bool ShowRefresh      { get => GetValue(ShowRefreshProperty);      set => SetValue(ShowRefreshProperty, value); }
    public bool ShowImport       { get => GetValue(ShowImportProperty);       set => SetValue(ShowImportProperty, value); }
    public bool ShowExport       { get => GetValue(ShowExportProperty);       set => SetValue(ShowExportProperty, value); }
    public bool ShowBulkDelete   { get => GetValue(ShowBulkDeleteProperty);   set => SetValue(ShowBulkDeleteProperty, value); }
    public bool IsBulkDeleteEnabled { get => GetValue(IsBulkDeleteEnabledProperty); set => SetValue(IsBulkDeleteEnabledProperty, value); }

    public string AddLabel        { get => GetValue(AddLabelProperty);        set => SetValue(AddLabelProperty, value); }
    public string EditLabel       { get => GetValue(EditLabelProperty);       set => SetValue(EditLabelProperty, value); }
    public string DeleteLabel     { get => GetValue(DeleteLabelProperty);     set => SetValue(DeleteLabelProperty, value); }
    public string RefreshLabel    { get => GetValue(RefreshLabelProperty);    set => SetValue(RefreshLabelProperty, value); }
    public string ImportLabel     { get => GetValue(ImportLabelProperty);     set => SetValue(ImportLabelProperty, value); }
    public string ExportLabel     { get => GetValue(ExportLabelProperty);     set => SetValue(ExportLabelProperty, value); }
    public string BulkDeleteLabel { get => GetValue(BulkDeleteLabelProperty); set => SetValue(BulkDeleteLabelProperty, value); }

    // ── Events ─────────────────────────────────────────────────────────────────

    public event EventHandler? AddClicked;
    public event EventHandler? EditClicked;
    public event EventHandler? DeleteClicked;
    public event EventHandler? RefreshClicked;
    public event EventHandler? ImportClicked;
    public event EventHandler? ExportClicked;
    public event EventHandler? BulkDeleteClicked;

    // ── Privates ───────────────────────────────────────────────────────────────

    private KzButton?    _btnAdd, _btnEdit, _btnDelete, _btnRefresh, _btnImport, _btnExport, _btnBulkDelete;
    private TextBlock?   _lblAdd, _lblEdit, _lblDelete, _lblRefresh, _lblImport, _lblExport, _lblBulkDelete;

    private EventHandler<RoutedEventArgs>? _onAddClick;
    private EventHandler<RoutedEventArgs>? _onEditClick;
    private EventHandler<RoutedEventArgs>? _onDeleteClick;
    private EventHandler<RoutedEventArgs>? _onRefreshClick;
    private EventHandler<RoutedEventArgs>? _onImportClick;
    private EventHandler<RoutedEventArgs>? _onExportClick;
    private EventHandler<RoutedEventArgs>? _onBulkDeleteClick;

    public KzToolBar() => InitializeComponent();

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _btnAdd        = this.FindControl<KzButton>  ("PART_BtnAdd")!;
        _btnEdit       = this.FindControl<KzButton>  ("PART_BtnEdit")!;
        _btnDelete     = this.FindControl<KzButton>  ("PART_BtnDelete")!;
        _btnRefresh    = this.FindControl<KzButton>  ("PART_BtnRefresh")!;
        _btnImport     = this.FindControl<KzButton>  ("PART_BtnImport")!;
        _btnExport     = this.FindControl<KzButton>  ("PART_BtnExport")!;
        _btnBulkDelete = this.FindControl<KzButton>  ("PART_BtnBulkDelete")!;

        _lblAdd        = this.FindControl<TextBlock> ("PART_LblAdd")!;
        _lblEdit       = this.FindControl<TextBlock> ("PART_LblEdit")!;
        _lblDelete     = this.FindControl<TextBlock> ("PART_LblDelete")!;
        _lblRefresh    = this.FindControl<TextBlock> ("PART_LblRefresh")!;
        _lblImport     = this.FindControl<TextBlock> ("PART_LblImport")!;
        _lblExport     = this.FindControl<TextBlock> ("PART_LblExport")!;
        _lblBulkDelete = this.FindControl<TextBlock> ("PART_LblBulkDelete")!;

        _onAddClick        = (_, _) => AddClicked?.Invoke(this, EventArgs.Empty);
        _onEditClick       = (_, _) => EditClicked?.Invoke(this, EventArgs.Empty);
        _onDeleteClick     = (_, _) => DeleteClicked?.Invoke(this, EventArgs.Empty);
        _onRefreshClick    = (_, _) => RefreshClicked?.Invoke(this, EventArgs.Empty);
        _onImportClick     = (_, _) => ImportClicked?.Invoke(this, EventArgs.Empty);
        _onExportClick     = (_, _) => ExportClicked?.Invoke(this, EventArgs.Empty);
        _onBulkDeleteClick = (_, _) => BulkDeleteClicked?.Invoke(this, EventArgs.Empty);

        _btnAdd.Click        += _onAddClick;
        _btnEdit.Click       += _onEditClick;
        _btnDelete.Click     += _onDeleteClick;
        _btnRefresh.Click    += _onRefreshClick;
        _btnImport.Click     += _onImportClick;
        _btnExport.Click     += _onExportClick;
        _btnBulkDelete.Click += _onBulkDeleteClick;

        SyncAll();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_btnAdd        is not null) _btnAdd.Click        -= _onAddClick;
        if (_btnEdit       is not null) _btnEdit.Click       -= _onEditClick;
        if (_btnDelete     is not null) _btnDelete.Click     -= _onDeleteClick;
        if (_btnRefresh    is not null) _btnRefresh.Click    -= _onRefreshClick;
        if (_btnImport     is not null) _btnImport.Click     -= _onImportClick;
        if (_btnExport     is not null) _btnExport.Click     -= _onExportClick;
        if (_btnBulkDelete is not null) _btnBulkDelete.Click -= _onBulkDeleteClick;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        SyncAll();
    }

    private void SyncAll()
    {
        if (_btnAdd is null) return;

        _btnAdd.IsVisible                                        = ShowAdd;
        if (_btnEdit       is not null) _btnEdit.IsVisible       = ShowEdit;
        if (_btnDelete     is not null) _btnDelete.IsVisible     = ShowDelete;
        if (_btnRefresh    is not null) _btnRefresh.IsVisible    = ShowRefresh;
        if (_btnImport     is not null) _btnImport.IsVisible     = ShowImport;
        if (_btnExport     is not null) _btnExport.IsVisible     = ShowExport;
        if (_btnBulkDelete is not null)
        {
            _btnBulkDelete.IsVisible  = ShowBulkDelete;
            _btnBulkDelete.IsEnabled  = IsBulkDeleteEnabled;
        }

        if (_lblAdd        is not null) _lblAdd.Text        = AddLabel;
        if (_lblEdit       is not null) _lblEdit.Text       = EditLabel;
        if (_lblDelete     is not null) _lblDelete.Text     = DeleteLabel;
        if (_lblRefresh    is not null) _lblRefresh.Text    = RefreshLabel;
        if (_lblImport     is not null) _lblImport.Text     = ImportLabel;
        if (_lblExport     is not null) _lblExport.Text     = ExportLabel;
        if (_lblBulkDelete is not null) _lblBulkDelete.Text = BulkDeleteLabel;
    }
}
