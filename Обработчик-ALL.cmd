@echo off
setlocal
title License inspector worker - all

rem C# worker: no PowerShell dependency (Server 2003 has PowerShell 1.0, where the
rem PS version of the worker cannot run at all).
set "ROOT=%~dp0"
if "%ROOT:~-1%"=="\" set "ROOT=%ROOT:~0,-1%"
if not exist "%ROOT%\build\Worker.exe" set "ROOT=\\tsclient\D\work\Learn\DSHarness\1CCommon"
set "WORKER=%ROOT%\build\Worker.exe"

echo Host      : %COMPUTERNAME%
echo Project   : %ROOT%
echo Worker    : %WORKER%
echo Queue     : %ROOT%\queue\all
echo.

if not exist "%WORKER%" (
  echo [ERROR] build\Worker.exe not found.
  echo         Run this file from \\tsclient\D\work\Learn\DSHarness\1CCommon
  pause
  exit /b 1
)

echo Keep this window open. Close it to stop the worker.
"%WORKER%" all "%ROOT%"
echo.
echo Worker stopped.
pause