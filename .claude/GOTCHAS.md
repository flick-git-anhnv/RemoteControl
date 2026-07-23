# GOTCHAS.md — Ràng buộc ngầm & Lỗi đã gặp

> **Mục đích:** Ghi lại các lỗi "ngầm" — không có trong docs chính thức, nhưng thực tế đã gặp và mất thời gian debug. Học từ pattern `PLUGIN_SCHEMA_NOTES.md` của affaan-m/ecc.
>
> **Quy tắc:** Agent fix xong 1 lỗi ngầm (không có trong CLAUDE.md hay README) PHẢI thêm 1 entry vào file này trước khi đánh dấu task hoàn thành.
>
> **Đọc file này khi:** bắt đầu session mới, hoặc gặp lỗi lạ chưa rõ nguyên nhân — tra ở đây trước khi debug từ đầu.

---

## Mục lục nhanh

| # | Vấn đề | Ngày |
|---|--------|------|
| G001 | `scripts/md_to_docx_kztek.py` — thiếu `python-docx`/`Pillow`; PDF không cần trên cloud/sandbox | 2026-07-12 |
| G002 | Avalonia `x:CompileBindings="False"` tắt compile-check binding → class sai không gây lỗi build, chỉ thấy null/empty runtime | 2026-07-15 |
| G003 | Avalonia 12 siết AVLN2100: `{Binding}` tại root UserControl/DataTemplate không có `x:DataType` = lỗi build cứng (Avalonia 11 cho phép fallback reflection) | 2026-07-15 |
| G004 | `CardFormatConfig` thuộc `Kztek.Object` (root), KHÔNG phải `Kztek.Object.MultyPlatform.Device` — build CS0234 nếu dùng sai namespace | 2026-07-16 |
| G005 | `KzNumericUpDown` không có `KzSize` — hardcode `Width/Height="45"` trên nested `KzButton`/`TextBox` đè style class `kz-size-md` (local value luôn thắng style) → control cao/to bất thường so với `KzTextBox` cùng hàng | 2026-07-17 |
| G006 | `KzComboBox` bóp mất chữ vì padding-phải bị trừ 2 lần: cột Grid `*,36` + `Margin=Padding` trên `ContentControl` chỉ span `Grid.Column="0"` → fix bằng `Grid.ColumnSpan="2"` | 2026-07-17 |
| G007 | Kztek.Cameras SDK (FFmpeg/PINVOKE) cấp phát ~100MB native memory mỗi camera khi `StartCamera()` — bất kể camera có kết nối được không; tight reconnect loop tiêu thụ thêm ~94-130MB/s khi camera offline | 2026-07-18 |
| G008 | Avalonia `AttachedToVisualTree` fires TRƯỚC khi Measure/Arrange → `Bounds.Width/Height = 0` → camera decode ở native resolution (1920×1080) thay vì control size (~300px) → CPU spike khi kết hợp với Motion detection. Fix: `DispatcherPriority.Loaded` | 2026-07-18 |
| G009 | `RestSharp.Authenticators.Digest 2.0.0` + `RestSharp` bị NuGet nổi phiên bản lên 114.0.0 (do project khác trong graph kéo) → `MissingMethodException: Method 'Authenticate'...` runtime, build vẫn sạch. Fix: nâng Digest lên 3.0.0 | 2026-07-20 |

---

## G001 — `scripts/md_to_docx_kztek.py`: thiếu `python-docx`/`Pillow`; PDF là optional trên cloud/sandbox

**Ngày phát hiện:** 2026-07-12

**Môi trường:** Linux sandbox (claude.ai / cloud agent)

**Vấn đề ban đầu:**
Chạy `python scripts/md_to_docx_kztek.py <file.md>` báo `ModuleNotFoundError: No module named 'docx'` vì thiếu package `python-docx` và `Pillow`.

**Khắc phục (ĐÃ XÁC NHẬN HOẠT ĐỘNG):**
```bash
pip install python-docx Pillow
```
Sau khi cài, DOCX tạo thành công. Đây là fix dứt điểm cho lỗi ModuleNotFoundError.

**Về PDF export trên cloud/sandbox:**
LibreOffice đã cài tại `/usr/bin/soffice`, nhưng `soffice --headless --convert-to pdf` báo lỗi "source file could not be loaded" trong môi trường sandbox — đây là hiện tượng đã biết, KHÔNG cần debug thêm.

Theo chỉ đạo: **trên cloud/sandbox, PDF không cần thiết**. Dùng `--no-pdf` làm mặc định:
```bash
python scripts/md_to_docx_kztek.py <file.md> --no-pdf
```

PDF chỉ cần khi chạy trên máy local có LibreOffice GUI đầy đủ — không phải môi trường sandbox.

**Không cần làm lại:**
- Không cần điều tra tại sao soffice lỗi trên sandbox — không blocking, không cần fix
- Không cần thử `pip install docx2pdf` — phụ thuộc vào Word/LibreOffice GUI, không hoạt động trên Linux sandbox
- DOCX là artifact chính; PDF là optional và chỉ cần ở môi trường local

**Lần đầu gặp:** Bước 1.1-1.2 — WF-REFACTOR optimize-framework (2026-07-12)

---

## G002 — Avalonia `x:CompileBindings="False"` tắt compile-check: binding sai class không gây lỗi build

**Ngày phát hiện:** 2026-07-15

**Môi trường:** Avalonia UI (.NET 8, Windows + Linux)

**Vấn đề:**
`ConfigurationWindowViewModel.Server` dùng class `Kztek.Object.Entity.ConfigObjects.ServerConfig` (thiếu field `ApiUrl`, `LoginUrl`, `ClientId`) trong khi XAML bind `Server.ApiUrl`, `Server.LoginUrl`. Build hoàn toàn sạch 0 lỗi — không có gì báo hiệu vấn đề. Lúc runtime, các textbox này hiển thị empty vì property không tồn tại.

**Nguyên nhân:**
`ServerTabView.axaml` có `x:CompileBindings="False"` → Avalonia dùng reflection binding tại runtime, không compile-check binding path. Với `x:CompileBindings="True"` (mặc định), mismatch property sẽ là lỗi build.

**Cách xử lý:**
- Đảm bảo ViewModel dùng đúng class type khớp với field XAML bind vào.
- Khi thấy binding không hiển thị data (empty/null) mà không có lỗi build → kiểm tra ngay `x:CompileBindings` của View + type của property trong ViewModel.
- Ưu tiên dùng `x:CompileBindings="True"` (default) — mismatch = lỗi build phát hiện sớm.

**Lần đầu gặp:** Phase 7.1 — D1 ServerConfig merge (2026-07-15, commit c35bfc8)

**Không cần làm lại:** Không có workaround nào phía XAML — phải sửa đúng class ở ViewModel/Service.

---

## G003 — Avalonia 12: AVLN2100 — Compiled binding yêu cầu x:DataType tường minh tại root UserControl

**Ngày phát hiện:** 2026-07-15

**Môi trường:** Avalonia 12.1.0 (nâng từ 11.2.7), .NET 8/10, Windows + Linux

**Vấn đề:**
Sau khi nâng Avalonia 11.2.7 → 12.1.0, tất cả file `.axaml` không có `x:DataType` tại root `<UserControl>` báo lỗi build cứng AVLN2100: "Cannot parse a compiled binding without an explicit x:DataType directive to give a starting data type for bindings". Avalonia 11.x cho phép fallback về reflection binding âm thầm khi thiếu `x:DataType`; Avalonia 12.x bắt buộc khai báo tường minh — thiếu = lỗi build, không phải warning.

**Nguyên nhân:**
Avalonia 12 siết chặt compiled binding: `{Binding ...}` (không có `x:CompileBindings="False"`) bắt buộc phải có `x:DataType` trong scope tương ứng (root element hoặc `<DataTemplate>` gần nhất) để compiler biết kiểu dữ liệu lúc build-time.

**Cách xử lý:**
Thêm `x:DataType="[xmlns-prefix]:[ViewModelClassName]"` vào root element của mỗi file thiếu. Nếu namespace alias chưa có → thêm `xmlns:vm="using:Your.Namespace"` vào cùng element. Không cần đổi bất kỳ binding nào khác.

```xml
<!-- Trước (lỗi AVLN2100) -->
<UserControl xmlns="..." x:Class="Kztek.Cameras.CameraDescriptionPage">

<!-- Sau (fix) -->
<UserControl xmlns="..." x:Class="Kztek.Cameras.CameraDescriptionPage"
             x:DataType="local:CameraDescriptionPageViewModel">
```

Với `<DataTemplate>` trong ListBox/ItemsControl: thêm `x:DataType` vào chính `<DataTemplate>`, không phải root.

**Lần đầu gặp:** Nâng Avalonia 11.2.7 → 12.1.0 cho `Kztek.Cameras.Avalonia` — 34 lỗi AVLN2100 tại 5 file SettingPage (2026-07-15)

**Không cần làm lại:**
- Không cần thêm `x:CompileBindings="False"` để tắt lỗi — đây là workaround tệ, mất compile-time safety (xem G002).
- Không cần sửa binding expression — chỉ thiếu type declaration, không phải binding path sai.

---

