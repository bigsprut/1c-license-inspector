@echo off
setlocal
title Worker queue - all

rem Finds tools\server-worker.ps1 next to this file; if missing, uses the project
rem from the RDP redirected folder. So a lone copy of this file also works.
set "ROOT=%~dp0"
if "%ROOT:~-1%"=="\" set "ROOT=%ROOT:~0,-1%"
set "WORKER=%ROOT%\tools\server-worker.ps1"
if not exist "%WORKER%" set "ROOT=\\tsclient\D\work\Learn\DSHarness\1CCommon"
if not exist "%WORKER%" set "WORKER=%ROOT%\tools\server-worker.ps1"
if not exist "%WORKER%" (
  echo [ERROR] tools\server-worker.ps1 not found next to this file
  echo         Run it from \\tsclient\D\work\Learn\DSHarness\1CCommon
  pause
  exit /b 1
)

echo Project root: %ROOT%
echo Queue: %ROOT%\queue\all
echo Keep this window open. Close it to stop the worker.
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%WORKER%" -Name all -Root "%ROOT%"
echo.
echo Worker stopped.
pause