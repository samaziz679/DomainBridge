@echo off
setlocal
echo DomainBridge Test Lab - read-only inventory
"%~dp0Agent\DomainBridge.Agent.exe" --inventory-once
echo.
pause
