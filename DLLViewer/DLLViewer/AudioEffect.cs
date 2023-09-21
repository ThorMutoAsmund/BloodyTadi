using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;


namespace DLLViewer
{
    // See
    // https://teragonaudio.com/article/How-to-make-your-own-VST-host.html

    public class AudioEffect
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate void SetParameterDelegate(IntPtr effect, UInt32 index, float parameter);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate float GetParameterDelegate(IntPtr effect, UInt32 index);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate UInt32 AudioMasterCallback(IntPtr effect, int opcode, int index, int value, IntPtr ptr, float opt);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate IntPtr MainDelegate(IntPtr audioMaster);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate UInt32 DispatcherDelegate(IntPtr effect, UInt32 opCode, UInt32 index, UInt32 value, StringBuilder ptr, float opt);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate UInt32 IntPtrDispatcherDelegate(IntPtr effect, UInt32 opCode, UInt32 index, UInt32 value, IntPtr ptr, float opt);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate void ProcessDelegate(IntPtr effect, IntPtr inputs, IntPtr outputs, UInt32 sampleframes);
        //delegate void ProcessDelegate(IntPtr effect, IntPtr inputs, IntPtr outputs, UInt32 sampleframes);

        private string filePath;
        private int hModule;
        private IntPtr currentProcess;
        private ulong baseOfDll;
        private IntPtr aeffectPtr;
        private AEffect aeffect;
        private static List<string> availableSymbols = new List<string>();
        //private static string symbolEnumerationResult;


        private SetParameterDelegate setParameter;
        private GetParameterDelegate getParameter;
        private DispatcherDelegate dispatcher;
        private IntPtrDispatcherDelegate intPtrDispatcher;
        private ProcessDelegate process;
        private ProcessDelegate processReplacing;

        public UInt32 NumParams => this.aeffect.numParams;
        public string UniqueID { get; private set; }

        public AudioEffect(string filePath)
        {
            this.filePath = filePath;
        }

