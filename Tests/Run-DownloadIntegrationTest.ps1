param(
    [string]$Project = "$PSScriptRoot\..\CopyWeb.csproj"
)

$ErrorActionPreference = 'Stop'
$python = Get-Command python -ErrorAction SilentlyContinue
if (-not $python) {
    Write-Warning 'Python was not found; local HTTP integration test was skipped.'
    exit 0
}

dotnet build $Project --configuration Debug --no-restore
$projectRoot = Split-Path $Project
$assembly = Join-Path $projectRoot 'bin\Debug\net10.0-windows\CopyWeb.dll'
$fixture = Join-Path $PSScriptRoot 'Fixtures\DedupSite'
$output = Join-Path ([IO.Path]::GetTempPath()) ('copyweb-integration-' + [Guid]::NewGuid().ToString('N'))
$port = Get-Random -Minimum 48100 -Maximum 48999
$server = $null

try {
    $server = Start-Process -FilePath $python.Source -ArgumentList @('-m', 'http.server', $port, '--bind', '127.0.0.1', '--directory', $fixture) -WindowStyle Hidden -PassThru
    Start-Sleep -Milliseconds 900
    # The application is built as WinExe. Running the DLL through dotnet keeps
    # CLI output and its exit code observable by this test runner.
    & dotnet $assembly --cli --url "http://127.0.0.1:$port/" --output $output --depth 1 --max-pages 5 --delay-ms 0 --concurrency 2
    if ($LASTEXITCODE -ne 0) { throw "CopyWeb CLI exited with code $LASTEXITCODE" }

    $webp = @(Get-ChildItem -LiteralPath (Join-Path $output 'Img') -Filter '*.webp' -File)
    if ($webp.Count -ne 3) { throw "Expected three unique WebP files (deduped hero, extensionless and inline JSON), found $($webp.Count)." }
    foreach ($image in $webp) {
        $bytes = [IO.File]::ReadAllBytes($image.FullName)
        if ($bytes.Length -lt 12 -or
            [Text.Encoding]::ASCII.GetString($bytes, 0, 4) -ne 'RIFF' -or
            [Text.Encoding]::ASCII.GetString($bytes, 8, 4) -ne 'WEBP') {
            throw "Saved file is not a real WebP image: $($image.Name)"
        }
    }
    $html = Get-Content -LiteralPath (Join-Path $output 'index.html') -Raw -Encoding UTF8
    if ($html -notmatch 'Img/.+\.webp') { throw 'Saved HTML does not point to the local WebP file.' }
    if ($html -match 'hero\.webp\?v=') { throw 'A remote/cache-busting WebP reference remains in saved HTML.' }
    if ($html -match 'rendered-image\?format=webp') { throw 'The extensionless WebP URL was not rewritten to a local file.' }
    if ($html -match 'images/lazy\.webp') { throw 'The inline JSON WebP URL was not rewritten to a local file.' }
    $css = Get-Content -LiteralPath (Get-ChildItem -LiteralPath (Join-Path $output 'CSS') -Filter '*.css' -File | Select-Object -First 1).FullName -Raw -Encoding UTF8
    if ($css -notmatch '\.\./Img/.+\.webp') { throw 'Saved CSS does not point to the local WebP file.' }
    $checkpoint = Get-Content -LiteralPath (Join-Path $output 'links.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($null -eq $checkpoint.proxy) { throw 'Downloader checkpoint erased the project proxy snapshot.' }
    Write-Host 'CopyWeb WebP, srcset, CSS rewrite, dedup and proxy-checkpoint integration test passed.'
}
finally {
    if ($server -and -not $server.HasExited) { Stop-Process -Id $server.Id -Force -ErrorAction SilentlyContinue }
    if (Test-Path -LiteralPath $output) { Remove-Item -LiteralPath $output -Recurse -Force }
}
