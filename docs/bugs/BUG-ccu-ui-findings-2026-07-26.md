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

## Ghi chú tổng hợp cho senior-developer

- F01 và F02 cùng liên quan tới cặp client mới / agent cũ. Ưu tiên: (a) thêm timeout + thông báo cho mọi request chờ response (SysInfo…); (b) kiểm tra logic chống-hủy-record do resolution.
- Môi trường tài liệu hiện tại: **agent ZCU cần được cập nhật lên bản cùng version client** để chụp được `system-inventory-data.png` và kiểm chứng đầy đủ Privacy/Chat/Clipboard. Việc deploy/cập nhật agent thuộc bước 2.3 (đang bị giới hạn ở phiên này).
- Không phát hiện lỗi ở: NetworkScan, ConnectionEntry, MultiRemote (grid/custom/tab), FileManager (duyệt/lọc/upload/sync/xóa/dir-warning/lỗi-quyền), ConfirmDelete, RemoteCommand Console (chạy lệnh + báo lỗi command-not-found) — tất cả hoạt động đúng như mong đợi.
