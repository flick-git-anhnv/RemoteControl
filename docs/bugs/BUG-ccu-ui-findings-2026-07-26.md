# BUG / UI Findings — CCU Remote Control (phát hiện trong lúc chụp tài liệu)

**Người phát hiện:** Documentation Writer (trong quá trình thao tác thật để chụp screenshot bước 2.2)
**Ngày:** 2026-07-26
**Môi trường:** CcuUI Release (PID 31064), ZCU thật `192.168.1.4` (Ubuntu 22, agent tại `/home/kztek/ipgs/remote-agent`, build 2026-07-24)
**Lưu ý:** Documentation Writer CHỈ ghi nhận, KHÔNG sửa code sản phẩm. Việc sửa thuộc senior-developer ở bước sau.

---

## F01 — Ghi hình (Record) tự dừng ngay lập tức với thông báo "độ phân giải ZCU thay đổi"

| Trường | Nội dung |
|---|---|
| **Màn hình** | RemoteScreenWindow — nút 🔴 Record |
| **Mức độ** | P2 |
| **Các bước tái hiện** | 1. Kết nối P01, xem màn hình ZCU. 2. Bấm 🔴 Record. 3. Ở hộp thoại "Lưu video ghi hình", chọn đường dẫn `.avi`, bấm Save. |
| **Kết quả thực tế** | Trong một số lần, ghi hình dừng NGAY sau khi bắt đầu; tiêu đề cửa sổ đổi thành "⏹ Đã dừng ghi hình: độ phân giải ZCU thay đổi", file `.avi` tạo ra chỉ 232 bytes (header rỗng, không xem được). Nút trở lại "🔴 Record". |
| **Kết quả mong đợi** | Ghi hình bắt đầu và tiếp tục tới khi người dùng bấm ⏹ Stop; file `.avi` chứa nội dung xem được. |
| **Phân tích sơ bộ** | `OnFrameReceived` so `e.Width/e.Height` với `recorder.Width/Height` (snapshot lúc bắt đầu). Nếu frame ĐẦU TIÊN sau khi tạo `SessionRecorder` có kích thước khác snapshot `ScreenWidth/Height` → hủy recorder. Xảy ra không ổn định (lần kết nối mới thường chạy được; lần ghi lại sau khi đã stream một lúc dễ bị hủy). Có thể do lấy `ScreenWidth/Height` trước khi nhận frame ổn định, hoặc ZCU gửi 1 frame kích thước khác giữa chừng. |
| **Ảnh chứng minh** | `docs/bugs/screenshots/bug-record-stopped-resolution.png` |

---

## F02 — Các tính năng Enterprise (SysInfo / Privacy / Chat / Clipboard) không phản hồi & KHÔNG báo lỗi khi agent ZCU cũ

| Trường | Nội dung |
|---|---|
| **Màn hình** | RemoteScreenWindow — nút 📊 SysInfo, 🕶️ Privacy, Chat, 📋 Sync Clipboard |
| **Mức độ** | P2 (robustness / UX feedback) |
| **Các bước tái hiện** | 1. Kết nối P01 (agent trên ZCU build 2026-07-24, cũ hơn commit thêm các tính năng Enterprise `52e93ae` ngày 2026-07-25). 2. Bấm 📊 SysInfo (hoặc bật 🕶️ Privacy, gửi Chat, 📋 Sync Clipboard). |
| **Kết quả thực tế** | Không có phản hồi nào: bấm SysInfo KHÔNG mở `SystemInventoryWindow`, KHÔNG có thông báo lỗi, KHÔNG có timeout. Bật Privacy: nút sáng lên (client) nhưng màn hình ZCU không bị che (agent không xử lý). Người dùng không biết thao tác đã thất bại hay đang chờ. |
| **Kết quả mong đợi** | Khi agent không hỗ trợ / không phản hồi trong X giây, client hiển thị thông báo rõ ràng (VD: "Agent trên máy đích không hỗ trợ tính năng này — vui lòng cập nhật Remote Agent" hoặc "Hết thời gian chờ phản hồi"). |
| **Phân tích sơ bộ** | Client gửi `MessageType.SysInfoReq` và chờ `SysInfoResp` để mở cửa sổ (`OnSysInfoReceived`). Agent cũ không có case `SysInfoReq` → không bao giờ trả `SysInfoResp` → cửa sổ không bao giờ mở, không có nhánh timeout/thông báo. Tương tự cho Privacy/Chat/Clipboard. |
| **Ảnh chứng minh** | Không có (không có gì hiển thị để chụp — chính đó là vấn đề). Hệ quả: ảnh `system-inventory-data.png` của tài liệu KHÔNG chụp được trên môi trường hiện tại (agent cần được cập nhật/deploy lại — thuộc bước 2.3). |

