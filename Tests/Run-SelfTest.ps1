param(
    [string]$Project = "$PSScriptRoot\..\CopyWeb.csproj"
)

$ErrorActionPreference = 'Stop'
dotnet build $Project --configuration Debug --no-restore
$exe = Join-Path (Split-Path $Project) 'bin\Debug\net10.0-windows\CopyWeb.exe'
if (-not (Test-Path -LiteralPath $exe)) {
    throw "Build output was not found: $exe"
}
& $exe --cli --self-test
if ($LASTEXITCODE -ne 0) { throw "CopyWeb self-test failed with exit code $LASTEXITCODE" }
Write-Host 'CopyWeb automated smoke tests passed.'
