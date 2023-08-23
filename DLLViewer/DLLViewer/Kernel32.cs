using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DLLViewer
{
    internal class Kernel32
    {
        public const int PROCESS_QUERY_INFORMATION = 0x0400;
        public const int MEM_COMMIT = 0x00001000;
        public const int PAGE_READWRITE = 0x04;
        public const int PROCESS_WM_READ = 0x0010;

        [DllImport("kernel32.dll", EntryPoint = "LoadLibrary", SetLastError = true)]
        public static extern int LoadLibrary([MarshalAs(UnmanagedType.LPStr)] string lpLibFileName);

        [DllImport("kernel32.dll", EntryPoint = "GetProcAddress")]
        public static extern IntPtr GetProcAddress(int hModule, [MarshalAs(UnmanagedType.LPStr)] string lpProcName);

        [DllImport("kernel32.dll", EntryPoint = "FreeLibrary")]
        public static extern bool FreeLibrary(int hModule);

        //[DllImport("kernel32.dll", SetLastError = true)]
        //public static extern int VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);

        //[DllImport("kernel32.dll")]
        //public static extern bool ReadProcessMemory (int hProcess, int lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesRead);

        //[DllImport("kernel32.dll")]
        //public static extern IntPtr OpenProcess (int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        //[DllImport("kernel32.dll")]
        //public static extern void GetSystemInfo(out SYSTEM_INFO lpSystemInfo);

        //public struct MEMORY_BASIC_INFORMATION
        //{
        //    public int BaseAddress;
        //    public int AllocationBase;
        //    public int AllocationProtect;
        //    public int RegionSize;
        //    public int State;
        //    public int Protect;
        //    public int lType;
        //}

        //public struct SYSTEM_INFO
        //{
        //    public ushort processorArchitecture;
        //    ushort reserved;
        //    public uint pageSize;
        //    public IntPtr minimumApplicationAddress;
        //    public IntPtr maximumApplicationAddress;
        //    public IntPtr activeProcessorMask;
        //    public uint numberOfProcessors;
        //    public uint processorType;
        //    public uint allocationGranularity;
        //    public ushort processorLevel;
        //    public ushort processorRevision;
        //}
    }
}