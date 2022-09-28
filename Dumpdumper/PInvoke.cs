using System;
using System.Runtime.InteropServices;

namespace Dumpdumper
{
    public class PInvoke
    {
        [DllImport("ntdll.dll")]
        public static extern Structs.NTSTATUS NtQuerySystemInformation(
            Structs.SYSTEM_INFORMATION_CLASS SystemInformationClass,
            IntPtr SystemInformation,
            int SystemInformationLength,
            out int ReturnLength);
    }
}
