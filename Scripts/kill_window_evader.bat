@echo off
taskkill /F /IM WindowEvader.exe >nul 2>&1

if %ERRORLEVEL%==0 (
    echo WindowEvader was terminated successfully.
) else (
    echo Failed to terminate WindowEvader. It may not be running.
)

pause
