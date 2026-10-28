param([switch]$AcceptDownload)
$ErrorActionPreference = 'Stop'
if (-not $AcceptDownload) { throw 'Acquisition Piper/Siwis de 86 Mo : autorisation explicite requise. Relancez avec -AcceptDownload après accord.' }
$root = Join-Path $env:LOCALAPPDATA 'IRIS'
New-Item -ItemType Directory -Path (Join-Path $root 'packages'),(Join-Path $root 'models'),(Join-Path $root 'licenses') -Force | Out-Null
function Get-VoiceFile([string]$Url,[string]$Target,[string]$Sha) {
    if (Test-Path -LiteralPath $Target) { if ((Get-FileHash -LiteralPath $Target -Algorithm SHA256).Hash -ne $Sha) { throw "Fichier différent conservé : $Target" }; return }
    $part = $Target + '.' + [Guid]::NewGuid().ToString('N') + '.part'
    Invoke-WebRequest -Uri $Url -OutFile $part
    if ((Get-FileHash -LiteralPath $part -Algorithm SHA256).Hash -ne $Sha) { throw "SHA-256 non conforme : $part" }
    [IO.File]::Move($part,$Target)
}
$archive = Join-Path $root 'packages/piper-2023.11.14-2-windows-amd64.zip'
Get-VoiceFile 'https://github.com/rhasspy/piper/releases/download/2023.11.14-2/piper_windows_amd64.zip' $archive 'f3c58906402b24f3a96d92145f58acba6d86c9b5db896d207f78dc80811efcea'
$modelUrl = 'https://huggingface.co/rhasspy/piper-voices/resolve/d5de91c972ba5a13cea0e77a63fda1623faac2c1/fr/fr_FR/siwis/medium/'
Get-VoiceFile ($modelUrl + 'fr_FR-siwis-medium.onnx') (Join-Path $root 'models/fr_FR-siwis-medium.onnx') '641d1ab097da2b81128c076810edb052b385decc8be3381814802a64a73baf99'
Get-VoiceFile ($modelUrl + 'fr_FR-siwis-medium.onnx.json') (Join-Path $root 'models/fr_FR-siwis-medium.onnx.json') '39479916c2db192b5ac9764daddd0c744d83e023ad890c6976c0633ae4df8959'
$runtime = Join-Path $root 'runtimes/piper-2023.11.14-2'
if (-not (Test-Path -LiteralPath $runtime)) {
    $zip = [IO.Compression.ZipFile]::OpenRead($archive)
    try { foreach($entry in $zip.Entries) { $path=[IO.Path]::GetFullPath((Join-Path $runtime $entry.FullName)); if (-not $path.StartsWith([IO.Path]::GetFullPath($runtime).TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase) -or $entry.FullName.Contains(':')) {throw 'Archive vocale hors périmètre'} } } finally {$zip.Dispose()}
    Expand-Archive -LiteralPath $archive -DestinationPath $runtime
}
$notices = @{
 'piper-2023.11.14-2-MIT.txt'='https://raw.githubusercontent.com/rhasspy/piper/2023.11.14-2/LICENSE.md'
 'espeak-ng-GPL-3.txt'='https://raw.githubusercontent.com/espeak-ng/espeak-ng/1.51/COPYING'
 'onnxruntime-MIT.txt'='https://raw.githubusercontent.com/microsoft/onnxruntime/v1.14.1/LICENSE'
 'siwis-medium-MODEL_CARD.txt'=($modelUrl + 'MODEL_CARD')
}
foreach($entry in $notices.GetEnumerator()) { $target=Join-Path $root ('licenses/'+$entry.Key); if(-not(Test-Path -LiteralPath $target)){Invoke-WebRequest -Uri $entry.Value -OutFile $target} }
Write-Output 'Voix locale acquise et épinglée ; aucun service ni microphone démarré.'
