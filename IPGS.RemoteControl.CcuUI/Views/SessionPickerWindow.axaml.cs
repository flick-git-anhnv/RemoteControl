using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using IPGS.RemoteControl.CcuClient;

namespace IPGS.RemoteControl.CcuUI.Views;

/// <summary>
/// Dialog chọn máy gọn nhẹ cho MultiRemoteWindow (fix A6).
/// <para>
/// Đọc danh sách từ <see cref="ComputerProfileStore"/>, cho chọn nhiều máy, trả về
/// <c>List&lt;ComputerProfile&gt;</c> qua <c>ShowDialog&lt;List&lt;ComputerProfile&gt;?&gt;</c>
/// (null nếu hủy). KHÔNG tái dùng <see cref="ConnectionEntryWindow"/> làm dialog —
/// window đó là main window, không bao giờ Close(result) nên ShowDialog không nhận
/// được gì, và mở nó lần 2 gây rối UX (2 danh sách máy giống hệt nhau cùng tồn tại).
/// </para>
/// </summary>
public partial class SessionPickerWindow : Window
{
    public SessionPickerWindow() : this(Array.Empty<string>())
    {
    }

    /// <param name="excludeHosts">Host đã có sẵn trong Dashboard — ẩn khỏi danh sách để tránh thêm trùng.</param>
    public SessionPickerWindow(IEnumerable<string> excludeHosts)
    {
        InitializeComponent();

        var exclude = new HashSet<string>(excludeHosts, StringComparer.OrdinalIgnoreCase);
        var profiles = ComputerProfileStore.Instance.GetAll()
            .Where(p => !string.IsNullOrWhiteSpace(p.Host) && !exclude.Contains(p.Host))
            .OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        PART_ProfileList.ItemsSource = profiles;
        PART_EmptyHint.IsVisible = profiles.Count == 0;
        PART_BtnAdd.IsEnabled = profiles.Count > 0;

        PART_BtnAdd.Click += (_, _) =>
        {
            var picked = PART_ProfileList.SelectedItems?
                .OfType<ComputerProfile>()
                .ToList();
            Close(picked is { Count: > 0 } ? picked : null);
        };

        PART_BtnCancel.Click += (_, _) => Close(null);

        // Double-click 1 dòng = chọn nhanh máy đó
        PART_ProfileList.DoubleTapped += (_, _) =>
        {
            if (PART_ProfileList.SelectedItem is ComputerProfile single)
                Close(new List<ComputerProfile> { single });
        };
    }
}
