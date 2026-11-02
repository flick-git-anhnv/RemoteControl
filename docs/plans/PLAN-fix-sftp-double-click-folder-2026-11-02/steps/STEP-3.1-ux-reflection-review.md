---
step: 3.1
plan: ../PLAN-MASTER.md
agent: ux-ui-reviewer
status: done
completed_at: 2026-11-02 08:56:50
deps: [2.1]
---

# STEP 3.1 — Phản biện Trực quan UI/UX & Tương tác Bàn phím / Chuột

## Input nhận
- Code đã qua review từ Step 2.1.
- Giao diện tab SFTP trong `RemoteCommandWindow`.

## Nhiệm vụ
Phản biện 4 trục UI/UX (R1-R4) theo quy chuẩn Kztek: kiểm tra vùng hit-box khi click/double click, tính tiện dụng của phím tắt `Enter`/`Backspace`, độ mượt của phản hồi giao diện khi vào thư mục con.

## Definition of Done
- [x] R1 (Hit-box toàn phần): StackPanel có `Background="Transparent"` kết hợp fallback `SelectedItem` cho phép double click ở bất kỳ tọa độ nào trên hàng mà không bị hụt.
- [x] R2 (Phản xạ bàn phím): Hỗ trợ `Enter` để vào thư mục và `Backspace` để quay lại thư mục cha, đồng nhất với chuẩn File Explorer của Windows.
- [x] R3 (Phản hồi thị giác): Status bar cập nhật `Đang tải danh sách file tại...` và `Đã tải X mục.` tức thì.
- [x] R4 (Biểu tượng & Trực quan): Cả thư mục thường lẫn symbolic links đều hiển thị `📁` màu vàng cam và chữ đậm (Bold), dễ nhận biết so với file `📄`.

## Đã làm
- Rà soát tương tác UI/UX: Trải nghiệm mượt mà, tiện dụng hơn rất nhiều so với trước đây (khi chỉ có thể gõ tay đường dẫn vào ô TextBox hoặc nhấn nút ⬆️ Lên).

## Artifact
- Báo cáo UX-UI Reflection & Interaction Audit.

## Quyết định quan trọng
- Chấp thuận đưa vào production.

## Handoff Payload — bước sau đọc phần này
- do_not_redo: Đã phản biện và nghiệm thu UI/UX.
- watch_out: Không có.
- next_inputs: Bản build nghiệm thu QA.

## Commit
- Hash:
- Đã push:
