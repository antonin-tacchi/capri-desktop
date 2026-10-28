param([switch]$AcceptDownload)
$ErrorActionPreference = 'Stop'
if (-not $AcceptDownload) { throw 'Téléchargement de 2,54 Go : autorisation explicite nécessaire. Relancez avec -AcceptDownload après avoir lu docs/MODELS.md.' }
$dataRoot = Join-Path $env:LOCALAPPDATA 'IRIS'
$packageRoot = Join-Path $dataRoot 'packages'
$modelRoot = Join-Path $dataRoot 'models'
$runtimeRoot = Join-Path $dataRoot 'runtimes/llama-b11901'
$licenseRoot = Join-Path $dataRoot 'licenses'
New-Item -ItemType Directory -Path $packageRoot,$modelRoot,$licenseRoot -Force | Out-Null
function Get-VerifiedFile([string]$Url, [string]$Target, [string]$Sha256) {
    if (Test-Path -LiteralPath $Target) {
        if ((Get-FileHash -LiteralPath $Target -Algorithm SHA256).Hash -ne $Sha256) { throw "Fichier existant différent, conservé : $Target" }
        Write-Output "PASS fichier existant vérifié : $Target"
        return
    }
    $part = "$Target.$([Guid]::NewGuid().ToString('N')).part"
    Write-Output "Téléchargement explicite : $Url"
    Invoke-WebRequest -Uri $Url -OutFile $part
    if ((Get-FileHash -LiteralPath $part -Algorithm SHA256).Hash -ne $Sha256) { throw "Empreinte incorrecte ; fichier non activé : $part" }
    [IO.File]::Move($part, $Target)
    Write-Output "PASS SHA-256 : $Target"
}
$archive = Join-Path $packageRoot 'llama-b11901-bin-win-vulkan-x64.zip'
Get-VerifiedFile 'https://github.com/ggml-org/llama.cpp/releases/download/b11901/llama-b11901-bin-win-vulkan-x64.zip' $archive '6e3a253f45117d1aec2adb2de0699ded0f6dccb683a798802d9e0dabae291f2c'
if (-not (Test-Path -LiteralPath $runtimeRoot)) {
    $zip = [IO.Compression.ZipFile]::OpenRead($archive)
    try {
        foreach ($entry in $zip.Entries) {
            if ($entry.FullName -match '[/\\:]' -or $entry.FullName -in '.', '..') { throw "Entrée d’archive non autorisée" }
        }
    } finally { $zip.Dispose() }
    Expand-Archive -LiteralPath $archive -DestinationPath $runtimeRoot
}
Get-VerifiedFile 'https://huggingface.co/unsloth/Qwen3-4B-Instruct-2507-GGUF/resolve/a06e946bb6b655725eafa393f4a9745d460374c9/Qwen3-4B-Instruct-2507-Q4_K_M.gguf' (Join-Path $modelRoot 'Qwen3-4B-Instruct-2507-Q4_K_M.gguf') '3605803b982cb64aead44f6c1b2ae36e3acdb41d8e46c8a94c6533bc4c67e597'
$notices = @{
    'llama.cpp-MIT.txt' = 'https://raw.githubusercontent.com/ggml-org/llama.cpp/b11901/LICENSE'
    'Qwen3-Apache-2.0.txt' = 'https://huggingface.co/Qwen/Qwen3-4B-Instruct-2507/raw/main/LICENSE'
}
foreach ($name in $notices.Keys) {
    $target = Join-Path $licenseRoot $name
    if (-not (Test-Path -LiteralPath $target)) { Invoke-WebRequest -Uri $notices[$name] -OutFile $target }
}
$llvmSource = Join-Path $runtimeRoot 'LICENSE-LLVM-OpenMP'
$llvmTarget = Join-Path $licenseRoot 'LICENSE-LLVM-OpenMP'
if (Test-Path -LiteralPath $llvmTarget) {
    if ((Get-FileHash -LiteralPath $llvmTarget).Hash -ne (Get-FileHash -LiteralPath $llvmSource).Hash) { throw "Licence existante différente, conservée : $llvmTarget" }
} else { Copy-Item -LiteralPath $llvmSource -Destination $llvmTarget }
Write-Output 'Installation terminée. IRIS vérifiera à nouveau les fichiers avant de lancer le moteur. Aucun service permanent ni pare-feu modifié.'
