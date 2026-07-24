param(
    [Parameter(Mandatory = $true)][string]$Executable,
    [Parameter(Mandatory = $true)][string]$ProjectFile,
    [string]$Output = "$PSScriptRoot\..\site-map-ui-qa.png"
)

$ErrorActionPreference = 'Stop'
$exe = (Resolve-Path -LiteralPath $Executable).Path
$outputPath = [IO.Path]::GetFullPath($Output)
$process = Start-Process -FilePath $exe -ArgumentList @('--qa-site-map', $ProjectFile, $outputPath) -PassThru -WindowStyle Hidden
if (-not $process.WaitForExit(30000)) {
    Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    throw 'Site-map preview timed out.'
}
if ($process.ExitCode -ne 0) { throw "Site-map preview failed with exit code $($process.ExitCode)." }
if (-not (Test-Path -LiteralPath $outputPath)) { throw 'Site-map preview image was not created.' }
Write-Output $outputPath