## G004 — `CardFormatConfig` thuộc namespace `Kztek.Object` (root), KHÔNG phải `Kztek.Object.MultyPlatform.Device`

**Ngày phát hiện:** 2026-07-16

**Môi trường:** .NET 8, Windows — project `ParkingV8.App`

**Vấn đề:**
Build báo lỗi `CS0234: The type or namespace name 'CardFormatConfig' does not exist in the namespace 'Kztek.Object.MultyPlatform.Device'` tại `LaneSettingsWindow.axaml.cs` trong method `SaveControllerConfigs()`.

**Nguyên nhân:**
`CardFormatConfig` và `CardFormat` (enum) nằm trong namespace **`Kztek.Object`** (root) — không phải sub-namespace `Kztek.Object.MultyPlatform.Device`. TDD/doc ghi `Kztek.Object.MultyPlatform.Device.CardFormatConfig` là sai. Để so sánh: `BarrieOpenModeConfig` thì đúng là ở `Kztek.Object.MultyPlatform.Device`.

**Cách xử lý:**
```csharp
// SAI:
using Kztek.Object.MultyPlatform.Device; // CardFormatConfig không có ở đây

// ĐÚNG:
using Kztek.Object;                          // CardFormatConfig, CardFormat enum
using Kztek.Object.MultyPlatform.Device;     // BarrieOpenModeConfig, EmBarrieOpenMode
```

**Lần đầu gặp:** Phase 2 (T1 LaneControllerConfigStore + T6 SaveControllerConfigs) — controller tab gap-fill (2026-07-16)

**Không cần làm lại:** Không thêm `using Kztek.Object.MultyPlatform.Device;` cho `CardFormatConfig` — sẽ lỗi build.

---

## G005 — `KzNumericUpDown` quá to/cao so với `KzTextBox`/`KzComboBox` cùng hàng vì thiếu `KzSize` + local Width/Height đè style class

**Ngày phát hiện:** 2026-07-17

**Môi trường:** Avalonia UI, `ParkingV8.UI.KztekComponents.Controls.KzNumericUpDown`

**Vấn đề:**
`PaymentTabView.axaml` đặt `kz:KzNumericUpDown` cạnh `kz:KzTextBox KzSize="Sm"` trong cùng 1 hàng field (VD: "Connection Port" / "Baudrate") nhưng `KzNumericUpDown` hiển thị cao/to hơn hẳn — không đồng bộ chiều cao.

**Nguyên nhân:**
`KzNumericUpDown` (khác với `KzTextBox`/`KzButton`/`KzComboBox`) KHÔNG có property `KzSize` — không có cách nào chỉnh size từ ngoài. Bên trong `Axaml/KzNumericUpDown.axaml`, các nút `KzButton` horizontal layout đã có `Classes="... kz-size-md"` (style setter Height=40 theo `kz.height.md`), NHƯNG đồng thời set local `Width="45" Height="45"` trực tiếp trên control — trong Avalonia, local value luôn có priority cao hơn style selector, nên style class `kz-size-md` bị vô hiệu hoá hoàn toàn, control luôn hiển thị 45x45 bất kể class nào được gán.

**Cách xử lý (ĐÃ ÁP DỤNG):**
1. Thêm `KzSizeProperty` (enum `KzSize`, default `Md`) vào `KzNumericUpDown.axaml.cs`, tương tự pattern `KzTextBox`/`KzButton`/`KzComboBox`.
2. Thêm method `ApplyKzSize(KzSize)` set trực tiếp `Width`/`Height`/`FontSize` theo `kz.height.sm/md/lg` (32/40/48) + `kz.font.sm/md/lg` cho cả horizontal và vertical layout — gọi tại setter `KzSize` và tại `OnAttachedToVisualTree` (để áp dụng default Md ngay cả khi user không set).
3. Xoá toàn bộ local `Width="45" Height="45"` / `Height="40"` / `Height="44"` hardcode trong `Axaml/KzNumericUpDown.axaml` — để code-behind kiểm soát hoàn toàn kích thước qua `KzSize`.
4. Tại nơi dùng cạnh `KzTextBox KzSize="Sm"` (VD `PaymentTabView.axaml`) → set `KzNumericUpDown KzSize="Sm"` để đồng bộ chiều cao.

**Bài học tổng quát (áp dụng cho MỌI Kz custom control trong tương lai):**
Khi 1 control lồng bên trong UserControl có cả `Classes="kz-size-md"` VÀ local `Width`/`Height` hardcode → local value luôn thắng, class bị vô hiệu hoá âm thầm (không có warning). Khi thêm size variant cho control mới, PHẢI kiểm tra không còn local `Width`/`Height` hardcode nào đè lên style class tương ứng.

