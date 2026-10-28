param([switch]$AcceptDownload)
$ErrorActionPreference = 'Stop'
if (-not $AcceptDownload) { throw 'Téléchargement vocal de 497 Mo : accord explicite requis. Consultez docs/VOICE.md puis utilisez -AcceptDownload.' }
$root = Join-Path $env:LOCALAPPDATA 'IRIS'
$packages = Join-Path $root 'packages'
$models = Join-Path $root 'models'
$runtime = Join-Path $root 'runtimes/whisper-b5454'
$licenses = Join-Path $root 'licenses'
New-Item -ItemType Directory -Path $packages,$models,$licenses -Force | Out-Null
function Get-VerifiedSpeechFile([string]$Url,[string]$Target,[string]$Sha) {
    if (Test-Path -LiteralPath $Target) {
        if ((Get-FileHash -LiteralPath $Target -Algorithm SHA256).Hash -ne $Sha) { throw "Fichier existant différent, conservé : $Target" }
        return
    }
    $partial = "$Target.$([Guid]::NewGuid().ToString('N')).part"
    Write-Output "Téléchargement autorisé : $Url"
    Invoke-WebRequest -Uri $Url -OutFile $partial
    if ((Get-FileHash -LiteralPath $partial -Algorithm SHA256).Hash -ne $Sha) { throw "SHA-256 incorrect ; fichier non activé : $partial" }
    [IO.File]::Move($partial,$Target)
    Write-Output "PASS SHA-256 : $Target"
}
$archive = Join-Path $packages 'whisper-b5454-x64.zip'
Get-VerifiedSpeechFile 'https://github.com/ggml-org/whisper.cpp/releases/download/b5454/whisper-bin-x64.zip' $archive '6ba69e3482d7826214f90a6a9c84ca07782aec1e1d0c6a7c30c994fd5d816ccb'
if (-not (Test-Path -LiteralPath $runtime)) {
    $zip = [IO.Compression.ZipFile]::OpenRead($archive)
    try {
        $base = [IO.Path]::GetFullPath($runtime).TrimEnd('\') + '\'
        foreach ($entry in $zip.Entries) {
            $target = [IO.Path]::GetFullPath((Join-Path $runtime $entry.FullName))
            if (-not $target.StartsWith($base,[StringComparison]::OrdinalIgnoreCase) -or $entry.FullName.Contains(':')) { throw 'Archive vocale hors périmètre' }
        }
    } finally { $zip.Dispose() }
    Expand-Archive -LiteralPath $archive -DestinationPath $runtime
}
Get-VerifiedSpeechFile 'https://huggingface.co/ggerganov/whisper.cpp/resolve/5359861c739e955e79d9a303bcbc70fb988958b1/ggml-small.bin' (Join-Path $models 'ggml-small.bin') '1be3a9b2063867b937e64e2ec7483364a79917e157fa98c5d94b5c1fffea987b'
$license = Join-Path $licenses 'whisper.cpp-b5454-MIT.txt'
if (-not (Test-Path -LiteralPath $license)) { Invoke-WebRequest -Uri 'https://raw.githubusercontent.com/ggml-org/whisper.cpp/b5454/LICENSE' -OutFile $license }
Write-Output 'Acquisition terminée. Aucun microphone ouvert ni moteur lancé.'
