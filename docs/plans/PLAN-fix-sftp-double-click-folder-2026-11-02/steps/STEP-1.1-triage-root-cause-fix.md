---
step: 1.1
plan: ../PLAN-MASTER.md
agent: senior-developer
status: done
completed_at: 2026-11-02 08:55:28
deps: []
---

# STEP 1.1 — Triage Root Cause & Sửa Logic DoubleTapped + KeyDown trên ListBox SFTP

## Input nhận
- Ảnh chụp màn hình từ người dùng mô tả lỗi không mở được thư mục con khi nhấn đúp chuột trong tab SFTP của `RemoteCommandWindow`.
- File `IPGS.RemoteControl.CcuUI/Views/RemoteCommandWindow.axaml` và `RemoteCommandWindow.axaml.cs`.

## Nhiệm vụ
Phân tích nguyên nhân gốc rễ sự kiện `DoubleTapped` không kích hoạt `LoadDirectoryAsync`, sửa chữa logic trích xuất `RemoteFileItem` từ `e.Source` / `sender` / `SelectedItem`, bổ sung phím tắt `Enter` mở thư mục và `Backspace` quay lại thư mục cha.

## Definition of Done
- [x] Xác định chính xác root cause: `sender` trong `OnFileItemDoubleTapped` là `ListBox`, có `DataContext` là null/Window nên điều kiện `if (sender is Control c && c.DataContext is RemoteFileItem item)` luôn `false`.
- [x] Cập nhật `OnFileItemDoubleTapped` hỗ trợ trích xuất đa tầng: `(sender as Control)?.DataContext`, `(e.Source as StyledElement)?.DataContext`, `(e.Source as Visual)?.FindAncestorOfType<ListBoxItem>()?.DataContext`, và `listBox?.SelectedItem`.
- [x] Gắn thêm `DoubleTapped="OnFileItemDoubleTapped"` trên phần tử `StackPanel` trong `ItemTemplate`.
- [x] Thêm xử lý `OnFileListBoxKeyDown` cho phím `Enter` (mở thư mục) và `Backspace` (quay lại thư mục cha).
- [x] Chuẩn hóa `NavigateUpAsync` theo POSIX path separator `/`.
- [x] Thêm hỗ trợ nhận diện và mở cả symbolic links (`IsSymbolicLink`).
- [x] Dự án `IPGS.RemoteControl.CcuUI` build sạch 0 Errors, 0 Warnings.

## Đã làm
- Sửa đổi file `RemoteCommandWindow.axaml` và `RemoteCommandWindow.axaml.cs`.
- Thêm `using Avalonia;` và `using Avalonia.VisualTree;`.
- Viết mới bộ unit test `RemoteFileItemTests.cs` (9 tests) kiểm chứng thuộc tính, POSIX path navigation và fallback path.
- Toàn bộ 117 tests trong `IPGS.RemoteControl.Tests` chạy thành công 100%.

## Artifact
- `IPGS.RemoteControl.CcuUI/Views/RemoteCommandWindow.axaml`
- `IPGS.RemoteControl.CcuUI/Views/RemoteCommandWindow.axaml.cs`
- `IPGS.RemoteControl.Tests/RemoteFileItemTests.cs`

## Quyết định quan trọng
- Dùng fallback `targetPath`: nếu `item.FullName` rỗng thì tự động ghép `currentPath.TrimEnd('/') + "/" + item.Name`.
- `NavigateUpAsync` dùng `currentPath.TrimEnd('/').LastIndexOf('/')` thay vì `Path.GetDirectoryName` để tránh xung đột backslash trên Windows khi thao tác SFTP đường dẫn Linux.

## Handoff Payload — bước sau đọc phần này
- do_not_redo: Đã sửa code và build test thành công 100%.
- watch_out: Cả `DoubleTapped` trên ListBox lẫn trên StackPanel con đều được xử lý và đánh dấu `e.Handled = true;`.
- next_inputs: Mã nguồn đã sửa và 117 unit tests.

## Commit
- Hash:
- Đã push:
