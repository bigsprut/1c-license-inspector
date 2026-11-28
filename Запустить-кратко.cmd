@echo off
rem Lite license inspector: run from build\, fall back to dist\.
cd /d "%~dp0"
if exist "build\1C-License-Inspector-Lite.exe" (
  start "" "build\1C-License-Inspector-Lite.exe"
) else (
  start "" "dist\1C-License-Inspector-Lite.exe"
)