> **CẬP NHẬT 2026-07-26 (bước 2.3):** Nguyên nhân **ĐÃ XÁC NHẬN là version mismatch** (agent build 2026-07-24 cũ hơn client). Sau khi cập nhật ZcuAgent bản mới lên ZCU `192.168.0.101` qua ZcuSetupWizard (build 2026-07-26 20:57, giữ nguyên Token/Port), bấm 📊 SysInfo mở `SystemInventoryWindow` với dữ liệu thật ngay lập tức — ảnh `docs/user-manuals/screenshots/system-inventory-data.png` đã chụp được. **Hiện tượng "im lặng không phản hồi" đã hết.** Khuyến nghị cho senior-developer vẫn giữ nguyên: client nên (a) so sánh version client/agent khi handshake và hiện cảnh báo "Agent phiên bản cũ — vui lòng cập nhật" thay vì fail âm thầm, (b) thêm timeout + thông báo cho mọi request chờ response. |

---

## F03 — Danh sách gợi ý lệnh mẫu (snippet AutoCompleteBox) không mở

| Trường | Nội dung |
|---|---|
| **Màn hình** | RemoteCommandWindow — tab Console — ô "Nhập từ khóa (vd: Reboot, RAM)…" (`PART_SnippetCombo`) |
| **Mức độ** | P3 |
| **Các bước tái hiện** | 1. Mở >_ CMD Shell của P01. 2. Bấm vào ô "Nhập từ khóa…" (hoặc gõ "RAM"). |
| **Kết quả thực tế** | Danh sách gợi ý (10 lệnh mẫu: Reboot, Kiểm tra RAM & Ổ cứng, apt update…) KHÔNG hiện ra dù bấm chuột thật, dù gõ từ khóa, dù nhấn phím Down. Người dùng không thấy được các lệnh mẫu — mất tác dụng của tính năng gợi ý. |
| **Kết quả mong đợi** | Bấm/gõ vào ô → dropdown hiện danh sách lệnh mẫu (MinimumPrefixLength=0, FilterMode=Contains đã cấu hình đúng), chọn 1 mục → lệnh tương ứng được điền vào ô lệnh. |
| **Phân tích sơ bộ** | `OnSnippetComboTapped` đặt `IsDropDownOpen = true` khi Tapped nhưng popup không hiển thị (quirk `AutoCompleteBox` của Avalonia — có thể do popup bị đóng ngay khi mất/giành focus, hoặc dropdown không được populate cho tới TextChanged). Cần kiểm tra lại binding/behavior. |
| **Ảnh liên quan** | `docs/user-manuals/screenshots/remote-command-snippet.png` (chỉ chụp được ô đã gõ "RAM", dropdown không mở) |

---

## F04 — Thông báo lỗi/status cũ KHÔNG được xóa khi bắt đầu thao tác mới (ZcuSetupWizard, RemoteAppInstall)

| Trường | Nội dung |
|---|---|
| **Màn hình** | ZcuSetupWizardWindow (`PART_StatusMsg`), RemoteAppInstallWindow (`PART_StatusMsg`) |
| **Mức độ** | P3 (UX) |
| **Các bước tái hiện** | 1. Mở ⚡ Cài remote của P01, xóa Token, bấm 🚀 Bắt đầu Cài đặt → status đỏ "Thiếu IP/SSH user … hoặc Token Agent." 2. Nhập lại Token hợp lệ, bấm 🚀 Bắt đầu Cài đặt lần nữa. |
| **Kết quả thực tế** | Trong SUỐT quá trình cài (progress 0→100%) status đỏ cũ "Thiếu IP/SSH user…" vẫn hiển thị ở góc trái dưới, chỉ đổi khi cài xong ("Cài đặt ZcuAgent hoàn tất!"). Người dùng nhìn thấy đồng thời log đang chạy + thông báo lỗi cũ → gây nhầm lẫn đang lỗi. Tương tự ở RemoteAppInstallWindow: sau lỗi validation "Vui lòng chọn file cài đặt…", chọn file xong thông báo đỏ vẫn còn. |
| **Kết quả mong đợi** | Khi bấm bắt đầu thao tác mới, `PART_StatusMsg` được reset (xóa trống hoặc đổi thành "Đang cài đặt…") ngay lập tức. (Ghi chú: `OnStartInstallClick` của wizard reset `PART_LogConsole` + `PART_ProgressBar` nhưng KHÔNG reset `PART_StatusMsg`; RemoteAppInstall có set "Đang kết nối và cài đặt…" nhưng chỉ sau khi qua validation — nhánh chọn-file-xong-chưa-bấm-lại thì status lỗi vẫn treo.) |
| **Ảnh chứng minh** | `docs/bugs/screenshots/bug-wizard-stale-error-status.png` (progress 85%, log đang chạy nhưng góc trái dưới vẫn hiện lỗi đỏ cũ) |

---

## F05 — BulkAction hiển thị raw .NET exception thay vì thông báo thân thiện khi máy thiếu cấu hình SSH