        public static AudioEffect Create(string filePath)
        {
            var effect = new AudioEffect(filePath);

            effect.currentProcess = Process.GetCurrentProcess().Handle;

            // Initialize sym
            bool status = DbgHelp.SymInitialize(effect.currentProcess, null, false);

            if (status == false)
            {
                Console.WriteLine("Failed to initialize symbol handler.");
                return null;
            }

            // Load dll
            effect.baseOfDll = DbgHelp.SymLoadModuleEx(effect.currentProcess, IntPtr.Zero, filePath, null, 0, 0, IntPtr.Zero, 0);

            if (effect.baseOfDll == 0)
            {
                string errorMessage = new Win32Exception(Marshal.GetLastWin32Error()).Message;
                Console.WriteLine($"Error loading symbol table. {errorMessage}");
                DbgHelp.SymCleanup(effect.currentProcess);
                return null;
            }

            // Enumerate symbols
            // effect.EnumerateSymbols();

            // Cleanup.
            DbgHelp.SymCleanup(effect.currentProcess);

            // Load module
            effect.hModule = Kernel32.LoadLibrary(filePath);
            if (effect.hModule == 0)
            {
                var lastError = Marshal.GetLastWin32Error();
                string errorMessage = new Win32Exception(lastError).Message;
                if (errorMessage.Contains("%1"))
                {
                    errorMessage = String.Format(errorMessage.Replace("%1","{0}"), effect.filePath);
                }
                Console.WriteLine($"Error loading library. {errorMessage} {lastError}");
                if (lastError == 193)
                {
                    Console.WriteLine($"Library might be a 64 bit which is not supported");
                }
                return null;
            }

            // Get ptr to VST main
            IntPtr mainPtr = Kernel32.GetProcAddress(effect.hModule, "main");
            if (mainPtr == IntPtr.Zero)
            {
                Console.WriteLine("main proc address is null. Cannot load module.");

                //if (symbolEnumerationResult != null)
                //{
                //    Console.WriteLine($"Failed to enumerate symbols. {symbolEnumerationResult}");
                //}
                //else
                //{
                //    Console.WriteLine("Available symbols are:");
                //    foreach (var symbol in availableSymbols)
                //    {
                //        Console.WriteLine(symbol);
                //    }
                //}

                return null;
            }
            MainDelegate main = Marshal.GetDelegateForFunctionPointer<MainDelegate>(mainPtr);

            // Declare pointer to callback
            var callback = Marshal.GetFunctionPointerForDelegate<AudioMasterCallback>(AMCallback);

            // Get effect ptr
            effect.aeffectPtr = main(callback);
            effect.aeffect = Marshal.PtrToStructure<AEffect>(effect.aeffectPtr);

            // Magic
            var magic = UInt32ToString(effect.aeffect.magic);

            if (magic != "VstP")
            {
                Console.WriteLine("Not a VST plugin.");
                return null;
            }

            // UniqueId
            effect.UniqueID = UInt32ToString(effect.aeffect.uniqueID);

            // Important callbacks
            if (effect.aeffect.setParameter == IntPtr.Zero)
            {
                Console.WriteLine("setParameter is null.");
                return null;
            }

            if (effect.aeffect.getParameter == IntPtr.Zero)
            {
                Console.WriteLine("getParameter is null.");
                return null;
            }

            if (effect.aeffect.dispatcher == IntPtr.Zero)
            {
                Console.WriteLine("dispatcher is null.");
                return null;
            }

            effect.setParameter = Marshal.GetDelegateForFunctionPointer<SetParameterDelegate>(effect.aeffect.setParameter);
            effect.getParameter = Marshal.GetDelegateForFunctionPointer<GetParameterDelegate>(effect.aeffect.getParameter);
            effect.dispatcher = Marshal.GetDelegateForFunctionPointer<DispatcherDelegate>(effect.aeffect.dispatcher);
            effect.intPtrDispatcher = Marshal.GetDelegateForFunctionPointer<IntPtrDispatcherDelegate>(effect.aeffect.dispatcher);
            effect.process = Marshal.GetDelegateForFunctionPointer<ProcessDelegate>(effect.aeffect.process);
            effect.processReplacing = Marshal.GetDelegateForFunctionPointer<ProcessDelegate>(effect.aeffect.processReplacing);

            return effect;
        }

        ~AudioEffect()
        {
            Close();
        }

        public void Describe()
        {
            Console.WriteLine($"numPrograms = {this.aeffect.numPrograms}");
            Console.WriteLine($"numParams = {this.aeffect.numParams}");
            Console.WriteLine($"numInputs = {this.aeffect.numInputs}");
            Console.WriteLine($"numOutputs = {this.aeffect.numOutputs}");
            Console.WriteLine($"flags = {this.aeffect.flags}");
            Console.WriteLine($"initialDelay = {this.aeffect.initialDelay}");
            Console.WriteLine($"realQualities = {this.aeffect.realQualities}");
            Console.WriteLine($"offQualities = {this.aeffect.offQualities}");
            Console.WriteLine($"uniqueID = {this.aeffect.uniqueID}");
            Console.WriteLine($"version = {this.aeffect.version}");
        }

        public string GetParamName(UInt32 index)
        {
            StringBuilder label = new StringBuilder(new String(' ', 256));
            dispatcher(this.aeffectPtr, (UInt32)OpCode.effGetParamName, index, 0, label, 0);
            return label.ToString();
        }

        //public void Test()
        //{
        //    var value = 0.42F;
        //    setParameter(this.aeffectPtr, 2, value);
        //    var test = getParameter(this.aeffectPtr, 2);
        //    Console.WriteLine($"{value.ToString(CultureInfo.InvariantCulture)} = {test.ToString(CultureInfo.InvariantCulture)}");
        //}

        //public void EnumerateSymbols()
        //{
        //    availableSymbols.Clear();

