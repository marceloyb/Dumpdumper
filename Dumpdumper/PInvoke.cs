using System;
using System.Runtime.InteropServices;
using System.Text;

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

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(
                   Structs.PROCESS_ACCESS processAccess,
                   bool bInheritHandle,
                   int processId);

        [DllImport("ntdll.dll")]
        //[return: MarshalAs(UnmanagedType.Bool)]
        public static extern Structs.NTSTATUS NtDuplicateObject(
            IntPtr hSourceProcessHandle,
            IntPtr hSourceHandle, 
            IntPtr hTargetProcessHandle, 
            out IntPtr lpTargetHandle,
            Structs.PROCESS_ACCESS dwDesiredAccess, 
            [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, 
            Structs.DuplicateOptions dwOptions);


        [DllImport("ntdll.dll")]
        public static extern Structs.NTSTATUS NtQueryObject(
            IntPtr ObjectHandle, 
            Structs.OBJECT_INFORMATION_CLASS ObjectInformationClass,
            IntPtr ObjectInformation, 
            int ObjectInformationLength, 
            out int ReturnLength);

        [DllImport("kernel32.dll")]
        public static extern bool QueryFullProcessImageName(
            IntPtr hprocess, 
            int dwFlags,
            StringBuilder lpExeName, 
            out int size);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseHandle(IntPtr hObject);

    }

        
}
