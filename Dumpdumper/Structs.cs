using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dumpdumper
{
    public class Structs
    {
        [Flags]
        public enum NTSTATUS : uint
        {
            // Success
            Success = 0x00000000,

            //Errors
            InfoLengthMismatch = 0xc0000004
        }

        [Flags]
        public enum SYSTEM_INFORMATION_CLASS : uint
        {
            SystemHandleInformation = 0x10
        }
    }
}
