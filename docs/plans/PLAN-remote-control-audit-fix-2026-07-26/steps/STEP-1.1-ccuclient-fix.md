---
step: 1.1
title: Fix IPGS.RemoteControl.CcuClient — L1, L4, A5, S1, S7(DPAPI), Q4-Q12, Q15
agent: senior-developer
status: done
started_at: 2026-07-26 13:58
completed_at: 2026-07-26 14:08
commit: 0146cb4
---

# STEP 1.1 — Fix `IPGS.RemoteControl.CcuClient` (kể cả `Protocol/`)

## Nhiệm vụ

Sửa toàn bộ phát hiện thuộc project `CcuClient` trong danh mục 41 mục:
L1, L4, A5, S1 (phần CcuClient), Q12, S7 (DPAPI), Q4, Q5, Q6, Q7, Q8, Q9, Q10, Q11, Q15 phụ.
KHÔNG đụng `ZcuAgent`/`CcuUI`.

## Đã làm

| ID | File | Nội dung fix |
|---|---|---|
| L1 | `RemoteControlClient.cs` | Bọc toàn thân `ConnectOnceAsync` bằng try/catch → `CloseConnectionAsync` + rethrow ở MỌI đường thoát lỗi (TCP connect, HELLO/AUTH handshake). Hết rò 1 socket + NetworkStream mỗi lần reconnect fail. `CloseConnectionAsync` idempotent (Interlocked.Exchange null) nên double-close vô hại |
| L4 | `Protocol/MessageCodec.cs` | 7 hàm decode (`DecodeHelloAck`, `DecodeAuth`, `DecodeFrameJpeg`, `DecodeMouseMove`, `DecodeMouseButton`, `DecodePingPong`, `DecodeKeyEvent`) validate độ dài tối thiểu + độ dài khai báo (nameLen/tokenLen/jpegLen) khớp payload thật — ném `ProtocolException` (không còn IndexOutOfRange/ArgumentOutOfRange). `DecodeFrameJpeg` yêu cầu `jpegLen == payload.Length - 24` |
| Q9 | `Protocol/MessageCodec.cs` | `EncodeHello/HelloAck/Auth/AuthFail` gọi `RequireU16Length` — name/token/reason > 65535 B → `ArgumentException` rõ ràng thay vì `(ushort)` wrap gây lệch khung |
| A5 | `RemoteAppInstallService.cs:77` | `sudo -S ./~/{file}` → `sudo -S "$HOME/{file}"` (cả `chmod +x`) — nhánh cài `.sh`/`.run` hết fail 100% |
| S1 | `RemoteAppInstallService.cs`, `ZcuRemoteInstallerService.cs`, `KioskDeployService.cs` | fileName/packageName validate whitelist `[A-Za-z0-9._+-]`, username whitelist `[a-zA-Z_][a-zA-Z0-9._-]*` (ném `ArgumentException` nếu chứa `'` `;` `$()` space...); `kioskUser`/`AppExec` quote chuẩn POSIX qua `ShellQuote.Quote` |
| Q12 | `ShellQuote.cs` (MỚI) | Helper dùng chung: `Quote()` (single-quote POSIX, strip newline) + `ValidateFileName/ValidatePackageName/ValidateUsername`. Thay 3 chỗ escape password lặp tay |
| S7 | `SecretProtector.cs` (MỚI) + `ComputerProfileStore.cs` + `.csproj` | DPAPI `ProtectedData` scope CurrentUser, format `enc:v1:<base64>`. Load đọc được cả plaintext cũ lẫn mã hoá (tự migrate ở lần save kế). Persist serialize BẢN SAO đã mã hoá (không mutate object runtime). Non-Windows: giữ plaintext + cảnh báo NỔI BẬT 1 lần qua Trace + stderr, không im lặng. Thêm `PackageReference System.Security.Cryptography.ProtectedData 8.0.0` |
| Q4 | `ZcuRemoteInstallerService.cs` | `appsettings.json` dựng bằng `JsonSerializer.Serialize` (anonymous object, WriteIndented) — token chứa `"`/newline được escape đúng, không phá heredoc |
| Q5 | `ZcuRemoteInstallerService.cs` | `dotnet publish`: `using` process, kiểm `WaitForExit(45000)`, quá hạn → `Kill(entireProcessTree: true)`, log ExitCode ≠ 0 |
| Q6 | `ZcuRemoteInstallerService.cs` | `ExecuteCommand` trả `string` (đọc `cmd.Result` TRƯỚC dispose) thay vì trả `SshCommand` đã dispose; cập nhật 4 caller |
| Q7 | `ComputerProfileStore.cs` | Persist ghi atomic: file `.tmp` → `File.Move(overwrite: true)` |
| Q8 | `ComputerProfileStore.cs` | Load lỗi → log Trace + stderr + backup `profiles.json.corrupt-<timestamp>` thay vì nuốt im lặng |
| Q10 | `Services/WakeOnLanService.cs` | Validate MAC bằng regex `^[0-9A-Fa-f]{12}$` → luôn `ArgumentException` đúng cam kết, hết `FormatException` |
| Q11 | `ComputerStatusChecker.cs` | `ProbeAsync` thêm param optional `Action<Action>? uiDispatch` — set `CpuUsage/RamUsage/DiskUsage` qua dispatcher (nhất quán `ApplyStatusResult`); tôn trọng `cancellationToken` (ThrowIfCancellationRequested giữa các lệnh); `catch {}` → log Trace; `using` cho các `SshCommand` |
| Q15 phụ | `RemoteControlClient.cs` (PingSenderLoop) | `(int)elapsed.TotalMilliseconds` → double + `Math.Max(0d, ...)` chống overflow/âm khi đồng hồ chỉnh |
| Bonus | `RemoteAppInstallService.cs` | Fix 5 warning CS8625 pre-existing (`RunCommand` param `Action<string>?`) — build về 0 warning |

