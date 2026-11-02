# Documentation Writer — Screenshot & Build Guide (Reference)

> File này chứa hướng dẫn kỹ thuật chi tiết về build ứng dụng và chụp screenshot. Được tách từ `documentation-writer.md` theo nguyên tắc Progressive Disclosure — chỉ đọc khi cần, không tải vào context mặc định.
>
> Đọc file này khi: bắt đầu Bước 0.5 (chạy ứng dụng thật) hoặc Bước 2 (chụp màn hình).

---

## A. Build Windows Forms (Release)

```powershell
# Phương án 1: Dùng build.ps1 nếu có sẵn trong project
.\build.ps1

# Phương án 2: dotnet publish (tạo thư mục publish sạch)
dotnet publish -c Release -o ./publish

# Phương án 3: MSBuild trực tiếp
msbuild /p:Configuration=Release /p:OutputPath=./bin/Release/

# Xác nhận build thành công
Get-ChildItem -Path "./publish","./bin/Release" -Filter "*.exe" -Recurse |
    Select-Object Name, LastWriteTime, @{N='SizeMB';E={[Math]::Round($_.Length/1MB,2)}}
```

**Quy tắc:** Phải chạy đến khi xuất hiện thông báo **Build succeeded**. Nếu lỗi → DỪNG, báo Senior Developer. Chạy `.exe` Release để xác nhận khởi động bình thường trước khi chụp.

```powershell
# Khởi động ứng dụng Release
$app = Start-Process ".\publish\[AppName].exe" -PassThru
Start-Sleep -Seconds 4
if ($app.HasExited) { Write-Error "ỨNG DỤNG KHÔNG KHỞI ĐỘNG ĐƯỢC — DỪNG LẠI" }
else { Write-Host "OK Ứng dụng đang chạy — PID: $($app.Id)" }
```

---

## B. Khởi động ứng dụng Web

```bash
# Khởi động server local
npm run dev        # hoặc: python manage.py runserver / dotnet run / ...

# Xác nhận đang chạy — phải trả về HTTP 200
curl -s -o /dev/null -w "%{http_code}" http://localhost:3000/
# Kết quả phải là: 200
```

---

## C. Chụp màn hình — Windows Forms (PowerShell)

```powershell
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

function Capture-Window {
    param([string]$ProcessName, [string]$OutputPath)
    $proc = Get-Process -Name $ProcessName -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $proc) { Write-Host "Không tìm thấy process: $ProcessName"; return }

    $hwnd = $proc.MainWindowHandle
    Add-Type @"
using System;
using System.Drawing;
using System.Runtime.InteropServices;
public class WinCapture {
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    public struct RECT { public int Left, Top, Right, Bottom; }
    public static Bitmap Capture(IntPtr hwnd) {
        SetForegroundWindow(hwnd);
        RECT r; GetWindowRect(hwnd, out r);
        var bmp = new Bitmap(r.Right-r.Left, r.Bottom-r.Top);
        using (var g = Graphics.FromImage(bmp))
            g.CopyFromScreen(r.Left, r.Top, 0, 0, bmp.Size);
        return bmp;
    }
}
"@
    $bmp = [WinCapture]::Capture($hwnd)
    $bmp.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "Da chup: $OutputPath"
}

# Ví dụ sử dụng
$screenshotDir = "docs\user-manuals\screenshots"
New-Item -ItemType Directory -Force -Path $screenshotDir | Out-Null
$app = Start-Process ".\publish\[AppName].exe" -PassThru
Start-Sleep -Seconds 3
Capture-Window -ProcessName "[AppName]" -OutputPath "$screenshotDir\main-default.png"
```

---

## D. Chụp màn hình — Web (Playwright)

```bash
pip install playwright && playwright install chromium
```

```python
from playwright.sync_api import sync_playwright
import os

SCREENS = [
    # (route, tên_file, mô_tả)
    ("/",           "home-default",      "Trang chủ"),
    ("/login",      "login-default",     "Đăng nhập — mặc định"),
    ("/login",      "login-error",       "Đăng nhập — lỗi"),
    ("/dashboard",  "dashboard-default", "Dashboard"),
    # ... thêm TẤT CẢ route ở đây
]

BASE_URL = "http://localhost:3000"
OUT_DIR  = "docs/user-manuals/screenshots"
os.makedirs(OUT_DIR, exist_ok=True)

with sync_playwright() as p:
    browser = p.chromium.launch()
    page = browser.new_page(viewport={"width": 1366, "height": 768})
    for route, fname, desc in SCREENS:
        page.goto(f"{BASE_URL}{route}")
        page.wait_for_load_state("networkidle")
        path = f"{OUT_DIR}/{fname}.png"
        page.screenshot(path=path, full_page=True)
        print(f"OK {desc:30s} -> {path}")
    browser.close()
print(f"\nHoan thanh: {len(SCREENS)} screenshots")
```

---

## E. Tiền điều kiện chạy script chuyển đổi DOCX (Windows)

```powershell
# 1. Đặt encoding UTF-8 (BẮT BUỘC trên Windows — thiếu sẽ crash khi in tiếng Việt)
$env:PYTHONIOENCODING = "utf-8"

# 2. Kiểm tra file output KHÔNG bị mở trong Word
$outputFile = "docs/user-manuals/MANUAL-ten-file.docx"
try {
    $s = [System.IO.File]::Open($outputFile,'Open','ReadWrite','None')
    $s.Close()
    Write-Host "OK — file không bị lock"
} catch {
    Write-Error "FILE ĐANG BỊ KHÓA — Đóng file trong Word trước"
}
```