| Trường | Nội dung |
|---|---|
| **Màn hình** | BulkActionWindow — kết quả từng máy |
| **Mức độ** | P3 (UX) |
| **Các bước tái hiện** | 1. Tick chọn P01 (đủ SSH) + P02 (KHÔNG có SSH username). 2. Bấm 🚀 Gửi lệnh / Upload File Hàng Loạt, nhập `uname -a`, bấm 🚀 Chạy lệnh. |
| **Kết quả thực tế** | P02 báo Lỗi với nguyên văn exception .NET: `The value cannot be an empty string or composed entirely of whitespace. (Parameter 'username')` — người dùng cuối không hiểu nguyên nhân là máy chưa khai báo SSH user. |
| **Kết quả mong đợi** | Thông báo tiếng Việt rõ ràng, VD: "Máy chưa cấu hình SSH user — vào 'Sửa' máy tính để bổ sung", hoặc loại máy thiếu SSH khỏi danh sách chạy kèm cảnh báo trước khi thực thi. |
| **Ảnh chứng minh** | `docs/bugs/screenshots/bug-bulk-raw-exception.png` |

---

## F06 — Backdoor cứng "ANHNV" + agent mở cho mọi IP (rủi ro bảo mật)

| Trường | Nội dung |
|---|---|
| **Màn hình / Thành phần** | `LicenseManagerService.ValidateLicenseKey` (CcuUI) + `appsettings.json` của ZcuAgent |
| **Mức độ** | P2 (bảo mật) |
| **Các bước tái hiện** | 1. Mở LicenseWindow (qua harness doc), nhập đúng chuỗi `ANHNV` vào ô License Key → bấm Kích hoạt → "Kích hoạt thành công" bất kể Hardware ID. 2. Trên ZCU đọc `~/ipgs/remote-agent/appsettings.json` → `Token: "ANHNV"`, `AllowedClientIPs: ["0.0.0.0/0"]`. |
| **Kết quả thực tế** | (a) Chuỗi `ANHNV` là superadmin backdoor hardcode trong source — bỏ qua toàn bộ kiểm tra chữ ký số + hết hạn + Hardware ID, cho phép kích hoạt vĩnh viễn trên mọi máy. (b) Agent chấp nhận kết nối từ **mọi IP** (`0.0.0.0/0`) với token đoán được `ANHNV` (trùng tên user git `ANHNV_2025`, trùng license backdoor). |
| **Kết quả mong đợi** | (a) Không nhúng backdoor cứng trong bản phát hành, hoặc ít nhất chuyển sang cơ chế ký/kiểm tra không hardcode chuỗi tĩnh trong mã nguồn. (b) `AllowedClientIPs` mặc định giới hạn dải LAN quản trị, token sinh ngẫu nhiên mạnh (không phải chuỗi từ điển ngắn 5 ký tự) — ZcuSetupWizard đã có nút 🎲 Sinh Token, nên bỏ default `ANHNV`. |
| **Ảnh liên quan** | `docs/user-manuals/screenshots/license-success.png` (kích hoạt bằng backdoor, đã che), `docs/user-manuals/screenshots/zcu-terminal-appsettings.png` (Token đã che, thấy `AllowedClientIPs: 0.0.0.0/0`) |
| **Ghi chú** | Documentation Writer CHỈ ghi nhận, KHÔNG sửa. Thuộc phạm vi security-audit-stride của senior-developer/Tech Lead. Chuỗi backdoor thực tế KHÔNG được ghi vào tài liệu người dùng; ảnh success đã che ô key. |

---

## Ghi chú tổng hợp cho senior-developer

- F01 và F02 cùng liên quan tới cặp client mới / agent cũ. Ưu tiên: (a) thêm timeout + thông báo cho mọi request chờ response (SysInfo…); (b) kiểm tra logic chống-hủy-record do resolution.
- Môi trường tài liệu hiện tại: **agent ZCU cần được cập nhật lên bản cùng version client** để chụp được `system-inventory-data.png` và kiểm chứng đầy đủ Privacy/Chat/Clipboard. Việc deploy/cập nhật agent thuộc bước 2.3 (đang bị giới hạn ở phiên này).
- Không phát hiện lỗi ở: NetworkScan, ConnectionEntry, MultiRemote (grid/custom/tab), FileManager (duyệt/lọc/upload/sync/xóa/dir-warning/lỗi-quyền), ConfirmDelete, RemoteCommand Console (chạy lệnh + báo lỗi command-not-found) — tất cả hoạt động đúng như mong đợi.
- **Bổ sung bước 2.3 (2026-07-26, ZCU tại IP mới `192.168.0.101`):** F02 đã hết sau khi cập nhật agent (xem cập nhật trong F02). Thêm F04 (status cũ không reset — ZcuSetupWizard/RemoteAppInstall) và F05 (BulkAction lộ raw exception). Hoạt động đúng: ZcuSetupWizard cài đặt 7/7 bước thành công (~10s, agent tự restart, stream + SysInfo hoạt động ngay); RemoteAppInstall dropdown danh sách package (nút ▼) mở và lọc đúng (kztek-*, agent, kiosk…); KioskDeploy 2 tab hiển thị đủ checkbox; BulkAction chạy `uname -a` song song P01 thành công/P02 lỗi đúng như thiết kế; CronJob (đã chụp trước) thêm/xóa job bình thường.
