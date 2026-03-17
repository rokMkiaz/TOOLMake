@echo off
cd /d "%~dp0"

set NODE=
where node >nul 2>&1 && set NODE=node
if not defined NODE set NODE="C:\Program Files\nodejs\node.exe"

%NODE% run_cheonsu.js
if %errorlevel% neq 0 (
    pause
    exit /b 1
)

start "" "%~dp0outputlog\result.html"
pause
