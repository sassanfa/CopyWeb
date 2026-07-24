param(
    [string]$Executable = "$PSScriptRoot\..\bin\Debug\net10.0-windows\CopyWeb.exe"
)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $Executable)) { throw "CopyWeb executable not found: $Executable" }

Add-Type @'
using System;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public static class CopyWebNavigationProbe
{
    private delegate bool EnumProc(IntPtr hwnd, IntPtr state);
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr parent, EnumProc callback, IntPtr state);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int length);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hwnd, StringBuilder text, int length);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern IntPtr GetParent(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hwnd, out Rect rect);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private static string expectedText;
    private static IntPtr foundHandle;
    private static List<string> visibleTexts;
    private static bool requireButton;
    private static IntPtr searchRoot;

    private static bool IsEffectivelyVisible(IntPtr hwnd)
    {
        var current = hwnd;
        while (current != IntPtr.Zero)
        {
            if (!IsWindowVisible(current)) return false;
            if (current == searchRoot) return true;
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
        if (IsEffectivelyVisible(hwnd) && (!requireButton || className.ToString().IndexOf("Button", StringComparison.OrdinalIgnoreCase) >= 0) && string.Equals(text.ToString(), expectedText, StringComparison.Ordinal))
        {
            foundHandle = hwnd;
            return false;
        }
        return true;
    }

    private static bool CollectCallback(IntPtr hwnd, IntPtr state)
    {
        if (IsEffectivelyVisible(hwnd))
        {
            var text = new StringBuilder(256);
            GetWindowText(hwnd, text, text.Capacity);
            if (text.Length > 0) visibleTexts.Add(text.ToString());
        }
        return true;
    }

    public static string[] VisibleTexts(IntPtr parent)
    {
        searchRoot = parent;
        visibleTexts = new List<string>();
        EnumChildWindows(parent, CollectCallback, IntPtr.Zero);
        return visibleTexts.ToArray();
    }

    public static IntPtr FindVisibleChild(IntPtr parent, string expected)
    {
        searchRoot = parent;
        expectedText = expected;
        foundHandle = IntPtr.Zero;
        requireButton = false;
        EnumChildWindows(parent, FindCallback, IntPtr.Zero);
        return foundHandle;
    }

    public static IntPtr FindVisibleButton(IntPtr parent, string expected)
    {
        searchRoot = parent;
        expectedText = expected;
        foundHandle = IntPtr.Zero;
        requireButton = true;
        EnumChildWindows(parent, FindCallback, IntPtr.Zero);
        return foundHandle;
    }

    public static void Click(IntPtr hwnd)
    {
        // BM_CLICK is not consistently delivered to every user-painted
        // WinForms button. Send a real client-area mouse click as a fallback.
        SendMessage(hwnd, 0x00F5, IntPtr.Zero, IntPtr.Zero);
        Rect rect;
        if (GetClientRect(hwnd, out rect))
        {
            int x = Math.Max(1, (rect.Right - rect.Left) / 2);
            int y = Math.Max(1, (rect.Bottom - rect.Top) / 2);
            IntPtr point = new IntPtr((y << 16) | (x & 0xffff));
            SendMessage(hwnd, 0x0201, new IntPtr(1), point);
            SendMessage(hwnd, 0x0202, IntPtr.Zero, point);
        }
    }
}
'@

$process = Start-Process -FilePath $Executable -PassThru
try {
    # Keep the script ASCII-safe for Windows PowerShell 5, which otherwise
    # reads UTF-8-without-BOM Persian literals using the active ANSI code page.
    $advancedCaption = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('2KrZhti424zZhdin2Kog2b7bjNi02LHZgdiq2Yc='))
    $editorCaption = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('2KrZhti424zZhdin2Kog2b7YsdmI2pjZhyDYrNiv24zYrw=='))
    $dashboardCaption = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('2K/Yp9i02KjZiNix2K8='))
    $closeCaption = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('w5c='))
    $legacyCaption = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('2LTYsdmI2Lkg2K/Yp9mG2YTZiNivINiz2KfbjNiq'))
    # Version 1.3.6 intentionally exposes the animated splash as the first
    # process window. Keep refreshing MainWindowHandle until the real dashboard
    # replaces it and its advanced-settings action is available.
    $main = [IntPtr]::Zero
    $advanced = [IntPtr]::Zero
    for ($attempt = 0; $attempt -lt 60 -and $advanced -eq [IntPtr]::Zero; $attempt++) {
        Start-Sleep -Milliseconds 300
        $process.Refresh()
        $candidate = $process.MainWindowHandle
        if ($candidate -eq [IntPtr]::Zero) { continue }
        $advanced = [CopyWebNavigationProbe]::FindVisibleButton($candidate, $advancedCaption)
        if ($advanced -ne [IntPtr]::Zero) { $main = $candidate }
    }
    if ($main -eq [IntPtr]::Zero) { throw 'CopyWeb dashboard was not created after the startup splash.' }
    if ($advanced -eq [IntPtr]::Zero) {
        $visible = [CopyWebNavigationProbe]::VisibleTexts($main) -join ' | '
        throw "Advanced settings button is missing from the dashboard. Visible: $visible"
    }
    $editor = [IntPtr]::Zero
    # The first WM_PAINT/Layout pass can overlap the first synthetic click on
    # slower machines. Retry the idempotent OpenProjectEditor action only while
    # the dashboard button is still effectively visible.
    Start-Sleep -Milliseconds 750
    for ($clickAttempt = 0; $clickAttempt -lt 3 -and $editor -eq [IntPtr]::Zero; $clickAttempt++) {
        $advanced = [CopyWebNavigationProbe]::FindVisibleButton($main, $advancedCaption)
        if ($advanced -eq [IntPtr]::Zero) { break }
        [CopyWebNavigationProbe]::Click($advanced)
        for ($attempt = 0; $attempt -lt 12 -and $editor -eq [IntPtr]::Zero; $attempt++) {
            Start-Sleep -Milliseconds 250
            $editor = [CopyWebNavigationProbe]::FindVisibleChild($main, $editorCaption)
        }
    }

    if ($editor -eq [IntPtr]::Zero) {
        $visible = [CopyWebNavigationProbe]::VisibleTexts($main) -join ' | '
        throw "Project editor did not open. Visible: $visible"
    }
    if ([CopyWebNavigationProbe]::FindVisibleChild($main, $dashboardCaption) -ne [IntPtr]::Zero) { throw 'Dashboard remained visible behind the project editor.' }

    $close = [CopyWebNavigationProbe]::FindVisibleButton($main, $closeCaption)
    if ($close -eq [IntPtr]::Zero) { throw 'Project editor close button is missing.' }
    [CopyWebNavigationProbe]::Click($close)
    Start-Sleep -Milliseconds 600

    if ([CopyWebNavigationProbe]::FindVisibleChild($main, $dashboardCaption) -eq [IntPtr]::Zero) { throw 'Dashboard did not return after closing advanced settings.' }
    if ([CopyWebNavigationProbe]::FindVisibleChild($main, $legacyCaption) -ne [IntPtr]::Zero) { throw 'Legacy UI became visible after returning to the dashboard.' }
    if ([CopyWebNavigationProbe]::FindVisibleChild($main, $editorCaption) -ne [IntPtr]::Zero) { throw 'Project editor remained visible after returning to the dashboard.' }

    Write-Host 'CopyWeb dashboard/advanced navigation smoke test passed.'
}
finally {
    if ($process -and -not $process.HasExited) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
}
