using System;
using System.Runtime.InteropServices;

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
                Console.WriteLine(queryResult);
                // get the true information buffer length returned by the QuerySystem API
                systemInformationBufferLength = trueBufferLength;

                // free the previous allocated information buffer with wrong size
                Marshal.FreeHGlobal(systemInformationBufferPtr);

                // allocate new memory region with the true size
                systemInformationBufferPtr = Marshal.AllocHGlobal(systemInformationBufferLength);

                queryResult = PInvoke.NtQuerySystemInformation(Structs.SYSTEM_INFORMATION_CLASS.SystemHandleInformation, systemInformationBufferPtr, systemInformationBufferLength, out trueBufferLength);
            }
            Console.WriteLine(queryResult);

            var numberOfHandles = Marshal.ReadInt64(systemInformationBufferPtr);
            Console.WriteLine(numberOfHandles);
        }
    }
}