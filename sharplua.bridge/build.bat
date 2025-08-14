@echo off
setlocal enableextensions

rem Resolve directories
set "SCRIPT_DIR=%~dp0"
set "OUT_DIR=%SCRIPT_DIR%bin"
if not exist "%OUT_DIR%" mkdir "%OUT_DIR%" >nul 2>&1

rem Show Zig version
zig version
if errorlevel 1 (
  echo Error: zig not found in PATH.
  exit /b 1
)

set "BIN_BASE=%OUT_DIR%\sharplua_bridge"

rem Build DLL from Zig source
zig build-lib -dynamic -femit-bin="%BIN_BASE%" "%SCRIPT_DIR%src\bridge.zig"
if errorlevel 1 goto :error

rem Ensure .dll extension if zig produced a file without extension
pushd "%OUT_DIR%" >nul
if not exist "sharplua_bridge.dll" (
  if exist "sharplua_bridge" ren "sharplua_bridge" "sharplua_bridge.dll"
)
popd >nul

if not exist "%BIN_BASE%.dll" (
  echo Error: build did not produce "%BIN_BASE%.dll".
  exit /b 1
)

rem Copy to repo root next to lua54.dll for convenience
copy /Y "%BIN_BASE%.dll" "%SCRIPT_DIR%..\sharplua_bridge.dll" >nul
if errorlevel 1 (
  echo Warning: failed to copy DLL to repo root.
) else (
  echo Built: "%BIN_BASE%.dll"
)

exit /b 0

:error
echo Build failed.
exit /b 1
