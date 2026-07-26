---
step: 3.1
plan: ../PLAN-MASTER.md
agent: documentation-writer
status: done
completed_at: 2026-07-26 18:13
---

# STEP 3.1 — Viết phần ZCU: cài đặt & triển khai ZcuAgent trên Linux

## Input nhận
- Handoff Log STEP-2.4 (danh sách toàn bộ ảnh đã có/thiếu của Phase 2).
- Scope + mục lục từ `_workspace/00_pm_docs-scope.md`.
- Source tham chiếu: project `IPGS.RemoteControl.ZcuAgent`, script đóng gói `.deb`, cấu hình systemd service, cơ chế khóa SSH (đọc code/script thực tế — KHÔNG bịa quy trình).

## Nhiệm vụ
Đọc `.claude/commands/kztek-brand-info.md`, tạo `docs/user-manuals/MANUAL-ccu-zcu-remote-control.md` với khung mục lục theo scope PM, viết trọn phần ZCU: yêu cầu hệ thống, cài gói `.deb`, kích hoạt/kiểm tra systemd service, thiết lập khóa SSH, cách dùng ZcuSetupWizard từ CcuUI để triển khai, kiểm tra hoạt động & xử lý sự cố thường gặp. Chèn ảnh wizard (từ 2.3) + ảnh terminal (từ 2.4) đúng chỗ; ảnh bị BLOCK → chèn ghi chú `<!-- TODO: ảnh chờ thiết bị ZCU -->`, không dùng placeholder giả.

## Definition of Done
- [x] `docs/user-manuals/MANUAL-ccu-zcu-remote-control.md` có khung đầy đủ + phần ZCU hoàn chỉnh (ghi chú: khung do bước 3.2 tạo trước theo thứ tự chạy thực tế; bước này viết trọn Phần 2)
- [x] Mọi lệnh/đường dẫn/tên service trong tài liệu đối chiếu từ code/script thực tế của ZcuAgent
- [x] Ảnh: máy ZCU LỖI không kết nối được → toàn bộ ảnh Phần 2 dùng marker `⏳ [CHỜ ẢNH]` theo quyết định user "viết trước, bổ sung ảnh sau"; không có link ảnh chết
- [x] Handoff Log điền đủ
- [x] Cập nhật step file này + PLAN-MASTER.md

## Đã làm
- Viết trọn Phần 2 vào khung ch11–13 có sẵn: **ch11** Chuẩn bị (11.1 HW/OS, 11.2 X11 vs Wayland + cách chuyển "Ubuntu on Xorg", 11.3 cài openssh-server, 11.4 kiểm tra mạng ping/Test-NetConnection, 11.5 checklist); **ch12** Cài ZcuAgent (12.1 cách từ xa qua wizard — liệt kê 6 việc kỹ thuật wizard làm, tham chiếu 9.1; 12.2 cài thủ công `setup-zcu-agent.sh` + bảng 5 tham số; 12.3 bảng vị trí file; 12.4 giải thích từng khóa `appsettings.json` kể cả `EnableDesktopIntegration` và deny-by-default của `AllowedClientIPs`; 12.5 systemd user service + journalctl; 12.6 kiểm tra 4 bước sau cài; 12.7 gỡ/nâng cấp); **ch13** Khóa & bảo mật (13.1 SSH password/key + cảnh báo KHÔNG tắt PasswordAuthentication vì CCU dùng password; 13.2 quản lý Token; 13.3 KeyGen RSA-2048 nội bộ; 13.4 quy trình license + nêu đúng hiện trạng KHÔNG enforce; 13.5 bảng khuyến nghị).
- Viết **FAQ 14.1** — 7 câu (agent failed, CCU không thấy máy, sai token, mất kết nối sau reboot/linger, firewall, log ở đâu, màn hình đen).
- Bổ sung Phụ lục A dòng **#53–61** (8 ảnh terminal ZCU + 1 ảnh KeyGen) + legend ZCU-TERM/DEV; cập nhật ghi chú (2) của Phụ lục A; bổ sung mục con ch11–13 vào Mục Lục.
- Xóa cả 3 ghi chú tạm 🚧. Gate check pass: 0 placeholder, 19 link ảnh đều tồn tại, 61 marker khớp 61 dòng Phụ lục A, số Hình giữ nguyên tối đa 19.

## Artifact
- `docs/user-manuals/MANUAL-ccu-zcu-remote-control.md` (Phần 2 + FAQ 14.1 + Phụ lục A mở rộng)
- Cập nhật: step file này, `../PLAN-MASTER.md`

## Quyết định quan trọng
- **Không có "Cách 3 — gói .deb" cho ZcuAgent** như dàn ý dự kiến: đối chiếu code cho thấy `scripts/linux-deb/build-deb.sh` đóng gói **IPGSUseCam** (ứng dụng, cài qua mục 9.2), không phải ZcuAgent → ch12 chỉ có 2 cách cài, kèm ghi chú phân biệt để tránh nhầm.
- **Mâu thuẫn cổng 17600 vs 5900 đã tự hết ở code hiện tại:** `IPGS.RemoteControl.ZcuAgent/appsettings.json` nay ghi 17600; giá trị 5900 chỉ còn trong `code-graph/CODE-GRAPH.md` (lạc hậu, KHÔNG sửa vì ngoài scope). Ch12.4 vẫn ghi rõ "giá trị thực tế = giá trị installer ghi vào file trên máy" như handoff yêu cầu.
- KHÔNG đưa backdoor key trong `LicenseManagerService` vào tài liệu (thông tin nhạy cảm); 13.4 nêu đúng hiện trạng license không enforce, không hứa hành vi khóa app.
- Ảnh Phần 2 toàn bộ là marker (ZCU lỗi); `keygen-output.png` (#61) đánh dấu điều kiện DEV — chụp được không cần ZCU.

## Handoff Log — bước sau cần biết
- Đã làm: Phần 2 (ch11–13) + FAQ 14.1 viết xong từ code/script thật; Phụ lục A mở rộng thành 61 dòng; Mục Lục cập nhật; hết placeholder 🚧.
- File/module đã đọc hoặc đổi: đổi `docs/user-manuals/MANUAL-ccu-zcu-remote-control.md`; đọc `IPGS.RemoteControl.ZcuAgent/{Program.cs,AgentOptions.cs,appsettings.json}`, `scripts/setup-zcu-agent.sh`, `scripts/linux-deb/build-deb.sh`, `KeyGen/Program.cs`, `IPGS.RemoteControl.CcuUI/Services/LicenseManagerService.cs`, `Views/ZcuSetupWizardWindow.axaml.cs`, `IPGS.RemoteControl.CcuClient/ZcuRemoteInstallerService.cs`.
- Quyết định quan trọng: **tổng marker `⏳ [CHỜ ẢNH]` toàn tài liệu = 61** (52 cũ + 9 mới của Phần 2) — bước 4.1 kiểm tra con số này; ảnh thật hiện có 19 (Hình 1–19); ảnh mới nếu chụp thêm đánh số từ Hình 20.
- Bước sau cần biết: Bước 4.1 xuất DOCX/PDF cho MANUAL — lưu ý bug đánh số ordered list đã fix trong `md_to_docx_kztek.py` (không dùng style "List Number"); marker ⏳ là blockquote thuần nên xuất DOCX an toàn; KHÔNG cần verify lại link ảnh (đã pass 19/19).

## Commit
- Hash: dce72bf
- Đã push: không (theo giới hạn scope của bước)

---
**Status icons:** ⬜ Todo | 🔄 In Progress | ✅ Done | 🛑 Blocked | ⏭️ Skipped
