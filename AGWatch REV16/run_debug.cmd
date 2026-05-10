@echo off
setlocal
cd /d "%~dp0"
set EXE=AGWatch\bin\Release\net8.0-windows\AGWatch_REV16_AutoDnsSave.exe

echo ==== BUILD START ==== > build_log_REV16.txt
dotnet --info >> build_log_REV16.txt 2>&1
dotnet clean AGWatch\AGWatch.csproj -c Release >> build_log_REV16.txt 2>&1
dotnet build AGWatch\AGWatch.csproj -c Release >> build_log_REV16.txt 2>&1

if errorlevel 1 (
  echo.
  echo Build failed. Send build_log_REV16.txt
  pause
  exit /b 1
)

echo ==== RUN START ==== > run_log_REV16.txt
if not exist "%EXE%" (
  echo ERROR: Expected EXE not found: %EXE% >> run_log_REV16.txt
  echo Folder contents: >> run_log_REV16.txt
  dir AGWatch\bin\Release\net8.0-windows >> run_log_REV16.txt 2>&1
  type run_log_REV16.txt
  pause
  exit /b 1
)

"%EXE%" >> run_log_REV16.txt 2>&1

echo.
echo Send these if it fails:
echo build_log_REV16.txt
echo run_log_REV16.txt
echo Documents\AGWatch\fatal_error_REV16.txt
pause
