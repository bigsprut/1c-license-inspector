@echo off
title Worker queue - ALL (2016)
echo Processing tasks from queue\all. Close this window to stop.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\server-worker.ps1" -Name all -Root "%~dp0"
pause