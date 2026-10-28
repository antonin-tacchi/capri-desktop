param()
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$portable = Join-Path $root 'out/IRIS-portable-win-x64-0.7.1'
$portableExecutable = Join-Path $portable 'IRIS.exe'
if (Test-Path -LiteralPath $portableExecutable -PathType Leaf) {
    foreach ($model in @('models/Qwen3-4B-Instruct-2507-Q4_K_M.gguf', 'models/ggml-small.bin', 'packages/llama-b11901-bin-win-vulkan-x64.zip', 'packages/whisper-b5454-x64.zip', 'runtimes/llama-b11901/llama-server.exe', 'runtimes/whisper-b5454/Release/whisper-cli.exe', 'models/fr_FR-siwis-medium.onnx', 'models/fr_FR-siwis-medium.onnx.json', 'packages/piper-2023.11.14-2-windows-amd64.zip', 'runtimes/piper-2023.11.14-2/piper/piper.exe')) {
        if (-not (Test-Path -LiteralPath (Join-Path $root ('out/IRIS-engine-data/' + $model)) -PathType Leaf)) { throw "Distribution IA incomplète : $model absent de out/IRIS-engine-data. Reconstruisez avec scripts/Build.ps1." }
    }
    foreach ($required in @('coreclr.dll', 'hostfxr.dll', 'hostpolicy.dll', 'System.Private.CoreLib.dll', 'System.Windows.Forms.dll', 'IRIS.dll', 'IRIS.runtimeconfig.json', 'assets/animations.json', 'assets/bulle_discution.png', 'assets/fonts/ShareTechMono-Regular.ttf', 'assets/fonts/OFL-ShareTechMono.txt')) {
        if (-not (Test-Path -LiteralPath (Join-Path $portable $required) -PathType Leaf)) {
            throw "Version autonome incomplète ($required absent). Reconstruisez avec scripts/Build.ps1."
        }
    }
    # Self-contained: no SDK/runtime discovery, PATH or global .NET installation.
    Start-Process -FilePath $portableExecutable -WorkingDirectory $portable | Out-Null
    return
}
throw 'Version IRIS 0.7.1 absente. Exécutez scripts/Build.ps1. Aucune ancienne version ne sera lancée silencieusement.'