        //    // Enumerate symbols. For every symbol the callback method EnumSyms is called
        //    if (DbgHelp.SymEnumerateSymbols64(this.currentProcess, this.baseOfDll, EnumSyms, IntPtr.Zero) == false)
        //    {
        //        symbolEnumerationResult = new Win32Exception(Marshal.GetLastWin32Error()).Message ?? "Unknown error";
        //    }
        //    else
        //    {
        //        symbolEnumerationResult = null;
        //    }
        //}

        public void Open()
        {
            // delegate UInt32 DispatcherDelegate(IntPtr effect, UInt32 opCode, UInt32 index, UInt32 value, StringBuilder ptr, float opt);
            IntPtr handle = Process.GetCurrentProcess().MainWindowHandle;
            intPtrDispatcher(this.aeffectPtr, (UInt32)OpCode.effOpen, 0, 0, handle, 0);
        }

        public void Close()
        {
            //this crashes
            //if (this.aeffectPtr != null)
            //{
            //    IntPtr handle = Process.GetCurrentProcess().MainWindowHandle;
            //    intPtrDispatcher(this.aeffectPtr, (UInt32)OpCode.effClose, 0, 0, handle, 0);
            //}

            if (this.hModule != 0)
            {
                Kernel32.FreeLibrary(this.hModule);
                this.hModule = 0;
            }
        }

        public void EditOpen()
        {
            // delegate UInt32 DispatcherDelegate(IntPtr effect, UInt32 opCode, UInt32 index, UInt32 value, StringBuilder ptr, float opt);
            intPtrDispatcher(this.aeffectPtr, (UInt32)OpCode.effOpen, 0, 0, IntPtr.Zero, 0);
        }

        public void VstProcess()
        {
            //process(this.aeffectPtr, IntPtr inputs, IntPtr outputs, UInt32 sampleframes);
        }

        public unsafe void VstProcessReplacing(float[][] inputs, float[][] outputs, UInt32 sampleframes)
        {
            //inputs = (float**)malloc(sizeof(float**) * numChannels);
            //outputs = (float**)malloc(sizeof(float**) * numChannels);
            //for (int channel = 0; channel < numChannels; channel++)
            //{
            //    inputs[i] = (float*)malloc(sizeof(float*) * blocksize);
            //    outputs[i] = (float*)malloc(sizeof(float*) * blocksize);
            //}

            var numChannels = 1;
            var size = (int)sampleframes * Marshal.SizeOf<float>();

            float** inp = (float**)Marshal.AllocHGlobal(numChannels * Marshal.SizeOf<IntPtr>());

            inp[0] = (float*)Marshal.AllocHGlobal(size);
            inp[1] = (float*)Marshal.AllocHGlobal(size);
            Marshal.Copy(inputs[0], 0, (IntPtr)(inp[0]), (int)sampleframes);
            Marshal.Copy(inputs[1], 0, (IntPtr)(inp[1]), (int)sampleframes);

            float** outp = (float**)Marshal.AllocHGlobal(numChannels * Marshal.SizeOf<IntPtr>());
            outp[0] = (float*)Marshal.AllocHGlobal(size);
            outp[1] = (float*)Marshal.AllocHGlobal(size);

            //processReplacing(this.aeffectPtr, (IntPtr)(inp[0]), (IntPtr)(outp[0]), sampleframes);
            processReplacing(this.aeffectPtr, (IntPtr)inp, (IntPtr)outp, sampleframes);

            Marshal.Copy((IntPtr)(outp[0]), outputs[0], 0, (int)sampleframes);
            Marshal.Copy((IntPtr)(outp[1]), outputs[1], 0, (int)sampleframes);
        }

        private static string UInt32ToString(UInt32 input)
        {
            char[] magicChars = new char[4];
            magicChars[0] = (char)(input >> 24);
            magicChars[1] = (char)((input >> 16) & 0xFF);
            magicChars[2] = (char)((input >> 8) & 0xFF);
            magicChars[3] = (char)(input & 0xFF);

            return new string(magicChars);
        }