**Lần đầu gặp:** Fix "KzNumericUpdate quá to" trong `PaymentTabView.axaml` (2026-07-17)

**Không cần làm lại:** Không sửa `kz.height.md` token (32/40/48 đã đúng, dùng chung toàn hệ thống) — vấn đề là ở local value override, không phải giá trị token sai.

**⚠️ Cập nhật 2026-07-17 (bug tái phát sau fix trên):** Sau khi thêm `KzSize` + set `Width`/`Height` cho nút `KzButton` bên trong, nút hiển thị thành "viên thuốc" tròn xoe, KHÔNG thấy ký tự +/− — vì `KzButton` (`KzButton.cs`) tự có property `KzSize` RIÊNG (default `Md`), và `OnAttachedToVisualTree`/`UpdateStyleClasses()` của chính nó LUÔN xoá hết `Classes` cũ rồi gán lại `kz-size-{sm|md|lg}` dựa theo property `KzSize` của bản thân nút — bất kể `Classes="... kz-size-md"` hardcode ngoài `KzNumericUpDown.axaml` là gì. Kết quả: `Padding` (VD `kz.padding.btn.md` = 16,0) luôn theo `KzSize` mặc định (Md) của nút con, trong khi `Width` bị code cha ép nhỏ (32 cho Sm) → padding 16,0 ăn hết chỗ trống bên trong nút 32px, chữ +/− bị bóp mất, chỉ còn thấy khối màu bo tròn.
**Fix đúng:** Phải set TRỰC TIẾP `btn.KzSize = size` (property của `KzButton`, không chỉ set Width/Height) để `UpdateStyleClasses()` bên trong nút tự chọn đúng `Padding`/`CornerRadius` theo size — sau đó mới override `Width`/`Height`/`Padding=0`/`CornerRadius` (có hướng, chỉ bo góc ngoài) bằng code, vì các giá trị set sau luôn thắng style class.
**Bài học:** Khi 1 Kz control cha chứa Kz control con cùng có property `KzSize` riêng (VD `KzButton` lồng trong `KzNumericUpDown`), PHẢI đồng bộ `KzSize` của con theo cha — không chỉ set Width/Height thô — vì con có thể tự ghi đè `Classes` của chính nó bất cứ lúc nào (`OnAttachedToVisualTree`, property setter) mà cha không hay biết.

