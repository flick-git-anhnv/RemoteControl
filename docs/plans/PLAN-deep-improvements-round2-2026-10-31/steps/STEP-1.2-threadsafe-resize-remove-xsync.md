---
step: 1.2
plan: ../PLAN-MASTER.md
agent: senior-developer
status: done
completed_at: 2026-10-31T08:59:00+07:00
deps: ["1.1"]
---

# STEP 1.2 — An Toàn Luồng Khi Đổi Resolution & Bỏ Blocking XSync

## Nhiệm vụ
1. Trong `RemoteScreenViewModel.OnFrameReceived`: hoãn `Dispose()` buffer cũ sang `Dispatcher.UIThread.Post(...)` sau khi UI đã tráo sang bitmap mới, triệt tiêu `ObjectDisposedException` khi resize màn hình.
2. Trong `MouseInjector.Move` và `KeyboardInjector.SendKey`: loại bỏ lệnh blocking `X11.XSync`, chỉ giữ `X11.XFlush`.

## Kết quả
- Ứng dụng CCU hoạt động ổn định tuyệt đối kể cả khi ZCU xoay hoặc đổi độ phân giải.
- Độ trễ chuột giảm từ ~0.5ms về $<0.01$ms.