        private static UInt32 AMCallback(IntPtr effect, int opcode, int index, int value, IntPtr ptr, float opt)
        {
            //Console.WriteLine($"{effect}, {opcode}, {index}, {value}, {ptr}, {opt.ToString(CultureInfo.InvariantCulture)}");
            return 1;
        }

        private static bool EnumSyms(string name, ulong address, uint size, IntPtr context)
        {
            availableSymbols.Add(name);
            return true;
        }

        public struct AEffect
        {
#pragma warning disable 0649
            public UInt32 magic;          // must be kEffectMagic ('VstP')
            public IntPtr dispatcher;     // long (VSTCALLBACK* dispatcher) (AEffect* effect, long opCode, long index, long value, void* ptr, float opt);
            public IntPtr process;        // void (VSTCALLBACK* process) (AEffect* effect, float** inputs, float** outputs, long sampleframes);
            public IntPtr setParameter;   //  void(VSTCALLBACK* setParameter) (AEffect* effect, long index, float parameter);
            public IntPtr getParameter;   // float (VSTCALLBACK* getParameter) (AEffect* effect, long index);

            public UInt32 numPrograms;
            public UInt32 numParams;      // all programs are assumed to have numParams parameters
            public UInt32 numInputs;      //
            public UInt32 numOutputs;     //
            public UInt32 flags;          // see constants
            public UInt32 resvd1;         // reserved, must be 0
            public UInt32 resvd2;         // reserved, must be 0
            public UInt32 initialDelay;   // for algorithms which need input in the first place
            public UInt32 realQualities;  // number of realtime qualities (0: realtime)
            public UInt32 offQualities;   // number of offline qualities (0: realtime only)
            public float ioRatio;         // input samplerate to output samplerate ratio, not used yet
            public IntPtr _object;        // void* object;		// for class access (see AudioEffect.hpp), MUST be 0 else!
            public IntPtr user;           // void* user;         // user access
            public UInt32 uniqueID;       // pls choose 4 character as unique as possible.
                                          // this is used to identify an effect for save+load
            public UInt32 version;        //
            public IntPtr processReplacing; // void(VSTCALLBACK* processReplacing) (AEffect* effect, float** inputs, float** outputs, long sampleframes);
            // char future[60];    // pls zero
#pragma warning restore 0649
        };

        enum OpCode
        {
            effOpen = 0,        // initialise
            effClose,           // exit, release all memory and other resources!

            effSetProgram,      // program no in <value>
            effGetProgram,      // return current program no.
            effSetProgramName,  // user changed program name (max 24 char + 0) to as passed in string 
            effGetProgramName,  // stuff program name (max 24 char + 0) into string 

            effGetParamLabel,   // stuff parameter <index> label (max 8 char + 0) into string
                                // (examples: sec, dB, type)
            effGetParamDisplay, // stuff parameter <index> textual representation into string
                                // (examples: 0.5, -3, PLATE)
            effGetParamName,    // stuff parameter <index> label (max 8 char + 0) into string
                                // (examples: Time, Gain, RoomType) 
            effGetVu,           // called if (flags & (effFlagsHasClip | effFlagsHasVu))

            // system

            effSetSampleRate,   // in opt (float)
            effSetBlockSize,    // in value
            effMainsChanged,    // the user has switched the 'power on' button to
                                // value (0 off, else on). This only switches audio
                                // processing; you should flush delay buffers etc.
                                // editor

            effEditGetRect,     // stuff rect (top, left, bottom, right) into ptr
            effEditOpen,        // system dependant Window pointer in ptr
            effEditClose,       // no arguments
            effEditDraw,        // draw method, ptr points to rect
            effEditMouse,       // index: x, value: y
            effEditKey,         // system keycode in value
            effEditIdle,        // no arguments. Be gentle!
            effEditTop,         // window has topped, no arguments
            effEditSleep,       // window goes to background

            // new

            effIdentify,        // returns 'NvEf'
            effGetChunk,        // host requests pointer to chunk into (void**)ptr, byteSize returned
            effSetChunk,        // plug-in receives saved chunk, byteSize passed

            effNumOpcodes
        };
    }
}
