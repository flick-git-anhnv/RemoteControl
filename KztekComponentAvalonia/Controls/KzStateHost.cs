using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Threading;

namespace KztekComponentAvalonia.Controls;

/// <summary>
/// Khung chọn View theo TRẠNG THÁI của ViewModel: mỗi trạng thái là một View riêng (không còn một View chứa nhiều trạng thái
/// bật/tắt bằng IsVisible). Lớp dẫn xuất khai báo thuộc tính trạng thái của ViewModel (<see cref="StateProperty"/>), cách đọc trạng thái
/// (<see cref="GetState"/>) và View tương ứng (<see cref="CreateStateView"/>); khi thuộc tính đổi, khung tự thay View.
/// View trạng thái dùng chung DataContext với khung. Mỗi View trạng thái xem trước / kiểm tra được độc lập.
/// </summary>
public abstract class KzStateHost : ContentControl
{
    private INotifyPropertyChanged? _source;
    private object? _currentState;

    /// <summary>Tên thuộc tính của ViewModel báo trạng thái đổi.</summary>
    protected abstract string StateProperty { get; }

    /// <summary>Đọc trạng thái hiện tại từ ViewModel (DataContext có thể null lúc xem trước).</summary>
    protected abstract object GetState(object? viewModel);

    /// <summary>Dựng View cho một trạng thái.</summary>
    protected abstract Control CreateStateView(object state);

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        Subscribe();
        Refresh();
    }

    protected override void OnAttachedToVisualTree(global::Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Subscribe();
        Refresh();
    }

    protected override void OnDetachedFromVisualTree(global::Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Unsubscribe();
    }

    private void Subscribe()
    {
        Unsubscribe();
        _source = DataContext as INotifyPropertyChanged;
        if (_source is not null) _source.PropertyChanged += OnViewModelChanged;
    }

    private void Unsubscribe()
    {
        if (_source is not null) _source.PropertyChanged -= OnViewModelChanged;
        _source = null;
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == StateProperty)
            Dispatcher.UIThread.Post(Refresh);
    }

    /// <summary>Thay View nếu trạng thái đã đổi (cùng trạng thái thì giữ nguyên View).</summary>
    protected void Refresh()
    {
        var state = GetState(DataContext);
        if (Content is not null && Equals(state, _currentState)) return;
        _currentState = state;
        Content = CreateStateView(state);
    }
}
