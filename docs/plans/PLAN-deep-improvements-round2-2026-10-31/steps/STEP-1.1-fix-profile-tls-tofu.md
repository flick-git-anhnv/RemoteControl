---
step: 1.1
plan: ../PLAN-MASTER.md
agent: senior-developer
status: done
completed_at: 2026-10-31T08:58:00+07:00
deps: []
---

# STEP 1.1 — Fix TlsCertFingerprint & Nối TOFU UI

## Nhiệm vụ
1. Bổ sung `existing.TlsCertFingerprint = profile.TlsCertFingerprint;` trong `ComputerProfileStore.Save()`.
2. Bổ sung `TlsCertFingerprint` trong `ComputerProfileStore.CloneForStorage()`.
3. Cung cấp hàm `UpdateTlsFingerprint(host, port, fingerprint)` hỗ trợ TOFU.
4. Nối `expectedFingerprint` từ `ConnectionEntryWindow` $\to$ `RemoteScreenWindow` $\to$ `RemoteScreenViewModel`.

## Kết quả
- Toàn bộ luồng TLS Certificate Pinning và Trust On First Use hoạt động khép kín.
- Vân tay chứng chỉ được ghi đĩa bền vững vào `profiles.json`.
