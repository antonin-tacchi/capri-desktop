param()
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'Initialize-DotNet.ps1')
& (Join-Path $PSScriptRoot 'Prepare-EngineAssets.ps1')
$destination = Join-Path $root 'out/IRIS-portable-win-x64-0.7.1'
& $IrisDotNetExecutable publish (Join-Path $root 'src/IRIS.Desktop/IRIS.Desktop.csproj') -c Release -r win-x64 --self-contained true -o $destination -p:PublishSingleFile=false -p:RuntimeFrameworkVersion=10.0.14
if ($LASTEXITCODE -ne 0) { throw 'Publication autonome IRIS échouée.' }
Copy-Item -LiteralPath (Join-Path $root 'LICENSE.md'), (Join-Path $root 'THIRD_PARTY_NOTICES.md') -Destination $destination -Force
$licenseDirectory = Join-Path $destination 'licenses'
New-Item -ItemType Directory -Path $licenseDirectory -Force | Out-Null
$cacheInfo = @(& $IrisDotNetExecutable nuget locals global-packages --list)
if ($LASTEXITCODE -ne 0) { throw 'Cache des licences NuGet inaccessible.' }
$cache = ($cacheInfo | Where-Object { $_ -match '^global-packages:\s*(.+)$' } | Select-Object -First 1) -replace '^global-packages:\s*', ''
if (-not $cache) { throw 'Dossier des licences NuGet introuvable.' }
$licenseFiles = @{
    'microsoft.netcore.app.runtime.win-x64/10.0.14/LICENSE.TXT' = 'dotnet-runtime-LICENSE.txt'
    'microsoft.netcore.app.runtime.win-x64/10.0.14/THIRD-PARTY-NOTICES.TXT' = 'dotnet-runtime-THIRD-PARTY-NOTICES.txt'
    'microsoft.windowsdesktop.app.runtime.win-x64/10.0.14/LICENSE' = 'windows-desktop-runtime-LICENSE.txt'
    'system.speech/10.0.14/system.speech.nuspec' = 'system-speech-package-license-metadata.xml'
    'system.speech/10.0.14/THIRD-PARTY-NOTICES.TXT' = 'system-speech-THIRD-PARTY-NOTICES.txt'
}
foreach ($entry in $licenseFiles.GetEnumerator()) {
    $source = Join-Path $cache $entry.Key
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "Notice requise absente : $source" }
    Copy-Item -LiteralPath $source -Destination (Join-Path $licenseDirectory $entry.Value) -Force
}
Write-Output "Version autonome prête : $destination"
