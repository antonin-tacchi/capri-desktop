@echo off
setlocal
set "IRIS_FOLDER=%~dp0out\IRIS-portable-win-x64-0.7.1"
if not exist "%IRIS_FOLDER%\IRIS.exe" (
    echo Version autonome absente. Executez scripts\Build.ps1 pour la construire.
    pause
    exit /b 1
)
if not exist "%~dp0out\IRIS-engine-data\models\Qwen3-4B-Instruct-2507-Q4_K_M.gguf" goto missing_models
if not exist "%~dp0out\IRIS-engine-data\models\ggml-small.bin" goto missing_models
start "" /D "%IRIS_FOLDER%" "%IRIS_FOLDER%\IRIS.exe"
exit /b 0
:missing_models
echo Ressources IA absentes. Conservez out\IRIS-engine-data a cote du dossier de l'application.
echo Executez scripts\Build.ps1 pour reconstruire la distribution complete.
pause
exit /b 1
endlocal
