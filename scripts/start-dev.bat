@echo off
title PixelAndBit Dev Server
cd /d "%~dp0.."
echo.
echo   PixelAndBit - http://localhost:5000
echo   Keep this window open. Stop with Ctrl+C.
echo.
dotnet watch run --project "%~dp0..\PixelAndBit.Web\PixelAndBit.Web.csproj"
if errorlevel 1 pause
