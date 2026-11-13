@echo off
setlocal
set ASPNETCORE_ENVIRONMENT=Development
start "DomainBridge local API" /min "%~dp0Console\DomainBridge.Api.exe" --urls http://127.0.0.1:5080
timeout /t 2 /nobreak >nul
start "DomainBridge" http://127.0.0.1:5080