**Lỗi thường gặp trên Windows:**
- `UnicodeEncodeError: 'charmap'` → Thiếu `$env:PYTHONIOENCODING = "utf-8"`
- `[Errno 13] Permission denied` → File DOCX đang mở trong Word → Đóng lại
- `(-2147023170, ...)` từ docx2pdf → kill WINWORD.EXE rồi thử lại

---

## F. Quét màn hình theo loại ứng dụng

```powershell
# Avalonia — liệt kê tất cả Window, UserControl, Page từ file AXAML
Get-ChildItem -Recurse -Filter "*.axaml" |
    Select-String -Pattern "<(Window|UserControl|Page)" |
    ForEach-Object { "$($_.Filename):$($_.LineNumber) — $($_.Line.Trim())" }

# Windows Forms — liệt kê tất cả Form
Get-ChildItem -Recurse -Filter "*.cs" |
    Select-String -Pattern "class \w+ *: *(Form|UserControl|MetroForm|BaseForm)" |
    ForEach-Object { "$($_.Filename):$($_.LineNumber) — $($_.Line.Trim())" }

# Next.js / React Router
Get-ChildItem -Recurse -Include "*.tsx","*.jsx" -Path "src/pages","src/app","src/routes"

# ASP.NET MVC
Get-ChildItem -Recurse -Filter "*.cshtml" | Select-Object FullName
```

---

## G. Xuất PDF (các phương án)

```powershell
# Phương án 1: docx2pdf (Windows/macOS — cần MS Word)
pip install docx2pdf
$env:PYTHONIOENCODING = "utf-8"
python C:/Users/Admin/.gemini/scripts/md_to_docx_kztek.py docs/user-manuals/MANUAL-[feature-slug].md

# Phương án 2: LibreOffice (Linux)
soffice --headless --convert-to pdf docs/user-manuals/MANUAL-[feature-slug].docx --outdir docs/user-manuals/

# Phương án 3: pypandoc
pip install pypandoc
python -c "import pypandoc; pypandoc.convert_file('docs/user-manuals/MANUAL-[feature-slug].md', 'pdf', outputfile='docs/user-manuals/MANUAL-[feature-slug].pdf', extra_args=['--pdf-engine=xelatex'])"
```

---

## H. Render UI ứng dụng Avalonia bằng Avalonia.Headless

### 1. Khi nào dùng
BẮT BUỘC cho toàn bộ ứng dụng Avalonia khi làm tài liệu hướng dẫn. Tránh được hoàn toàn các lỗi gotcha Win32/X11 (lệch DPI, mất focus, Alt+F4 sai cửa sổ, không chạy được trên máy không có display).

### 2. Nguyên lý hoạt động
Avalonia cung cấp package `Avalonia.Headless` cho phép khởi tạo ứng dụng Avalonia mà không cần hiển thị cửa sổ vật lý, vẽ trực tiếp vào framebuffer trong bộ nhớ và chụp ảnh frame bằng `window.CaptureRenderedFrame()` hoặc `WriteableBitmap`.

### 3. Hướng dẫn viết script render UI Headless (nếu project chưa có sẵn)

Documentation Writer có thể tạo một console runner hoặc test project nhỏ (ví dụ `tools/HeadlessDocRenderer/` hoặc script C#) với các bước:

**Bước 1: Cấu hình project (`HeadlessDocRenderer.csproj`)**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Avalonia.Headless" Version="11.*" />
    <PackageReference Include="Avalonia.Skia" Version="11.*" />
    <!-- Tham chiếu đến project Avalonia chính của giải pháp -->
    <ProjectReference Include="..\..\src\MyApp\MyApp.csproj" />
  </ItemGroup>
</Project>
```

**Bước 2: Viết mã nguồn render (`Program.cs`)**
```csharp
using System;
using System.IO;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using MyApp; // Namespace ứng dụng Avalonia chính
using MyApp.Views;
using MyApp.ViewModels;

namespace HeadlessDocRenderer;

class Program
{
    static void Main(string[] args)
    {
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = true
            })
            .AfterSetup(_ =>
            {
                RenderAllScreens();
            });

    static void RenderAllScreens()
    {
        var outDir = Path.GetFullPath("docs/user-manuals/screenshots");
        Directory.CreateDirectory(outDir);

        // 1. Render MainWindow (Default)
        var mainWin = new MainWindow
        {
            DataContext = new MainViewModel(),
            Width = 1366,
            Height = 768
        };
        mainWin.Show();
        SaveFrame(mainWin, Path.Combine(outDir, "main-default.png"));

        // 2. Render Form nhập liệu (Filled State)
        var userVm = new UserEditViewModel { UserName = "NguyenVanA", Role = "Admin" };
        var userWin = new UserEditWindow { DataContext = userVm, Width = 800, Height = 600 };
        userWin.Show();
        SaveFrame(userWin, Path.Combine(outDir, "user-edit-filled.png"));

        // 3. Render Form lỗi validation (Error State)
        var errorVm = new UserEditViewModel { UserName = "" };
        errorVm.Validate(); // Kích hoạt trạng thái lỗi
        var errorWin = new UserEditWindow { DataContext = errorVm, Width = 800, Height = 600 };
        errorWin.Show();
        SaveFrame(errorWin, Path.Combine(outDir, "user-edit-error-required.png"));

        Console.WriteLine("[Headless] Render hoàn tất 100% screenshots!");
        Environment.Exit(0);
    }

    static void SaveFrame(Avalonia.Controls.Window window, string outputPath)
    {
        using var frame = window.CaptureRenderedFrame();
        frame.Save(outputPath);
        Console.WriteLine($"Đã render: {outputPath}");
    }
}
```

**Bước 3: Chạy script render**
```powershell
dotnet run --project tools/HeadlessDocRenderer/HeadlessDocRenderer.csproj
```
