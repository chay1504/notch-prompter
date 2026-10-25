# Builds dist\NotchPrompter.exe (a single file that runs on any Windows 10/11 PC).
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

if (-not (Test-Path "$root\assets\app.ico")) { & "$root\tools\make-icon.ps1" }

dotnet build "$root\NotchPrompter.csproj" -c Release --nologo -v minimal
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

New-Item -ItemType Directory -Force "$root\dist" | Out-Null
Copy-Item "$root\bin\Release\net48\NotchPrompter.exe" "$root\dist\NotchPrompter.exe" -Force
$size = [math]::Round((Get-Item "$root\dist\NotchPrompter.exe").Length / 1KB)
"Built dist\NotchPrompter.exe ($size KB)"
