param([switch]$UsePowerShellCompiler)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $UsePowerShellCompiler) {
    . (Join-Path $PSScriptRoot 'Initialize-DotNet.ps1')
    & $IrisDotNetExecutable run --project (Join-Path $root 'tests/IRIS.SecurityTests') -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Échec des tests IRIS.' }
    return
}
if ([Environment]::Version.Major -lt 10) { throw 'Le compilateur de secours nécessite PowerShell exécuté sur .NET 10.' }
$sources = @('IRIS.Core', 'IRIS.Security', 'IRIS.Actions', 'IRIS.AI', 'IRIS.Infrastructure', 'IRIS.Memory') | ForEach-Object {
    Get-ChildItem -LiteralPath (Join-Path $root "src/$_") -Recurse -Filter *.cs | Where-Object { $_.FullName -notmatch '[\\/](obj|bin)[\\/]' }
}
$sources += Get-Item -LiteralPath (Join-Path $root 'tests/IRIS.SecurityTests/SecurityChecks.cs')
$sources += Get-Item -LiteralPath (Join-Path $root 'tests/IRIS.SecurityTests/LocalInferenceChecks.cs')
$sources += Get-Item -LiteralPath (Join-Path $root 'tests/IRIS.SecurityTests/MemoryChecks.cs')
Add-Type -Path $sources.FullName -CompilerOptions '/nullable:enable', '/warnaserror'
$results = @([IRIS.SecurityTests.SecurityChecks]::RunAsync().GetAwaiter().GetResult()) + @([IRIS.SecurityTests.LocalInferenceChecks]::RunAsync().GetAwaiter().GetResult())
$results += @([IRIS.SecurityTests.MemoryChecks]::RunAsync().GetAwaiter().GetResult())
$results
Write-Output "$($results.Length) contrôles réussis via Roslyn/PowerShell sur $([System.Runtime.InteropServices.RuntimeInformation]::FrameworkDescription)."
Write-Output 'LIMIT : ceci ne valide ni les projets MSBuild ni WinForms. Exécutez ensuite scripts/Build.ps1 avec le SDK .NET 10.'
