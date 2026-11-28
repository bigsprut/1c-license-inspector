@echo off
rem Removes traces of "1C License Inspector" from this server.
rem Use it when you are done with the tool and the worker window is closed.
rem ASCII only: this file runs on Windows Server 2003 in OEM code page.
setlocal
title Remove traces of 1C License Inspector

echo.
echo  Removes: worker copies, application logs and dumps in %%TEMP%%
echo           and the 1C-License-Inspector folder in ProgramData.
echo  Keeps  : 1C platform, RAS service, HASP services (not ours).
echo.
echo  Close the worker window first, if it is still open.
echo.
pause

set "WORK=%TEMP%\1c-inspector-worker"

taskkill /F /FI "WINDOWTITLE eq License inspector worker*" >nul 2>&1
taskkill /F /IM 1cli.exe >nul 2>&1
taskkill /F /IM 1cli-lite.exe >nul 2>&1

for %%f in (
  "%TEMP%\1c-license-inspector.log"
  "%TEMP%\1c-license-inspector-crash.log"
  "%TEMP%\1c-license-inspector-boot.log"
  "%TEMP%\1c-license-inspector-selftest.txt"
  "%TEMP%\mmc20dump.log"
  "%TEMP%\rac-sessions-err.txt"
) do del /q %%f >nul 2>&1

del /q "%TEMP%\mmc20-*.txt" >nul 2>&1
del /q "%TEMP%\1c-worker-selfdel.cmd" >nul 2>&1
rd /s /q "%WORK%" >nul 2>&1
rd /s /q "%ProgramData%\1C-License-Inspector" >nul 2>&1

echo.
if exist "%WORK%" (
  echo  [NOTE] %WORK% is still there - close the worker window and run this file again.
) else (
  echo  Done: temporary files and ProgramData folder are removed.
)
echo  The 1C platform, RAS and HASP services were not touched.
echo.
pause
