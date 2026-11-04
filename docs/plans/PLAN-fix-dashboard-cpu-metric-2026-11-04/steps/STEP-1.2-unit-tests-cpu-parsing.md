---
step: 1.2
task: fix-dashboard-cpu-metric
agent: senior-developer
status: done
deps: [1.1]
created: 2026-11-04
completed_at: 2026-11-04 13:50:00
---

# STEP 1.2 — Viết Unit Tests Kiểm Thử TopParser

## Đã thực hiện
1. Viết 5 unit tests toàn diện trong `IPGS.RemoteControl.Tests/TopParserTests.cs`:
   - `Parse_NullOrEmpty_ReturnsNullCpuAndEmptyProcesses`: Kiểm tra chuỗi rỗng / null.
   - `Parse_EnglishLocale_StandardDot_CalculatesAccurately`: Kiểm tra dấu chấm tiếng Anh (`93.0 id` -> 7.0% CPU).
   - `Parse_VietnameseLocale_CommaDecimal_CalculatesAccuratelyWithoutDroppingIntegerPart`: Kiểm tra lỗi locale tiếng Việt (`67,5 id` -> 32.5% CPU thay vì 95% CPU).
   - `Parse_TwoIterationsBatchOutput_PicksSecondIterationForSteadyState`: Kiểm tra trích xuất mẫu 2 (loại bỏ spike 11% của top ở mẫu 1).
   - `Parse_FallbackToUs_WhenIdleMissing`: Kiểm tra fallback trường user CPU (`%us`).
2. Chạy `dotnet test IPGS.RemoteControl.sln`: 140/140 tests đạt 100% PASS.

## Artifacts
- `IPGS.RemoteControl.Tests/TopParserTests.cs`
