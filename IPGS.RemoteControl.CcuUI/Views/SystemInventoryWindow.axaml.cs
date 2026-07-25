using Avalonia.Controls;
using System.Text.Json;

namespace IPGS.RemoteControl.CcuUI.Views;

public partial class SystemInventoryWindow : Window
{
    public SystemInventoryWindow()
    {
        InitializeComponent();
    }

    public void LoadFromJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            if (root.TryGetProperty("error", out var err))
            {
                this.FindControl<TextBlock>("PART_CpuText")!.Text = "Error: " + err.GetString();
                return;
            }

            this.FindControl<TextBlock>("PART_CpuText")!.Text = root.TryGetProperty("cpu", out var c) ? c.GetString() : "N/A";
            this.FindControl<TextBlock>("PART_MemText")!.Text = root.TryGetProperty("memory", out var m) ? m.GetString() : "N/A";
            this.FindControl<TextBlock>("PART_OsText")!.Text = root.TryGetProperty("os", out var o) ? o.GetString() : "N/A";
            this.FindControl<TextBlock>("PART_ArchText")!.Text = root.TryGetProperty("arch", out var a) ? a.GetString() : "N/A";
        }
        catch
        {
            this.FindControl<TextBlock>("PART_CpuText")!.Text = "Failed to parse system info.";
        }
    }
}
