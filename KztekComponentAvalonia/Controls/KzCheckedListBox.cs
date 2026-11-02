using Avalonia.Controls;

namespace KztekComponentAvalonia.Controls;

/// <summary>
/// KZTEK branded CheckedListBox — multi-select list với checkbox Navy/Orange.
/// Mỗi item hiển thị indicator checkbox: Navy900 khi chọn, border khi chưa.
/// Styling defined in Themes/KzCheckedListBox.axaml.
/// </summary>
public class KzCheckedListBox : ListBox
{
    public KzCheckedListBox()
    {
        SelectionMode = SelectionMode.Multiple | SelectionMode.Toggle;
    }
}
