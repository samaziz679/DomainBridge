@echo off
setlocal
echo DomainBridge Test Lab - read-only inventory
"%~dp0DomainBridge.Agent.exe" --inventory-once
echo.
pause
