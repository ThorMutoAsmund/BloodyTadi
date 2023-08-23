using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DLLViewer
{
    internal class DbgHelp
    {
        [DllImport("dbghelp.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SymInitialize(IntPtr hProcess, string UserSearchPath, [MarshalAs(UnmanagedType.Bool)] bool fInvadeProcess);

        [DllImport("dbghelp.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SymCleanup(IntPtr hProcess);

        [DllImport("dbghelp.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern ulong SymLoadModuleEx(IntPtr hProcess, IntPtr hFile,
             string ImageName, string ModuleName, long BaseOfDll, int DllSize, IntPtr Data, int Flags);

        [DllImport("dbghelp.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SymEnumerateSymbols64(IntPtr hProcess,
           ulong BaseOfDll, SymEnumerateSymbolsProc64 EnumSymbolsCallback, IntPtr UserContext);

        [DllImport("dbghelp.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SymEnumerateSymbols(IntPtr hProcess,
           ulong BaseOfDll, SymEnumerateSymbolsProc EnumSymbolsCallback, IntPtr UserContext);

        //[PInvokeData("dbghelp.h", MSDNShortId = "NF:dbghelp.SymSetContext")]
        [DllImport("dbghelp.dll", SetLastError = true, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SymSetContext(IntPtr hProcess, in IMAGEHLP_STACK_FRAME StackFrame, [In, Optional] IntPtr Context);


        //[PInvokeData("dbghelp.h", MSDNShortId = "NF:dbghelp.SymEnumSymbols")]
        [DllImport("dbghelp.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SymEnumSymbols(IntPtr hProcess, [Optional] ulong BaseOfDll, [Optional, MarshalAs(UnmanagedType.LPTStr)] string Mask,
            PSYM_ENUMERATESYMBOLS_CALLBACK EnumSymbolsCallback, [In, Optional] IntPtr UserContext);


        public delegate bool SymEnumerateSymbolsProc64(string SymbolName, ulong SymbolAddress, uint SymbolSize, IntPtr UserContext);
        public delegate bool SymEnumerateSymbolsProc(string SymbolName, uint SymbolAddress, uint SymbolSize, IntPtr UserContext);
        

        //[PInvokeData("dbghelp.h", MSDNShortId = "NC:dbghelp.PSYM_ENUMERATESYMBOLS_CALLBACK")]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, CharSet = CharSet.Auto)]
        public delegate bool PSYM_ENUMERATESYMBOLS_CALLBACK([In] IntPtr pSymInfo, uint SymbolSize, [In, Optional] IntPtr UserContext);


        [StructLayout(LayoutKind.Sequential)]
        public struct IMAGEHLP_STACK_FRAME
        {
            /// <summary>
            /// <para>The program counter.</para>
            /// <para><c>x86:</c> The program counter is EIP.</para>
            /// <para>
            /// <c>Intel Itanium:</c> The program counter is a combination of the bundle address and a slot indicator of 0, 4, or 8 for the
            /// slot within the bundle.
            /// </para>
            /// <para><c>x64:</c> The program counter is RIP.</para>
            /// </summary>
            public ulong InstructionOffset;

            /// <summary>The return address.</summary>
            public ulong ReturnOffset;

            /// <summary>
            /// <para>The frame pointer.</para>
            /// <para><c>x86:</c> The frame pointer is EBP.</para>
            /// <para><c>Intel Itanium:</c> There is no frame pointer, but <c>AddrBStore</c> is used.</para>
            /// <para><c>x64:</c> The frame pointer is RBP. AMD-64 does not always use this value.</para>
            /// </summary>
            public ulong FrameOffset;

            /// <summary>
            /// <para>The stack pointer.</para>
            /// <para><c>x86:</c> The stack pointer is ESP.</para>
            /// <para><c>Intel Itanium:</c> The stack pointer is SP.</para>
            /// <para><c>x64:</c> The stack pointer is RSP.</para>
            /// </summary>
            public ulong StackOffset;

            /// <summary><c>Intel Itanium:</c> The backing store address.</summary>
            public ulong BackingStoreOffset;

            /// <summary><c>x86:</c> An FPO_DATA structure. If there is no function table entry, this member is <c>NULL</c>.</summary>
            public ulong FuncTableEntry;

            /// <summary>The possible arguments to the function.</summary>
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public ulong[] Params;

            /// <summary>This member is reserved for system use.</summary>
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
            public ulong[] Reserved;

            /// <summary>If this is a virtual frame, this member is <c>TRUE</c>. Otherwise, this member is <c>FALSE</c>.</summary>
            [MarshalAs(UnmanagedType.Bool)] public bool Virtual;

            /// <summary>This member is reserved for system use.</summary>
            public uint Reserved2;
        }
    }
}





/*
 * 
        DbgHelp.IMAGEHLP_STACK_FRAME stackFrame = new DbgHelp.IMAGEHLP_STACK_FRAME();

        stackFrame.InstructionOffset = address;

            if (!DbgHelp.SymSetContext(currentProcess, stackFrame, IntPtr.Zero))
            {
                var error = Marshal.GetLastWin32Error();


        Console.Out.WriteLine($"Error setting context: {error}");
                return false;
            }

    DbgHelp.SymEnumSymbols(currentProcess, 0, null, EnumParams, IntPtr.Zero);


            //DbgHelp.IMAGEHLP_STACK_FRAME stackFrame = new DbgHelp.IMAGEHLP_STACK_FRAME();

            //DbgHelp.SymSetContext(currentProcess, stackFrame);

            //DbgHelp.SymEnumSymbols(currentProcess, baseOfDll, null, EnumParams);
        public static bool EnumSyms2(string name, ulong address, uint size, IntPtr context)
        {
            Console.Out.WriteLine(name);

            return true;
        }

        public static bool EnumParams([In] IntPtr pSymInfo, uint SymbolSize, [In, Optional] IntPtr UserContext)
        {
            Console.Out.WriteLine(SymbolSize);
            return true;
        }
*/