# Installs NotchPrompter for the current user (no admin needed) and starts it.
# From then on it launches automatically every time you sign in to Windows.
$ErrorActionPreference = 'Stop'
$src = Join-Path $PSScriptRoot 'dist\NotchPrompter.exe'
if (-not (Test-Path -LiteralPath $src)) { $src = Join-Path $PSScriptRoot 'NotchPrompter.exe' }
if (-not (Test-Path -LiteralPath $src)) { throw "NotchPrompter.exe not found - download the Windows ZIP or run .\build.ps1 first" }
$dir = Join-Path $env:LOCALAPPDATA 'Programs\NotchPrompter'
$dst = Join-Path $dir 'NotchPrompter.exe'

# Ask a running copy to quit (it saves where you were in the script), then make sure it is gone.
Start-Process $src -ArgumentList '--quit'
$deadline = (Get-Date).AddSeconds(5)
while ((Get-Process NotchPrompter -ErrorAction SilentlyContinue) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 200 }
Get-Process NotchPrompter -ErrorAction SilentlyContinue | Stop-Process -Force

New-Item -ItemType Directory -Force $dir | Out-Null
Copy-Item $src $dst -Force

# Start menu entry, so it can be reopened after "Quit".
$lnk = Join-Path ([Environment]::GetFolderPath('Programs')) 'NotchPrompter.lnk'
$shortcut = (New-Object -ComObject WScript.Shell).CreateShortcut($lnk)
$shortcut.TargetPath = $dst
$shortcut.WorkingDirectory = $dir
$shortcut.Description = 'Dynamic Island teleprompter'
$shortcut.Save()

# Start with Windows. The app keeps this entry up to date itself while "Start with Windows" is ticked.
Set-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name NotchPrompter -Value "`"$dst`" --autostart"

Start-Process $dst
"Installed to $dst and started. It will launch automatically every time you sign in."
