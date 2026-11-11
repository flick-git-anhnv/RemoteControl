@C:/Users/nguye/Desktop/Claude-Git/claude/CLAUDE.md
@C:/Users/nguye/Desktop/Claude-Git/claude/RULES.md
@C:/Users/nguye/Desktop/Claude-Git/claude/WORKFLOW.md

## Nguyên tắc code (Karpathy Guidelines — github.com/multica-ai/andrej-karpathy-skills)
Mọi agent khi viết/sửa/review code trong repo này PHẢI áp dụng (có skill `karpathy-guidelines` thì nạp skill đó):
1. Nghĩ trước khi code — nêu rõ giả định, có nhiều cách hiểu thì hỏi, không tự chọn im lặng.
2. Đơn giản trước — code tối thiểu giải quyết đúng yêu cầu, không thêm tính năng/abstraction/cấu hình không được yêu cầu.
3. Sửa đúng chỗ — chỉ đụng dòng cần thiết, giữ style + line ending sẵn có, không refactor/dọn code không liên quan (thấy thì báo).
4. Chạy theo mục tiêu kiểm chứng được — đặt tiêu chí (test/build/log), lặp đến khi xác minh xong mới báo hoàn thành.

## Build lại app CCU (Windows) sau khi sửa code
Sau MỌI thay đổi code CcuUI / CcuClient / KztekComponentAvalonia, phải build lại bằng script ở thư mục gốc repo:
- `build.bat` — gọi `build-app.ps1`: tự đóng app CcuUI đang chạy (tránh khoá file) → `dotnet build IPGS.RemoteControl.sln -c Debug`.
  EXE: `IPGS.RemoteControl.CcuUI\bin\Debug\net8.0\IPGS.RemoteControl.CcuUI.exe`
- `build.bat -Run` — build xong tự mở app để kiểm tra.
- `build.bat -Release` — build Release + `dotnet publish` win-x64 (framework-dependent) ra `IPGS.RemoteControl.CcuUI\bin\publish\win-x64\`.
- Lỗi build → script trả mã lỗi ≠ 0, in "BUILD THAT BAI". Chỉ báo "xong" khi build thành công.
- Lưu ý: tham số `-SkipTests` có khai báo nhưng script hiện KHÔNG chạy test; muốn chạy test: `dotnet test IPGS.RemoteControl.sln`.
- Agent chạy trong môi trường Linux/cloud (không có dotnet/PowerShell, NuGet bị chặn) KHÔNG tự build được — phải nhờ người dùng chạy `build.bat` (hoặc điều khiển máy Windows) rồi đọc lỗi build.
