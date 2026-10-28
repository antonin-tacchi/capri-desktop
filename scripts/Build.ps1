param()
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'Initialize-DotNet.ps1')
& $IrisDotNetExecutable build (Join-Path $root 'IRIS.sln') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Compilation IRIS échouée.' }
& (Join-Path $PSScriptRoot 'Test-Security.ps1')
& $IrisDotNetExecutable run --project (Join-Path $root 'tests/IRIS.DesktopTests') -c Release --no-build -- (Join-Path $root 'out/ui')
if ($LASTEXITCODE -ne 0) { throw 'Échec des tests WinForms.' }
& $IrisDotNetExecutable run --project (Join-Path $root 'tests/IRIS.VoiceTests') -c Release --no-build
if ($LASTEXITCODE -ne 0) { throw 'Échec des tests vocaux simulés.' }
& (Join-Path $PSScriptRoot 'Publish-Portable.ps1')
