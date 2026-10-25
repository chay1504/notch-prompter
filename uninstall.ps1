# Removes NotchPrompter. Your script and settings in %APPDATA%\NotchPrompter are kept
# unless you pass -RemoveSettings.
param([switch]$RemoveSettings)
$dir = Join-Path $env:LOCALAPPDATA 'Programs\NotchPrompter'
$exe = Join-Path $dir 'NotchPrompter.exe'

if (Test-Path $exe) { Start-Process $exe -ArgumentList '--quit' }
$deadline = (Get-Date).AddSeconds(5)
while ((Get-Process NotchPrompter -ErrorAction SilentlyContinue) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 200 }
Get-Process NotchPrompter -ErrorAction SilentlyContinue | Stop-Process -Force

Remove-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name NotchPrompter -ErrorAction SilentlyContinue
Remove-Item (Join-Path ([Environment]::GetFolderPath('Programs')) 'NotchPrompter.lnk') -ErrorAction SilentlyContinue
Remove-Item $dir -Recurse -Force -ErrorAction SilentlyContinue
if ($RemoveSettings) { Remove-Item (Join-Path $env:APPDATA 'NotchPrompter') -Recurse -Force -ErrorAction SilentlyContinue }
"NotchPrompter removed."
