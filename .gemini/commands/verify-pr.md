---
name: verify-pr
description: "Chạy checklist verification cố định trước khi submit PR để Tech Lead review — build, lint/type-check, test, security note, diff review. Output: VERIFICATION REPORT với mỗi mục PASS/FAIL/SKIP. Gọi trước khi đánh dấu code \"sẵn sàng review\"."
---

# Skill: `/verify-pr` — Pre-PR Verification Checklist

> **Mục đích:** Đảm bảo Developer tự kiểm tra code TRƯỚC khi đẩy cho Tech Lead review, tránh tốn 1 vòng review vô ích do lỗi build/test cơ bản còn sót. Học từ `verification-loop` skill của affaan-m/ecc.
>
> **Khi nào gọi:** Ngay sau khi Developer hoàn thành code thay đổi và TRƯỚC khi nộp PR cho Tech Lead. Tech Lead chỉ mở review khi VERIFICATION REPORT toàn PASS.

---

## Quy trình thực hiện (6 bước tuần tự)

### Bước 1 — Build Check

Chạy build command phù hợp với stack của project:

| Stack | Lệnh |
|-------|------|
| .NET / C# | `dotnet build` |
| Node.js | `npm run build` hoặc `yarn build` |
| Python | `python -m py_compile` hoặc `pytest --collect-only` |
| Generic | Dùng lệnh build có trong `package.json`/`Makefile`/`README` |

**Tiêu chí PASS:** Exit code 0, không có error (warning được phép nhưng phải ghi nhận).

---

### Bước 2 — Lint / Type-Check

| Stack | Lệnh |
|-------|------|
| TypeScript/JS | `npx tsc --noEmit` + `npx eslint .` |
| C# | `dotnet format --verify-no-changes` (nếu có) |
| Python | `ruff check .` hoặc `flake8 .` |

**Tiêu chí PASS:** Không có error-level violation. Warning có thể ghi chú.

**SKIP điều kiện:** Project không có linter/type-checker được cấu hình → ghi rõ "SKIP — không có lint config" trong report.

---

### Bước 3 — Test

Chạy test suite hiện có (unit + integration nếu có):

| Stack | Lệnh |
|-------|------|
| .NET | `dotnet test` |
| Node.js | `npm test` hoặc `npm run test:unit` |
| Python | `pytest` hoặc `python -m unittest` |

**Tiêu chí PASS:** Tất cả test hiện có pass. Không có regression.

**Nếu test fail:** Ghi rõ tên test + message lỗi — không được khai "PASS" khi test đang đỏ.

---

### Bước 4 — Security Quick Note

Nhìn lướt diff của lần thay đổi này, check nhanh 4 điểm:

1. Có hard-code credential / API key / password nào không?
2. Có input từ user/network được dùng trực tiếp vào SQL/shell command không? (injection risk)
3. Có file chứa thông tin nhạy cảm bị add vào git không?
4. Có thay đổi logic auth/payment/DB schema nào cần escalate lên security-audit-stride không?

**Tiêu chí PASS:** Không phát hiện vấn đề nghiêm trọng.
**Tiêu chí FAIL:** Nếu phát hiện bất kỳ điểm nào trên → FAIL + ghi rõ, BLOCK PR cho đến khi fix.

---

### Bước 5 — Diff Review (self-review)

Đọc lại toàn bộ diff của lần thay đổi này:

- [ ] Có dead code hay debug statement bị bỏ sót không? (`console.log`, `TODO`, `// temp`)
- [ ] Logic thay đổi có đúng với yêu cầu (user story / AC) không?
- [ ] Có side effect ngoài ý muốn không? (sửa 1 nơi nhưng ảnh hưởng module khác)
- [ ] Comment và đặt tên biến có rõ ràng không?
- [ ] (Nếu có thay đổi UI) Đã tuân thủ 3 quy chuẩn UI/UX (§20.5 GEMINI.md): (1) Căn giữa dọc nút/text/icon; (2) Phím tắt cho thao tác lặp lại; (3) DataGrid cấu hình auto size `*`.

**Tiêu chí PASS:** Không có vấn đề đáng kể khi đọc lại.

