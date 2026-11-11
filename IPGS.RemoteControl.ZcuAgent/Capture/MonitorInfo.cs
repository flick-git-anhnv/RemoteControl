namespace IPGS.RemoteControl.ZcuAgent.Capture;

/// <summary>Một màn hình vật lý trong desktop ảo (toạ độ so với góc trên-trái của khung hình được chụp).</summary>
internal sealed record MonitorInfo(int Index, string Name, int X, int Y, int W, int H);
