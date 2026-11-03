@echo off
rem Builds Aidyn Chronicles Retold and starts it at the title screen, as a player would, with the installed plugins, the base plugins
rem ACRE_BASE_PLUGINS names (the private repository's, ADR 0039) and the saved settings.
rem A .cmd has no icon of its own, so the first run makes "Launch ACRE.lnk" beside it, a shortcut here with the ACRE icon, to pin or
rem double-click instead. The icon is the one the private repository's branding/make.py puts in the game's project.
cd /d "%~dp0"
if not exist "%~dp0Launch ACRE.lnk" powershell -NoProfile -Command "$s=(New-Object -ComObject WScript.Shell).CreateShortcut('%~dp0Launch ACRE.lnk'); $s.TargetPath='%~dp0Launch ACRE.cmd'; $s.WorkingDirectory='%~dp0'; if (Test-Path '%~dp0src\AidynRetold.Game\icon.ico') { $s.IconLocation='%~dp0src\AidynRetold.Game\icon.ico,0' }; $s.Save()"
dotnet build src\AidynRetold.Game
if errorlevel 1 pause & exit /b 1
powershell -NoProfile -ExecutionPolicy Bypass -File tools\godot.ps1 --path src\AidynRetold.Game
if errorlevel 1 pause
