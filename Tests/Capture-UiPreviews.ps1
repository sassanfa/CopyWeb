param(
    [string]$Executable = "$PSScriptRoot\..\bin\Debug\net10.0-windows\CopyWeb.exe",
    [string]$OutputDirectory = "$PSScriptRoot\.."
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Text;
using System.Runtime.InteropServices;

public static class CopyWebPreviewNative
{
    public delegate bool EnumProc(IntPtr hwnd, IntPtr state);
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

    [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr parent, EnumProc callback, IntPtr state);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int length);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr hwnd, StringBuilder text, int length);
    [DllImport("user32.dll")] public static extern IntPtr GetParent(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hwnd, int command);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);

    private static IntPtr root;
    private static IntPtr found;
    private static string expected = string.Empty;
    private static bool requireButton;

    private static bool VisibleThroughRoot(IntPtr hwnd)
    {
        var current = hwnd;
        while (current != IntPtr.Zero)
        {
            if (!IsWindowVisible(current)) return false;
            if (current == root) return true;
            current = GetParent(current);
        }
        return false;
    }

    private static bool FindCallback(IntPtr hwnd, IntPtr state)
    {
        var text = new StringBuilder(256);
        var className = new StringBuilder(256);
        GetWindowText(hwnd, text, text.Capacity);
        GetClassName(hwnd, className, className.Capacity);
        if (VisibleThroughRoot(hwnd) &&
            (!requireButton || className.ToString().IndexOf("Button", StringComparison.OrdinalIgnoreCase) >= 0) &&
            string.Equals(text.ToString(), expected, StringComparison.Ordinal))
        {
            found = hwnd;
            return false;
        }
        return true;
    }

    public static IntPtr Find(IntPtr parent, string caption, bool button)
    {
        root = parent;
        found = IntPtr.Zero;
        expected = caption;
        requireButton = button;
        EnumChildWindows(parent, FindCallback, IntPtr.Zero);
        return found;
    }
}
'@

function Save-WindowPreview([IntPtr]$Handle, [string]$Path) {
    $rect = New-Object CopyWebPreviewNative+RECT
    [CopyWebPreviewNative]::GetWindowRect($Handle, [ref]$rect) | Out-Null
    $bitmap = New-Object System.Drawing.Bitmap ($rect.Right - $rect.Left), ($rect.Bottom - $rect.Top)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $hdc = $graphics.GetHdc()
        try { [CopyWebPreviewNative]::PrintWindow($Handle, $hdc, 2) | Out-Null }
        finally { $graphics.ReleaseHdc($hdc) }
    }
    finally { $graphics.Dispose() }
    try { $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png) }
    finally { $bitmap.Dispose() }
}

if (-not (Test-Path -LiteralPath $Executable)) { throw "CopyWeb executable not found: $Executable" }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$dashboardPath = Join-Path $OutputDirectory 'main-ui-qa.png'
$advancedPath = Join-Path $OutputDirectory 'advanced-ui-qa.png'
$advancedCaption = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('2KrZhti424zZhdin2Kog2b7bjNi02LHZgdiq2Yc='))
$editorCaption = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('2KrZhti424zZhdin2Kog2b7YsdmI2pjZhyDYrNiv24zYrw=='))
$closeCaption = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('w5c='))

$process = Start-Process -FilePath $Executable -PassThru
try {
    $main = [IntPtr]::Zero
    for ($attempt = 0; $attempt -lt 30 -and $main -eq [IntPtr]::Zero; $attempt++) {
        Start-Sleep -Milliseconds 250
        $process.Refresh()
        $main = $process.MainWindowHandle
    }
    if ($main -eq [IntPtr]::Zero) { throw 'CopyWeb main window was not created.' }
    [CopyWebPreviewNative]::ShowWindow($main, 9) | Out-Null
    [CopyWebPreviewNative]::SetForegroundWindow($main) | Out-Null
    Start-Sleep -Seconds 2
    Save-WindowPreview $main $dashboardPath

    $editor = [IntPtr]::Zero
    for ($clickAttempt = 0; $clickAttempt -lt 3 -and $editor -eq [IntPtr]::Zero; $clickAttempt++) {
        $advanced = [CopyWebPreviewNative]::Find($main, $advancedCaption, $true)
        if ($advanced -eq [IntPtr]::Zero) { throw 'Advanced settings button was not found.' }
        [CopyWebPreviewNative]::SendMessage($advanced, 0x00F5, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
        for ($wait = 0; $wait -lt 12 -and $editor -eq [IntPtr]::Zero; $wait++) {
            Start-Sleep -Milliseconds 250
            $editor = [CopyWebPreviewNative]::Find($main, $editorCaption, $false)
        }
    }
    if ($editor -eq [IntPtr]::Zero) { throw 'Advanced project editor did not open.' }
    Start-Sleep -Milliseconds 500
    Save-WindowPreview $main $advancedPath

    # Returning to the dashboard also forces a full real navigation repaint,
    # which is more reliable than PrintWindow during the initial fade/layout.
    $close = [CopyWebPreviewNative]::Find($main, $closeCaption, $true)
    if ($close -eq [IntPtr]::Zero) { throw 'Advanced project editor close button was not found.' }
    [CopyWebPreviewNative]::SendMessage($close, 0x00F5, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
    Start-Sleep -Seconds 2
    Save-WindowPreview $main $dashboardPath
    Write-Output $dashboardPath
    Write-Output $advancedPath
}
finally {
    if ($process -and -not $process.HasExited) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
}
