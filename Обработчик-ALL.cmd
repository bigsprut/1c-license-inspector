@echo off
setlocal
title License inspector worker - all

set "ROOT=%~dp0"
if "%ROOT:~-1%"=="\" set "ROOT=%ROOT:~0,-1%"
if not exist "%ROOT%\build\Worker.exe" set "ROOT=\\tsclient\D\work\Learn\DSHarness\1CCommon"
set "SRC=%ROOT%\build\Worker.exe"

echo Host      : %COMPUTERNAME%
echo Project   : %ROOT%
echo Worker    : %SRC%
echo Queue     : %ROOT%\queue\all
echo.

if not exist "%SRC%" (
  echo [ERROR] build\Worker.exe not found.
  echo         Run this file from \\tsclient\D\work\Learn\DSHarness\1CCommon
  pause
  exit /b 1
)

rem Run a LOCAL copy: an exe started from a network path locks the file on the host,
rem which then cannot be rebuilt. Copying also avoids UNC load problems on Server 2003.
set "LOCALDIR=%TEMP%\1c-inspector-worker"
if not exist "%LOCALDIR%" mkdir "%LOCALDIR%"
copy /Y "%SRC%" "%LOCALDIR%\Worker.exe" >nul
if errorlevel 1 (
  echo [ERROR] cannot copy Worker.exe to %LOCALDIR%
  pause
  exit /b 1
)

echo Keep this window open. Close it to stop the worker.
"%LOCALDIR%\Worker.exe" all "%ROOT%"
echo.
echo Worker stopped.
pause