@echo off
rem Builds Aidyn Chronicles Enhanced and starts it at the title screen, as a player would, with the installed plugins and the saved settings.
cd /d "%~dp0"
dotnet build src\AidynEnhanced.Game
if errorlevel 1 pause & exit /b 1
powershell -NoProfile -ExecutionPolicy Bypass -File tools\godot.ps1 --path src\AidynEnhanced.Game
if errorlevel 1 pause
