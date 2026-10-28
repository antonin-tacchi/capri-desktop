param([switch]$RuntimeOnly)
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$irisDotNetCandidates = @()
foreach ($irisDotNetRoot in @(
    $env:DOTNET_ROOT_X64, $env:DOTNET_ROOT,
    [Environment]::GetEnvironmentVariable('DOTNET_ROOT', 'User'),
    [Environment]::GetEnvironmentVariable('DOTNET_ROOT', 'Machine'),
    (Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Microsoft\dotnet'),
    (Join-Path ([Environment]::GetFolderPath('UserProfile')) '.dotnet'),
    (Join-Path ([Environment]::GetFolderPath('UserProfile')) 'AppData\Local\Microsoft\dotnet'),
    $env:ProgramW6432, $env:ProgramFiles
)) {
    if (-not [string]::IsNullOrWhiteSpace($irisDotNetRoot)) {
        $irisDotNetCandidates += Join-Path $irisDotNetRoot 'dotnet.exe'
        $irisDotNetCandidates += Join-Path $irisDotNetRoot 'dotnet\dotnet.exe'
    }
}
$irisDotNetCandidates += @(Get-Command dotnet.exe -CommandType Application -All -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source)
$irisDotNetCandidates = @($irisDotNetCandidates | Select-Object -Unique)
$IrisDotNetExecutable = $null
$irisDotNetDiagnostics = @()
Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    foreach ($irisDotNetCandidate in ($irisDotNetCandidates | Select-Object -Unique)) {
        if (-not (Test-Path -LiteralPath $irisDotNetCandidate -PathType Leaf)) { continue }
        try {
            # IRIS's apphost is Windows x64. Do not select an x86 host from a 32-bit shell.
            $irisHostReader = [IO.BinaryReader]::new([IO.File]::OpenRead($irisDotNetCandidate))
            try {
                $irisHostReader.BaseStream.Position = 0x3c
                $irisHostOffset = $irisHostReader.ReadInt32()
                $irisHostReader.BaseStream.Position = $irisHostOffset
                $irisHostSignature = $irisHostReader.ReadUInt32()
                $irisHostMachine = $irisHostReader.ReadUInt16()
                if ($irisHostSignature -ne 17744 -or $irisHostMachine -ne 34404) {
                    $irisDotNetDiagnostics += $irisDotNetCandidate + " : hôte non x64 (signature=$irisHostSignature, machine=$irisHostMachine)."
                    continue
                }
            } finally { $irisHostReader.Dispose() }
            if ($RuntimeOnly) {
                $irisProbe = @(& $irisDotNetCandidate --list-runtimes 2>&1)
                $irisUsable = $LASTEXITCODE -eq 0 -and ($irisProbe -match '^Microsoft.WindowsDesktop.App 10\.0\.\d+ ')
            } else {
                # Run inside the repository so global.json selects the compatible SDK.
                $irisProbe = @(& $irisDotNetCandidate --version 2>&1)
                $irisUsable = $LASTEXITCODE -eq 0 -and ($irisProbe -match '^10\.\d+\.\d+$')
            }
            if ($irisUsable) { $IrisDotNetExecutable = [IO.Path]::GetFullPath($irisDotNetCandidate); break }
            $irisDotNetDiagnostics += $irisDotNetCandidate + ' : composant .NET 10 compatible indisponible.'
        } catch { $irisDotNetDiagnostics += $irisDotNetCandidate + ' : impossible de vérifier cette installation.' }
    }
} finally { Pop-Location }
if (-not $IrisDotNetExecutable) {
    $irisRequirement = if ($RuntimeOnly) { 'Runtime Windows Desktop .NET 10 x64' } else { 'SDK .NET 10 x64 compatible avec global.json' }
    throw ($irisRequirement + " introuvable. Aucun composant installé automatiquement.`nChemins examinés :`n" + ($irisDotNetCandidates -join "`n") + "`n" + ($irisDotNetDiagnostics -join "`n"))
}
$env:DOTNET_ROOT = Split-Path -Parent $IrisDotNetExecutable
$env:DOTNET_ROOT_X64 = $env:DOTNET_ROOT
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
