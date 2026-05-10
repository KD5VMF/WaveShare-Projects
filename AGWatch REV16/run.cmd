@echo off
setlocal
cd /d "%~dp0"
set EXE=AGWatch\bin\Release\net8.0-windows\AGWatch_REV16_AutoDnsSave.exe

echo Building AGWatch REV16 Auto DNS Save...
dotnet clean AGWatch\AGWatch.csproj -c Release
dotnet build AGWatch\AGWatch.csproj -c Release
if errorlevel 1 (
  echo.
  echo Build failed.
  pause
  exit /b 1
)

if not exist "%EXE%" (
  echo.
  echo ERROR: Expected EXE not found:
  echo %EXE%
  echo.
  echo Folder contents:
  dir AGWatch\bin\Release\net8.0-windows
  pause
  exit /b 1
)

echo.
echo Starting AGWatch REV16 Auto DNS Save...
start "" "%EXE%"
pause
