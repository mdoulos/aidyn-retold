@echo off
rem Builds Aidyn Chronicles Enhanced and starts it at the title screen, as a player would, with the installed plugins and the saved settings.
rem A .cmd has no icon of its own, so the first run makes "Launch ACE.lnk" beside it, a shortcut here with the ACE icon, to pin or double-click instead.
cd /d "%~dp0"
if not exist "%~dp0Launch ACE.lnk" powershell -NoProfile -Command "$s=(New-Object -ComObject WScript.Shell).CreateShortcut('%~dp0Launch ACE.lnk'); $s.TargetPath='%~dp0Launch ACE.cmd'; $s.WorkingDirectory='%~dp0'; $s.IconLocation='%~dp0assets\branding\ace-icon.ico,0'; $s.Save()"
dotnet build src\AidynEnhanced.Game
if errorlevel 1 pause & exit /b 1
powershell -NoProfile -ExecutionPolicy Bypass -File tools\godot.ps1 --path src\AidynEnhanced.Game
if errorlevel 1 pause
