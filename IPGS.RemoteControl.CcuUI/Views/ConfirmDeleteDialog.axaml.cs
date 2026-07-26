using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using KztekComponentAvalonia.Controls;

namespace IPGS.RemoteControl.CcuUI.Views;

/// <summary>
/// Dialog xác nhận xóa file/thư mục remote (Q14) — hiển thị rõ danh sách đường dẫn sẽ bị xóa
/// và cảnh báo riêng khi có thư mục (xóa đệ quy rm -rf). Trả về true khi user xác nhận.
/// Dùng chung cho FileManagerWindow và RemoteCommandWindow (tab File).
/// </summary>
public partial class ConfirmDeleteDialog : Window
{
    private bool _confirmed;

    public ConfirmDeleteDialog()
    {
        InitializeComponent();
    }

    public ConfirmDeleteDialog(IReadOnlyList<string> paths, bool hasDirectory) : this()
    {
        if (this.FindControl<TextBlock>("PART_ItemList") is { } itemList)
        {
            // Giới hạn hiển thị 30 dòng đầu để dialog không phình vô hạn
            const int maxShown = 30;
            var shown = paths.Take(maxShown).ToList();
            string text = string.Join("\n", shown);
            if (paths.Count > maxShown)
                text += $"\n… và {paths.Count - maxShown} mục khác";
            itemList.Text = text;
        }

        if (this.FindControl<TextBlock>("PART_DirWarning") is { } dirWarning)
            dirWarning.IsVisible = hasDirectory;

        if (this.FindControl<KzButton>("PART_BtnCancel") is { } btnCancel)
            btnCancel.Click += (_, _) => { _confirmed = false; Close(false); };

        if (this.FindControl<KzButton>("PART_BtnConfirm") is { } btnConfirm)
            btnConfirm.Click += (_, _) => { _confirmed = true; Close(true); };

        Closing += (_, _) => { /* đóng bằng X = hủy (mặc định _confirmed = false) */ };
    }

    /// <summary>Hiển thị dialog và trả về true nếu user bấm xác nhận xóa.</summary>
    public static async Task<bool> ShowAsync(Window owner, IReadOnlyList<string> paths, bool hasDirectory)
    {
        var dlg = new ConfirmDeleteDialog(paths, hasDirectory);
        var result = await dlg.ShowDialog<bool?>(owner);
        return result == true && dlg._confirmed;
    }
}
