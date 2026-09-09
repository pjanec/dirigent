@echo off
rem Convenience wrapper so the release can be started by double-clicking.
rem -NoProfile keeps a developer's PowerShell profile out of the build, and the exit code is
rem passed on so a failure is visible to whoever called this.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0make_release.ps1" %*
exit /b %ERRORLEVEL%
