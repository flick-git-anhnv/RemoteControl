# STEP 1.1: Sao chép project KztekComponentAvalonia từ 5.BaseUI và làm sạch thư mục tạm bin/obj

## Mục tiêu
Sao chép toàn bộ mã nguồn của `KztekComponentAvalonia` từ `E:\KZTEK\Code_Git\5.BaseUI\KztekComponentAvalonia\KztekComponentAvalonia` vào thư mục gốc của repository `E:\KZTEK\Code_Git\6.RemoteControlTool\KztekComponentAvalonia`. Sau đó loại bỏ hoàn toàn các thư mục rác `bin` và `obj` nếu bị sao chép sang.

## Chi tiết thực hiện
- Sao chép toàn bộ thư mục: Controls, Theme, Themes, logo.jpg, KztekThemeExtension.cs, KztekComponentAvalonia.csproj.
- Xóa thư mục `bin` và `obj` bằng `Remove-Item -Recurse -Force`.
- Kiểm tra tính đầy đủ của các tệp nguồn sau khi chép: toàn bộ 74 tệp mã nguồn và tài nguyên đã được sao chép đầy đủ.

## Trạng thái
- **Hoàn thành:** 2026-11-02 09:17:18
- **Agent:** senior-developer
