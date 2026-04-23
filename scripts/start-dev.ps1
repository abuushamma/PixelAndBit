# PixelAndBit dev server (watch). Keep this window open. Ctrl+C to stop.
$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..")
Write-Host ""
Write-Host "  PixelAndBit - http://localhost:5001" -ForegroundColor Cyan
Write-Host "  Keep this window open. Stop with Ctrl+C." -ForegroundColor DarkGray
Write-Host ""
dotnet watch run --project 'PixelAndBit.Web\PixelAndBit.Web.csproj'