## Verification (Iron Law)

```
Verification: dotnet build IPGS.RemoteControl.CcuClient/IPGS.RemoteControl.CcuClient.csproj
Output: Build succeeded.  0 Warning(s)  0 Error(s)  Time Elapsed 00:00:01.08
Kết luận: Pass
```

## Artifact

- `IPGS.RemoteControl.CcuClient/ShellQuote.cs` (mới)
- `IPGS.RemoteControl.CcuClient/SecretProtector.cs` (mới)
- 9 file sửa: `RemoteControlClient.cs`, `Protocol/MessageCodec.cs`, `RemoteAppInstallService.cs`, `ZcuRemoteInstallerService.cs`, `KioskDeployService.cs`, `ComputerProfileStore.cs`, `ComputerStatusChecker.cs`, `Services/WakeOnLanService.cs`, `IPGS.RemoteControl.CcuClient.csproj`
- Commit: `0146cb4` (chưa push — chờ review Bước 4.1)

## Quyết định quan trọng

1. **S1 dùng whitelist thay vì quote lồng:** fileName/packageName nội suy vào BÊN TRONG chuỗi `bash -c '...'` đã single-quote — không thể nest `ShellQuote.Quote` → chọn validate whitelist ký tự an toàn, từ chối input lạ bằng `ArgumentException`. Password/kioskUser/AppExec nằm NGOÀI single-quote context → dùng `Quote()`.
2. **S7 không mutate object runtime:** Persist serialize bản clone đã mã hoá (`CloneForStorage`) — UI binding vẫn thấy plaintext trong RAM, chỉ file trên đĩa mã hoá.
3. **DPAPI decrypt fail → chuỗi rỗng + log, KHÔNG throw** — tránh 1 profile hỏng làm mất cả danh sách; user chỉ cần nhập lại password.
4. **Q11 chọn phương án "caller cung cấp dispatcher"** (param optional, default null = hành vi cũ) vì CcuClient là thư viện thuần, không được kéo dependency Avalonia vào.

## Handoff Log — bước sau cần biết

- Đã làm: Sửa xong toàn bộ 15 mục thuộc CcuClient (L1, L4, A5, S1, S7, Q4-Q12, Q15 phụ) + tạo 2 helper mới `ShellQuote` và `SecretProtector`. Build CcuClient sạch 0 error / 0 warning, commit `0146cb4` (chưa push).
- File/module đã đọc hoặc đổi: 11 file trong `IPGS.RemoteControl.CcuClient/` (xem Artifact); đã đọc thêm `ComputerProfile.cs` (không đổi) và `KioskDeployService.cs` phần đầu.
- Quyết định quan trọng: whitelist thay quote-lồng cho tham số trong `bash -c '...'`; DPAPI qua bản clone khi persist; xem mục trên.
- Bước sau cần biết (**QUAN TRỌNG cho 2.1/3.1/3.2**):
  1. **`MessageCodec.cs` được ZcuAgent compile chung** (`<Compile Include="..\IPGS.RemoteControl.CcuClient\Protocol\MessageCodec.cs">` trong `ZcuAgent.csproj`) — các decode giờ ném `ProtocolException` với payload sai độ dài. Bước 2.1 PHẢI build lại ZcuAgent và đảm bảo receive-loop của `ClientSession` xử lý `ProtocolException` (đóng session, không crash service). Không có signature nào đổi — chỉ hành vi với input malformed.
  2. **`ComputerStatusChecker.ProbeAsync` có param mới `Action<Action>? uiDispatch = null`** (source-compatible, không breaking). Bước 3.1/3.2 khi sửa CcuUI NÊN truyền `a => Dispatcher.UIThread.Post(a)` tại các call-site (`ConnectionEntryWindow` v.v.) để hoàn tất fix Q11 — nếu không truyền, hành vi = cũ (PropertyChanged off UI-thread).
  3. **`RemoteAppInstallService`/`ZcuRemoteInstallerService` giờ ném `ArgumentException`** khi fileName/packageName/username chứa ký tự ngoài whitelist — UI (CcuUI) nên hiển thị message này cho user thay vì coi là lỗi hệ thống.
  4. **profiles.json đổi format secret** (`enc:v1:...`): file cũ plaintext vẫn đọc được, tự migrate khi save. Trên Linux giữ plaintext + warning stderr/Trace (đúng quyết định user).
  5. `docs/plans/` hiện KHÔNG commit vào git (untracked) — giữ nguyên convention, chỉ commit code.
