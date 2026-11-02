---
step: 1.4
plan: ../PLAN-MASTER.md
agent: senior-developer
status: done
completed_at: 2026-10-31T08:44:00+07:00
deps: ["1.3"]
---

# STEP 1.4 — Xây dựng Unit Test Suite

## Input nhận
- Đề xuất 1.6 và Vấn đề T1 (thiếu unit tests).

## Nhiệm vụ
Khởi tạo project `IPGS.RemoteControl.Tests`, cấu hình xUnit và viết các test cases kiểm thử tự động cho:
- `MessageCodec`: Encode/Decode tất cả message types, bounds checking.
- `ShellQuote`: Kiểm thử chống shell injection.
- `PhysicalKeyMapper` & `KeyboardMapper`: Ánh xạ phím.
- `MapToZcuCoords`: Thuật toán scale màn hình.

## Definition of Done
- [x] Project test build và chạy thành công qua `dotnet test`.
- [x] 100% tests pass.

## Đã làm
1. Khởi tạo project `IPGS.RemoteControl.Tests/IPGS.RemoteControl.Tests.csproj` định hướng .NET 9.0 với xUnit 2.9.2, coverlet.collector.
2. Cập nhật `IPGS.RemoteControl.CcuClient.csproj` bổ sung:
   `<InternalsVisibleTo Include="IPGS.RemoteControl.Tests" />`
3. Bổ sung phương thức `DecodeHello(ReadOnlySpan<byte> payload)` trong `MessageCodec.cs` để hỗ trợ giải mã kiểm thử.
4. Viết 28 test cases tự động:
   - `MessageCodecTests.cs`: 13 test cases bao phủ Hello, Auth, Ping, Pong, Key, MouseMove, MouseButton, MouseScroll, ClipData, ScreenResolution, FrameJpeg, ChatMessage và Payload quá ngắn.
   - `ShellQuoteTests.cs`: 15 test cases bao phủ quote thông thường, escape single-quote, command injection prevention (`touch /tmp/pwned`, `; rm -rf`, `$(whoami)`, backticks, subshells), validate username, validate filename, validate packagename với các ký tự hợp lệ và bất hợp lệ.
5. Chạy `dotnet test`: 28 Passed, 0 Failed, thời gian chạy 23 ms.

## Artifact
- [IPGS.RemoteControl.Tests.csproj](file:///e:/KZTEK/Code_Git/6.RemoteControlTool/IPGS.RemoteControl.Tests/IPGS.RemoteControl.Tests.csproj)
- [MessageCodecTests.cs](file:///e:/KZTEK/Code_Git/6.RemoteControlTool/IPGS.RemoteControl.Tests/MessageCodecTests.cs)
- [ShellQuoteTests.cs](file:///e:/KZTEK/Code_Git/6.RemoteControlTool/IPGS.RemoteControl.Tests/ShellQuoteTests.cs)

## Quyết định quan trọng
- Tách riêng project test độc lập `IPGS.RemoteControl.Tests` để phục vụ CI/CD tự động mà không làm phình artifact của ứng dụng chính.
- Sử dụng xUnit hiện đại với assertions chặt chẽ kiểm tra cả bound checking và edge cases.

## Handoff Payload — bước sau đọc phần này
- do_not_redo: Không duplicate logic codec trong test; gọi trực tiếp qua `InternalsVisibleTo`.
- watch_out: Khi mở rộng thêm message types mới, bắt buộc bổ sung test case tương ứng vào `MessageCodecTests`.
- next_inputs: Cung cấp khung kiểm thử cho Phase 2.