**⚠️ Cập nhật 2026-07-17 (bug thứ 3, tinh vi hơn — border khung nhập số lệch 1-2px so với nút +/−):** Sau 2 fix trên, border của ô nhập số (`Border Grid.Column="1"` bọc `TextBox ValueInput`) vẫn cao hơn nút `KzButton` 1-2px dù cả hai cùng set số bằng nhau (VD 32). Nguyên nhân: code chỉ set `Height` cho `TextBox` bên TRONG `Border`, còn `Border` không có `Height` riêng — `Border` không có `BorderThickness="0,1,0,1"` (dày 1px top + 1px bottom) sẽ tự CỘNG THÊM 2px vào chiều cao hiển thị so với `Height` của child bên trong (Border's rendered height = child.Height + BorderThickness.Top + BorderThickness.Bottom, KHÔNG phải Border co lại để chứa vừa child trong đúng Height đó). Trong khi nút `KzButton` không có Border cha nào cộng thêm — `Height` set trên chính nút là chiều cao cuối cùng.
**Fix đúng:** Đặt `x:Name="ValueBorderH"` cho chính cái `Border` bọc TextBox, rồi set `Height` LÊN BORDER (không phải lên TextBox con) + `TextBox.VerticalAlignment="Stretch"` để TextBox tự co giãn vừa trong border (phần diện tích trong sẽ tự nhỏ hơn `Border.Height` đúng bằng `BorderThickness`, đó là hành vi mong muốn).
**Bài học tổng quát:** Bất cứ khi nào cần đồng bộ chiều cao/rộng giữa 2 sibling control trong cùng hàng ngang mà 1 trong 2 có `Border` bọc ngoài với `BorderThickness` khác 0 ở trục đang so sánh (top/bottom cho Height, left/right cho Width) → PHẢI set kích thước lên chính `Border` ngoài cùng, KHÔNG set lên control con bên trong `Border` — nếu không, `BorderThickness` sẽ cộng dồn ra ngoài kích thước mong muốn, gây lệch vài px không dễ nhận ra bằng mắt thường trên ảnh nhỏ nhưng rõ khi zoom.

**⚠️ Cập nhật 2026-07-17 (bug thứ 4 — vẫn lệch dù Height bằng nhau, do Grid không có `RowDefinitions` tường minh):** Sau khi Border và cả 2 nút cùng set `Height` bằng nhau (VD 32), người dùng vẫn thấy lệch nhẹ theo chiều dọc ở phần trên/dưới khi zoom ảnh. `HorizontalLayout`/`RootGrid` KHÔNG khai báo `RowDefinitions` tường minh → Avalonia Grid mặc định 1 hàng kiểu `*` (Star, chiếm hết chiều cao khả dụng) thay vì `Auto` (co theo nội dung). Khi hàng `*` có nhiều khoảng trống hơn `Height` của children, `VerticalAlignment` mặc định (`Stretch`) của từng control quyết định vị trí dọc trong khoảng trống dư đó — và `Border` vs `KzButton` có thể xử lý "Stretch + Height tường minh" hơi khác nhau (căn giữa vs không), gây lệch vài px không nhất quán.
**Fix đúng:** (1) Khai báo `RowDefinitions="Auto"` tường minh cho `RootGrid` VÀ `HorizontalLayout` để hàng co đúng theo nội dung, không còn khoảng trống dư; (2) đặt `VerticalAlignment="Top"` tường minh trên `DecreaseBtn`, `IncreaseBtn`, VÀ `ValueBorderH` để cả 3 luôn neo cùng 1 điểm mốc, không phụ thuộc hành vi ngầm định của từng loại control khi `Stretch`.
**Bài học tổng quát:** Khi ghép nhiều control cạnh nhau cần khớp pixel chính xác (input group/segmented control), KHÔNG dựa vào `VerticalAlignment`/`HorizontalAlignment` mặc định (`Stretch`) của Grid row/column kiểu `*` — luôn khai báo `RowDefinitions`/`ColumnDefinitions="Auto"` tường minh + set alignment tường minh giống nhau (`Top`/`Left` hoặc `Center`) trên TẤT CẢ sibling cần khớp nhau, để loại bỏ mọi phụ thuộc vào hành vi mặc định có thể khác nhau giữa các loại control (`Border` vs `Button`/`TemplatedControl`).

---

## G006 — `KzComboBox` bóp mất chữ vì padding-phải bị trừ 2 lần (Grid column + Margin=Padding)

**Ngày phát hiện:** 2026-07-17

**Môi trường:** Avalonia UI, `ParkingV8.UI.Controls.Axaml.KzComboBox` (`ParkingV8.UI.KztekComponents.Controls.KzComboBox`)

**Vấn đề:**
`KzComboBox` hiển thị rỗng (không thấy chữ, VD "Không sử dụng") dù đã có giá trị chọn, đặc biệt rõ ở `kz-size-sm` (ComboBox hẹp).

**Nguyên nhân:**
Template có `Grid ColumnDefinitions="*,36"` — cột 1 (36px) đã dành riêng cho mũi tên dropdown, nên cột 0 (`*`) chỉ còn `width - 36`. Đồng thời, `ContentControl Name="PART_ContentPresenter"` chỉ đặt `Grid.Column="0"` NHƯNG lại có `Margin="{TemplateBinding Padding}"`, và `Padding` mặc định có cạnh phải 32/36/40 (theo `kz-size-sm/md/lg`) — vốn được thiết kế để chừa chỗ cho mũi tên. Kết quả: khoảng trống bên phải bị trừ **2 lần** — 1 lần do cột Grid (36px), 1 lần nữa do Margin/Padding (32-40px) — vùng hiển thị chữ thực tế chỉ còn `width - 36 - 12(trái) - 36(phải) = width - 84`. Với ComboBox nhỏ (size sm, width ~100-150px), vùng còn lại gần như bằng 0 → chữ bị cắt/ẩn hoàn toàn dù control có giá trị.

**Cách xử lý (ĐÃ ÁP DỤNG):**
Thêm `Grid.ColumnSpan="2"` cho `ContentControl Name="PART_ContentPresenter"` (giữ nguyên `Grid.Column="0"`) trong `src/ParkingV8.UI/Controls/Axaml/KzComboBox.axaml` — để nó trải rộng cả 2 cột, cho `Padding` phải (36/40/32) là nơi DUY NHẤT chừa chỗ cho mũi tên, không còn bị cột Grid trừ thêm lần nữa. `PathIcon` mũi tên vẫn nằm sau trong cây visual (vẽ đè lên trên) nên vẫn hiển thị đúng vị trí, không bị chữ che.

**Bài học tổng quát:** Khi 1 control có Grid chia cột để dành chỗ cho icon/mũi tên (VD `*,36`), KHÔNG đồng thời dùng `Padding`/`Margin` với cùng giá trị đó trên nội dung chỉ nằm ở cột đầu — chọn 1 trong 2 cơ chế (hoặc Grid column, hoặc Padding trên full-width content), không dùng cả hai cùng lúc vì sẽ cộng dồn và bóp méo vùng hiển thị, đặc biệt lộ rõ ở size nhỏ.

**Lần đầu gặp:** Fix "KzCombobox padding quá lớn, không hiện được chữ" (2026-07-17)

---

## G007 — Kztek.Cameras SDK: ~100MB native memory mỗi camera khi StartCamera(), crash trong 5s khi camera offline

**Ngày phát hiện:** 2026-07-18

**Môi trường:** Windows 11 x64, Debug build, môi trường dev không có camera IP thật kết nối

**Vấn đề:**
`MainShellWindow` với 6 camera feed (2 lanes × 3 cameras) trở thành "(Not Responding)" và crash sau ~5 giây kể từ khi camera bắt đầu. RAM tăng từ ~180MB lên 1237MB+ trong vài giây, CPU ngốn 4+ cores liên tục trong khi UI hoàn toàn idle.

**Nguyên nhân:**
Kztek.Cameras SDK (backed bởi `ANV.Cameras.PINVOKE` / FFmpeg) cấp phát **~100MB native (unmanaged) memory mỗi camera** ngay tại `StartCamera()` — bất kể camera có thực sự kết nối được hay không (allocation xảy ra trước khi kết nối TCP/RTSP). Với 6 camera đồng thời: ~589MB native memory spike trong ~5 giây đầu.

Khi camera unreachable, FFmpeg vào tight reconnect loop: tiếp tục cấp phát thêm **~94–130MB/s** native memory + ngốn **4+ CPU cores**. Crash xảy ra sau ~5 giây — nhanh hơn bất kỳ watchdog timer nào có thể can thiệp kịp.

**Đặc điểm quan trọng:**
- Allocation xảy ra ở native/unmanaged heap, KHÔNG xuất hiện trong managed GC counters → profiler managed sẽ không thấy rõ
- GC.Collect() hoặc Dispose() của managed Bitmap KHÔNG ảnh hưởng đến native memory của SDK
- `CameraView.WatchdogTimer` với timeout 8-25s đều vô dụng — crash đã xảy ra trước khi timeout fire

**Cách xử lý (ĐÃ ÁP DỤNG, CHƯA ĐỦ):**
- Rút timeout 25s → 8s + gọi `StopCamera()` khi timeout (fix trong `CameraView.axaml.cs`)
- Guard `string.IsNullOrWhiteSpace(config.Host)` để skip camera chưa cấu hình IP
- Cả hai không đủ ngăn crash trong ~5s với camera có IP nhưng offline

**Cách xử lý đề xuất (cần Tech Lead duyệt):**
- **A — Staggered start**: Khởi động camera tuần tự cách nhau 2-3s thay vì song song, giảm spike ban đầu
- **B — Dev-mode toggle**: Config flag tắt toàn bộ camera SDK trong môi trường dev không có camera thật
- **C — SDK source investigation**: Tìm API limit native allocation hoặc connection timeout trong Kztek.Cameras source

**Lần đầu gặp:** BUG-004 investigation (2026-07-18) — WF-BUGFIX Bước 2

**Không cần làm lại:** Không cần tăng watchdog timeout (8s hoặc 25s đều không cứu được — crash trước khi timeout fire). Không cần profile managed heap (issue là unmanaged native memory). Không cần dispose thêm Bitmap ở app layer để fix idle crash (Bitmap chỉ cấp phát khi có sự kiện xe, không phải lúc idle).

---

## G008 — Avalonia `AttachedToVisualTree` fires trước layout → `Bounds = 0` → camera decode full native resolution

**Ngày phát hiện:** 2026-07-18

**Môi trường:** Avalonia UI (.NET 8, Windows 11 x64), `ParkingV8.App` multi-lane scenario với Motion detection

**Vấn đề:**
`MainShellWindow` với 6 camera + Motion detection bật: "Not Responding" ngay khi mở, CPU 8+ cores dù UI idle, RAM tăng không giới hạn.

**Nguyên nhân (tổ hợp — phải xảy ra cùng lúc mới gây crash):**
1. `AttachedToVisualTree += (_, _) => StartCamera()` gọi `StartCamera()` trước khi Avalonia hoàn tất Measure/Arrange pass lần đầu → `Bounds.Width/Height = 0` tại thời điểm đó.
2. `AnvPlayer._cachedWidth/_cachedHeight` (volatile int) chỉ cập nhật từ `OnPropertyChanged(BoundsProperty)` khi `Width > 0` → vẫn = 0 khi `StartCamera()` chạy.
3. `_cachedWidth = 0` → `controlReady = (0 >= 32) = false` → decode tại native camera resolution (1920×1080) thay vì control size (~300px) → 41× nhiều pixel hơn.
4. `motionDetectionInterval = 0` → mọi frame đều đi qua OpenCV pipeline (`ProcessFrame()` với Absdiff + Threshold + Erode) → 6 cameras × 30fps × heavy OpenCV → CPU maxed → UI starved.

**Nếu chỉ có 1 điều kiện (không đủ gây crash):**
- Motion detection BẬT nhưng layout valid (size > 0) → decode ở ~300px → fine
- Layout size = 0 nhưng Motion detection TẮT → không có OpenCV → fine
- Motion detection BẬT + size=0 nhưng chỉ 1 camera → tải thấp hơn → thường fine

**Cách xử lý (ĐÃ ÁP DỤNG — 6 fixes, build 0 error):**
1. **`DispatcherPriority.Loaded`** thay vì gọi trực tiếp trong `AttachedToVisualTree` — delay đến sau layout pass, `_cachedWidth` đã hợp lệ
2. **`motionDetectionInterval = 100`** (không phải 0) — tối đa 10 motion check/giây thay vì 30
3. **`CloseVideoSource()` luôn gọi `Stop()`** — không bypass khi `IsRunning = false`
4. **Bare `catch {}` → xử lý `OperationCanceledException` riêng** — tránh exception storm khi cancel
5. **`VideoStreamDecoderIntptr.Dispose()` gọi `TryComplete()`** trên tất cả channels — consumer tasks không bị stuck
6. **`using var erodeKernel = new Mat()`** thay `new Mat()` không dispose — tránh ~180 objects/s vào finalizer queue

**Lần đầu gặp:** BUG-004 investigation vòng 2 (2026-07-18) — xác nhận sau coordinator update "motion detection + size=0 = crash"

**Không cần làm lại:**
- Không cần tăng watchdog timer timeout — crash xảy ra trước bất kỳ timeout nào
- Không cần điều tra riêng `OperationCanceledException` (là triệu chứng của exception storm do size=0 + OpenCV, không phải root cause)
- Với Motion detection TẮT, không cần fix size=0 (không đủ tải để crash)

---

## G009 — `RestSharp.Authenticators.Digest 2.0.0` không tương thích khi RestSharp core bị NuGet nổi lên 114.0.0 → `MissingMethodException` runtime dù build sạch

**Ngày phát hiện:** 2026-07-20

**Môi trường:** .NET 8, Windows, `IPGS.Object` (camera controllers HIK/Dahua dùng Digest Auth để lấy ảnh/config vùng phát hiện)

**Vấn đề:**
Màn hình "Cài đặt vùng phát hiện Camera (ConfigRegion)" báo lỗi runtime khi bấm "Lấy ảnh"/"Phát hiện": `Method 'Authenticate' in type 'RestSharp.Authenticators.Digest.DigestAuthenticator' from assembly 'RestSharp.Authenticators.Digest, Version=2.0.0.0'...`. Build hoàn toàn sạch, 0 lỗi — không có gì báo hiệu trước.

**Nguyên nhân:**
`IPGS.Object.csproj` chỉ khai báo `PackageReference RestSharp.Authenticators.Digest 2.0.0` (không pin `RestSharp` core trực tiếp). Package Digest 2.0.0 được compile chống lại `IAuthenticator` interface của RestSharp 111.x (sync `Authenticate`). Nhưng project reference khác trong cùng graph (`Kztek.Api.MultyPlatform`, thuộc `parking-v8-app-avalonia`) khai báo cứng `RestSharp 114.0.0`. NuGet resolve toàn bộ graph theo bản CAO NHẤT thoả floor constraint (`>= 111.2.0`) → chọn 114.0.0 cho cả solution. RestSharp 114 đổi signature `IAuthenticator.Authenticate` (thêm `CancellationToken`) → assembly Digest 2.0.0 cũ bị `MissingMethodException` ở runtime vì interface binding không khớp, dù compile-time không phát hiện được (do duck-typing qua interface, không có strict version check lúc build).

**Cách xử lý (ĐÃ ÁP DỤNG, xác nhận build 0 lỗi):**
Nâng cả 2 nơi dùng Digest lên bản 3.0.0 (đã hỗ trợ RestSharp 114 + `CancellationToken`, có class `DigestAuthenticatorLegacy` back-compat):
```xml
<!-- IPGS.Object.csproj -->
<PackageReference Include="RestSharp.Authenticators.Digest" Version="3.0.0" />

<!-- Kztek.Tool.csproj (cùng lỗi tiềm ẩn, đồng bộ luôn) -->
<PackageReference Include="RestSharp" Version="114.0.0" />
<PackageReference Include="RestSharp.Authenticators.Digest" Version="3.0.0" />
```
`dotnet restore --force` để NuGet re-resolve lock file (`obj/project.assets.json`) — restore thường không tự refresh nếu chỉ đổi version patch nhỏ trong cache cũ.

**Bài học tổng quát:** Khi 1 project không pin trực tiếp 1 package core (VD `RestSharp`) mà chỉ pin package phụ thuộc nó (VD `RestSharp.Authenticators.Digest`), NuGet sẽ tự nổi version core theo bất kỳ project nào khác trong dependency graph pin cao hơn — có thể phá vỡ tương thích binary của package phụ thuộc mà KHÔNG có lỗi build, chỉ lộ ra ở runtime khi gọi đúng method bị đổi signature. Luôn kiểm tra `obj/project.assets.json` (`grep "PackageName/"`) để biết version THỰC SỰ được resolve, không tin vào version ghi trong `.csproj`.

**Lần đầu gặp:** Bug report user "Lỗi: Method 'Authenticate'..." trên màn ConfigRegion (2026-07-20)

**Không cần làm lại:** Không cần pin `RestSharp` xuống 111.2.0 để ép Digest 2.0.0 hoạt động — sẽ downgrade `Kztek.Api.MultyPlatform` và có thể gây lỗi khác ở project đó; nâng Digest lên bản mới tương thích là hướng đúng.

---

<!-- Thêm entry mới theo format:

## G00N — [Tên vấn đề ngắn gọn]

**Ngày phát hiện:** YYYY-MM-DD
**Môi trường:** [OS / platform / version]
**Vấn đề:** [Mô tả triệu chứng cụ thể]
**Nguyên nhân:** [Root cause đã xác định]
**Cách xử lý:** [Giải pháp, workaround, hoặc cách tránh]
**Lần đầu gặp:** [Context task / session]
**Không cần làm lại:** [Những gì đã thử mà KHÔNG hoạt động — để tránh lặp lại]

-->

## G010 — Lỗi RAM tăng dần khi xem LiveView do khởi tạo luồng giải mã 2 lần (Double allocation)

**Ngày phát hiện:** 2026-07-20
**Môi trường:** Avalonia UI, Windows

**Vấn đề:**
Người dùng phản ánh RAM tăng dần sau khi bấm xem LiveView. Mở LiveView với camera offline cũng gây rò rỉ RAM (vào tight reconnect loop theo G007).

**Nguyên nhân:**
1. Trong MainWindow.axaml.cs, hàm OpenLiveViewAsync tạo một KzCamera mới và gọi camera.Start(motionDetectionInterval: 100). Việc này khởi tạo ngầm một AnvPlayer chạy dưới nền, cấp phát native memory (~100MB) và tiến hành phân tích nhận diện chuyển động mỗi 100ms.
2. Sau đó, rmViewCamera lại tạo ra một AnvPlayer thứ hai (UI control) và copy lại stream URL. Kết quả là có tới HAI luồng FFmpeg giải mã cùng một stream, gây lãng phí RAM và CPU. Nếu camera offline, cả 2 luồng đều rơi vào reconnect loop (G007). Hơn nữa, rmViewCamera không có Watchdog Timer nên nếu để LiveView mở, RAM sẽ tăng liên tục (~94-130MB/s) cho đến khi app crash.

**Cách xử lý (ĐÃ ÁP DỤNG):**
1. Trong MainWindow.axaml.cs, xoá bỏ hoàn toàn việc gọi camera.Start() và camera.Stop(). Chỉ truyền object qua cho form View.
2. Trong rmViewCamera.axaml.cs, dùng CameraRtspUrlBuilderFactory.TryBuild để build trực tiếp URL thay vì phụ thuộc vào luồng của MainWindow. Điều này giúp chỉ có DUY NHẤT một luồng AnvPlayer (của UI) được kích hoạt.
3. Trong rmViewCamera.axaml.cs, thêm cơ chế Watchdog vào timer (interval 300ms): Nếu không có khung hình nào mới (frame == null) trong vòng 8 giây, tự động gọi _player.Stop() để tránh rò rỉ bộ nhớ do FFmpeg reconnect loop, đồng thời hiển thị trạng thái Offline.

**Bài học tổng quát:**
Tuyệt đối không gọi KzCamera.Start() nếu chỉ cần lấy URL/StreamInfo (sẽ khởi động ngầm một decoder pipeline cực nặng). Luôn thêm Watchdog Timer (timeout tuỳ ý, khuyến nghị 8s) để Stop() các Native Player nếu chúng rớt mạng, tránh tình trạng rò rỉ không giới hạn của FFmpeg khi reconnect.
