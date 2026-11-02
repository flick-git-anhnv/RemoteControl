# STEP 2.1: Cập nhật ProjectReference trong CcuUI và tạo Solution .sln liên kết 5 projects

## Mục tiêu
- Sửa file `IPGS.RemoteControl.CcuUI/IPGS.RemoteControl.CcuUI.csproj` để trỏ vào `..\KztekComponentAvalonia\KztekComponentAvalonia.csproj` thay vì đường dẫn ra ngoài thư mục `..\..\5.BaseUI`.
- Tạo file solution `IPGS.RemoteControl.sln` bằng `dotnet new sln -n IPGS.RemoteControl`.
- Thêm toàn bộ 5 project vào solution:
  1. `IPGS.RemoteControl.CcuClient\IPGS.RemoteControl.CcuClient.csproj`
  2. `IPGS.RemoteControl.ZcuAgent\IPGS.RemoteControl.ZcuAgent.csproj`
  3. `IPGS.RemoteControl.CcuUI\IPGS.RemoteControl.CcuUI.csproj`
  4. `IPGS.RemoteControl.Tests\IPGS.RemoteControl.Tests.csproj`
  5. `KztekComponentAvalonia\KztekComponentAvalonia.csproj`

## Trạng thái
- **Hoàn thành:** 2026-11-02 09:17:48
- **Agent:** senior-developer
