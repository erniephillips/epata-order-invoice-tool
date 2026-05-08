$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

dotnet restore
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=false

$publishPath = Join-Path $PSScriptRoot "bin\Release\net10.0\win-x64\publish"
Write-Host ""
Write-Host "Published to: $publishPath" -ForegroundColor Green
Write-Host "Double-click EPATA.InvoiceTool.exe from that folder." -ForegroundColor Green
