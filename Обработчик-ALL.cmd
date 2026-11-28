@echo off
setlocal
title Worker queue - all

rem Resolves project root: next to this file, or the RDP redirected folder.
set "ROOT=%~dp0"
if "%ROOT:~-1%"=="\" set "ROOT=%ROOT:~0,-1%"
set "WORKER=%ROOT%\tools\server-worker.ps1"
if not exist "%WORKER%" set "ROOT=\\tsclient\D\work\Learn\DSHarness\1CCommon"
if not exist "%WORKER%" set "WORKER=%ROOT%\tools\server-worker.ps1"

echo Host      : %COMPUTERNAME%
echo Project   : %ROOT%
echo Worker    : %WORKER%
for /f "delims=" %%v in ('powershell -NoProfile -Command "$PSVersionTable.PSVersion.ToString()" 2^>nul') do echo PowerShell: %%v

if not exist "%WORKER%" (
  echo.
  echo [ERROR] server-worker.ps1 not found.
  echo         Run this file from \\tsclient\D\work\Learn\DSHarness\1CCommon
  pause
  exit /b 1
)

echo Queue     : %ROOT%\queue\all
echo Keep this window open. Close it to stop the worker.
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%WORKER%" -Name all -Root "%ROOT%"
echo.
echo Worker stopped.
pause