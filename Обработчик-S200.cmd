@echo off
title Worker queue - S200 (2003)
echo Processing tasks from queue\s200. Close this window to stop.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\server-worker.ps1" -Name s200 -Root "%~dp0"
pause