---

### Bước 5b — SDLC Documents Compliance Check (`/check-docs`)

Chạy lệnh kiểm tra tính đầy đủ và tính mới của các tài liệu kỹ thuật liên quan đến thay đổi này:

```powershell
$env:PYTHONIOENCODING = "utf-8"; python scripts/check-sdlc-docs.py --strict
```

- [ ] Các tài liệu SDLC liên quan đến PR (SRS, HLD, LLD, DBDD, API, Test Cases, Deployment) đã được tạo hoặc cập nhật chưa?
- [ ] Có xuất hiện cảnh báo `MISSING` hoặc `OUTDATED` trong báo cáo kiểm tra không?

**Tiêu chí PASS:** Báo cáo trả về `DOCUMENT QUALITY GATE: PASSED` (0 missing, 0 outdated).
**Tiêu chí FAIL:** Có tài liệu bị thiếu hoặc lỗi thời → BLOCK PR, yêu cầu hoàn thiện tài liệu trước khi nộp Tech Lead.

---

### Bước 6 — Tổng hợp VERIFICATION REPORT

Sau khi hoàn thành các bước trên, tạo report theo format sau và **đính kèm vào PR description**:

```
## VERIFICATION REPORT
Generated: YYYY-MM-DD HH:MM | Branch: [branch-name] | By: [Developer name/role]

| # | Kiểm tra | Kết quả | Ghi chú |
|---|----------|---------|---------|
| 1 | Build | PASS / FAIL / SKIP | [lệnh đã chạy, thời gian, số warning nếu có] |
| 2 | Lint / Type-check | PASS / FAIL / SKIP | [tool dùng, số lỗi/warning] |
| 3 | Test | PASS / FAIL / SKIP | [số test, số pass/fail, thời gian chạy] |
| 4 | Security Quick Note | PASS / FAIL / SKIP | [phát hiện gì nếu có] |
| 5 | Diff Self-review | PASS / FAIL / SKIP | [vấn đề gì nếu có] |
| 6 | SDLC Docs Compliance | PASS / FAIL / SKIP | [/check-docs report, 0 missing, 0 outdated] |

**Tổng kết:** [READY FOR REVIEW / BLOCKED — lý do]
```

**Quy tắc tổng kết:**
- `READY FOR REVIEW` — tất cả các bước đều PASS (hoặc SKIP có ghi lý do hợp lệ)
- `BLOCKED` — bất kỳ bước nào FAIL → không được nộp PR cho Tech Lead

---

## Ví dụ VERIFICATION REPORT (C# project)

```
## VERIFICATION REPORT
Generated: 2026-07-12 14:30 | Branch: feature/parking-led-status | By: Senior Developer

| # | Kiểm tra | Kết quả | Ghi chú |
|---|----------|---------|---------|
| 1 | Build | PASS | `dotnet build` — 0 error, 2 warning (obsolete API, sẽ fix riêng) |
| 2 | Lint / Type-check | PASS | `dotnet format --verify-no-changes` — 0 thay đổi cần |
| 3 | Test | PASS | `dotnet test` — 47/47 passed, 3.2s |
| 4 | Security Quick Note | PASS | Không có hard-code credential, không có injection risk |
| 5 | Diff Self-review | PASS | Loại bỏ 1 debug log còn sót; naming rõ ràng |
| 6 | SDLC Docs Compliance | PASS | `/check-docs`: 17/17 docs OK, 0 missing, 0 outdated |

**Tổng kết:** READY FOR REVIEW
```

---

## Lưu ý quan trọng

- **KHÔNG bịa kết quả:** Nếu không chạy được lệnh build (VD: thiếu dependency), ghi FAIL + lý do thay vì khai PASS.
- **SKIP phải có lý do:** "SKIP — project không có test suite" là hợp lệ; "SKIP" không giải thích là không được chấp nhận.
- **Tech Lead có quyền từ chối review** nếu PR không đính kèm VERIFICATION REPORT hoặc report có mục FAIL chưa được giải thích.
- **Không thay thế QA:** Report này là self-check của Developer — QA Engineer vẫn verify sau khi TL approve.
