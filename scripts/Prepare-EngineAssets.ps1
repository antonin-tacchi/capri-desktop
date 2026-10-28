param([string]$SourceDirectory = (Join-Path $env:LOCALAPPDATA 'IRIS'))
$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
$destination = Join-Path $project 'out/IRIS-engine-data'
function Copy-VerifiedEngineFile([string]$Source, [string]$Target, [string]$Sha256) {
    if (Test-Path -LiteralPath $Target -PathType Leaf) {
        if ((Get-FileHash -LiteralPath $Target -Algorithm SHA256).Hash -ne $Sha256) { throw "Fichier existant différent, conservé : $Target" }
        return
    }
    if (-not (Test-Path -LiteralPath $Source -PathType Leaf)) { throw "Ressource IA absente : $Source. Les acquisitions déjà autorisées utilisent scripts/Install-LocalModel.ps1 et Install-SpeechModel.ps1 -AcceptDownload." }
    if ((Get-Item -LiteralPath $Source).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Lien de ressource refusé : $Source" }
    if ((Get-FileHash -LiteralPath $Source -Algorithm SHA256).Hash -ne $Sha256) { throw "Ressource source non conforme : $Source" }
    New-Item -ItemType Directory -Path (Split-Path -Parent $Target) -Force | Out-Null
    $part = $Target + '.' + [Guid]::NewGuid().ToString('N') + '.part'
    [IO.File]::Copy($Source, $part, $false)
    if ((Get-FileHash -LiteralPath $part -Algorithm SHA256).Hash -ne $Sha256) { throw "Copie non conforme, non activée : $part" }
    [IO.File]::Move($part, $Target)
}
$packages = @(
    @{ Archive='llama-b11901-bin-win-vulkan-x64.zip'; Hash='6e3a253f45117d1aec2adb2de0699ded0f6dccb683a798802d9e0dabae291f2c'; Runtime='llama-b11901'; Model='Qwen3-4B-Instruct-2507-Q4_K_M.gguf'; ModelHash='3605803b982cb64aead44f6c1b2ae36e3acdb41d8e46c8a94c6533bc4c67e597' },
    @{ Archive='whisper-b5454-x64.zip'; Hash='6ba69e3482d7826214f90a6a9c84ca07782aec1e1d0c6a7c30c994fd5d816ccb'; Runtime='whisper-b5454'; Model='ggml-small.bin'; ModelHash='1be3a9b2063867b937e64e2ec7483364a79917e157fa98c5d94b5c1fffea987b' },
    @{ Archive='piper-2023.11.14-2-windows-amd64.zip'; Hash='f3c58906402b24f3a96d92145f58acba6d86c9b5db896d207f78dc80811efcea'; Runtime='piper-2023.11.14-2'; Model='fr_FR-siwis-medium.onnx'; ModelHash='641d1ab097da2b81128c076810edb052b385decc8be3381814802a64a73baf99' }
)
Add-Type -AssemblyName System.IO.Compression.FileSystem
foreach ($package in $packages) {
    Write-Output ('Préparation des ressources livrées : ' + $package.Runtime)
    $archive = Join-Path $destination ('packages/' + $package.Archive)
    Copy-VerifiedEngineFile (Join-Path $SourceDirectory ('packages/' + $package.Archive)) $archive $package.Hash
    $zip = [IO.Compression.ZipFile]::OpenRead($archive)
    try {
        $runtime = [IO.Path]::GetFullPath((Join-Path $destination ('runtimes/' + $package.Runtime)))
        foreach ($entry in $zip.Entries) {
            if ($entry.FullName.EndsWith('/') -or $entry.FullName.EndsWith('\')) { continue }
            $target = [IO.Path]::GetFullPath((Join-Path $runtime $entry.FullName))
            if (-not $target.StartsWith($runtime.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase) -or $entry.FullName.Contains(':')) { throw 'Archive hors périmètre.' }
            $stream = $entry.Open(); $sha = [Security.Cryptography.SHA256]::Create()
            try { $expected = ([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-', '') } finally { $stream.Dispose(); $sha.Dispose() }
            Copy-VerifiedEngineFile (Join-Path $SourceDirectory ('runtimes/' + $package.Runtime + '/' + $entry.FullName)) $target $expected
        }
    } finally { $zip.Dispose() }
    Copy-VerifiedEngineFile (Join-Path $SourceDirectory ('models/' + $package.Model)) (Join-Path $destination ('models/' + $package.Model)) $package.ModelHash
}
Copy-VerifiedEngineFile (Join-Path $SourceDirectory 'models/fr_FR-siwis-medium.onnx.json') (Join-Path $destination 'models/fr_FR-siwis-medium.onnx.json') '39479916c2db192b5ac9764daddd0c744d83e023ad890c6976c0633ae4df8959'
foreach ($license in @('llama.cpp-MIT.txt', 'Qwen3-Apache-2.0.txt', 'LICENSE-LLVM-OpenMP', 'whisper.cpp-b5454-MIT.txt', 'piper-2023.11.14-2-MIT.txt', 'espeak-ng-GPL-3.txt', 'onnxruntime-MIT.txt', 'siwis-medium-MODEL_CARD.txt')) {
    $source = Join-Path $SourceDirectory ('licenses/' + $license)
    if (Test-Path -LiteralPath $source) { Copy-VerifiedEngineFile $source (Join-Path $destination ('licenses/' + $license)) (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash }
}
Write-Output "Modèles, archives et moteurs vérifiés prêts à côté de l’application : $destination"
