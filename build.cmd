@echo off
rem ---------------------------------------------------------------
rem  Build wrapper. KEEP THIS FILE PURE ASCII.
rem  cmd.exe parses batch files using the system ANSI code page
rem  (936 / GBK here), so UTF-8 Chinese text in a .cmd file would be
rem  garbled. All user-facing Chinese messages live in build.ps1,
rem  which is saved as UTF-8 with BOM so PowerShell reads it correctly.
rem ---------------------------------------------------------------
setlocal

where powershell >nul 2>nul
if errorlevel 1 (
  echo [ERROR] powershell.exe not found; cannot build.
  exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" %*
set "RC=%ERRORLEVEL%"

if not "%RC%"=="0" (
  echo.
  echo [ERROR] Build failed with exit code %RC%.
  exit /b %RC%
)

exit /b 0
