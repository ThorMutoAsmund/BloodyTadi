using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DLLViewer
{
    // First clone
    // https://github.com/kmatheussen/vstserver.git

    // 64 bit DLL issues
    // https://stackoverflow.com/questions/15396897/using-a-64bit-dll-in-a-32bit-application
    // https://stackoverflow.com/questions/42360367/how-to-load-a-32bit-dll-in-a-64bit-windows

    internal class Program
    {
        static AudioSystem audioSystem = AudioSystem.WaveOut;
        static List<string> fileNames;
        static List<string> pathNames;
        static Dictionary<int,string> inputDriverNames = new Dictionary<int, string>();
        static Dictionary<int, string> outputDriverNames = new Dictionary<int, string>();
        static Dictionary<string, string> vsts = new Dictionary<string, string>();
        static OutputDeviceMode outputDeviceMode = OutputDeviceMode.Unset;
        static int outputDeviceNum;
        static InputDeviceMode inputDeviceMode = InputDeviceMode.Unset;
        static int inputDeviceNum;
        static List<AudioNode> chain = new List<AudioNode>();

        [STAThread]
        static void Main(string[] args)
        {
            // Get file path
            if (args.Length < 1 || args[0] == "/?")
            {
                Console.WriteLine("Work with VST DLLs.");
                Console.WriteLine();
                Console.WriteLine("VSTINFO [path][filename] [/O] [/I] [/L]");
                Console.WriteLine("  /O      List output devices");
                Console.WriteLine("  /I      List input devices");
                Console.WriteLine("  /O:n    Select output device n");
                Console.WriteLine("  /I:n    Select input device n");
                Console.WriteLine("  /I:d    Play demo sample");
                Console.WriteLine("  /L      List VST plugins with details in given path (and/or matching filename)");
                Console.WriteLine("  /DW     Use WaveOut");
                Console.WriteLine("  /DD     Use DirectSound");
                Console.WriteLine("  /DA     Use ASIO");
                Console.WriteLine("  /DS     Use WASAPI");
                Console.WriteLine("  /V:id   Add VST plugin with id to the chain");
                return;
            }

            if (Environment.Is64BitProcess)
            {
                Console.WriteLine("VSTINFO (64-bit process)");
            }
            else
            {
                Console.WriteLine("VSTINFO (32-bit process)");
            }

            fileNames = new List<string>();
            pathNames = new List<string>();

            string param = String.Empty;
            var enterActiveMode = true;

            bool listOutputDevices = false;
            bool listInputDevices = false;
            bool listVSTs = false;
            bool skipRest = false;

            foreach (var arg in args)
            {
                if (arg.StartsWith("/"))
                {
                    param = arg.Substring(1); // Remove slash

                    switch (param.Substring(0, 1).ToLowerInvariant())
                    {
                        case "-":
                            {
                                skipRest = true;
                                break;
                            }
                        case "o":
                            {
                                if (param.Length <= 2)
                                {
                                    listOutputDevices = true;
                                    enterActiveMode = false;
                                }
                                else
                                {
                                    param = param.Substring(2);
                                    if (!Int32.TryParse(param, out outputDeviceNum))
                                    {
                                        Console.WriteLine($"Error parsing {param}.");
                                        return;
                                    }
                                    outputDeviceMode = OutputDeviceMode.Driver;
                                }
                                break;
                            }
                        case "i":
                            {
                                if (param.Length <= 2)
                                {
                                    listInputDevices = true;
                                    enterActiveMode = false;
                                }
                                else
                                {
                                    param = param.Substring(2);
                                    if (param.ToLowerInvariant() == "d")
                                    {
                                        inputDeviceMode = InputDeviceMode.Demo;
                                    }
                                    else
                                    {
                                        if (!Int32.TryParse(param, out inputDeviceNum))
                                        {
                                            Console.WriteLine($"Error parsing {param}.");
                                            return;
                                        }
                                        inputDeviceMode = InputDeviceMode.Driver;
                                    }
                                }
                                break;
                            }
                        case "l" when param.Length == 1:
                            {
                                listVSTs = true;
                                enterActiveMode = false;
                                break;
                            }
                        case "d":
                            {
                                param = param.ToLowerInvariant();
                                if (param == "dw")
                                {
                                    audioSystem = AudioSystem.WaveOut;
                                }
                                else if (param == "dd")
                                {
                                    audioSystem = AudioSystem.DirectSound;
                                }
                                else if (param == "da")
                                {
                                    audioSystem = AudioSystem.ASIO;
                                }
                                else if (param == "ds")
                                {
                                    audioSystem = AudioSystem.WASAPI;
                                }
                                else
                                {
                                    Console.WriteLine($"Unknown command /{param}.");
                                    return;
                                }

                                break;
                            }
                        case "v":
                            {
                                if (param.Length <= 2)
                                {
                                    break;
                                }
                                else
                                {
                                    chain.Add(new AudioNode()
                                    {
                                        UniqueID = param.Substring(2)
                                    });
                                }
                                continue;
                            }
                        default:
                            {
                                Console.WriteLine($"Unknown command /{param}.");
                                return;
                            }
                    }
                }
                else
                {
                    if (File.Exists(arg))
                    {
                        fileNames.Add(arg);
                    }
                    else if (Directory.Exists(arg))
                    {
                        pathNames.Add(arg);
                    }
                    else
                    {
                        var arged = arg.Replace("*", "_").Replace("?", "_");
                        var rootPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                        var pathToScan = Path.GetDirectoryName(arged);
                        var fileToMatch = pathToScan.Length == 0 ? arg : arg.Substring(pathToScan.Length + 1);

                        if (!string.IsNullOrEmpty(fileToMatch))
                        {
                            var regex = FindFilesPatternToRegex.Convert(fileToMatch);

                            if (!Directory.Exists(pathToScan))
                            {
                                pathToScan = Path.Combine(rootPath, pathToScan);
                            }
                            if (Directory.Exists(pathToScan))
                            {
                                bool fileFound = false;
                                foreach (var file in Directory.GetFiles(pathToScan))
                                {
                                    var fileName = Path.GetFileName(file);
                                         
                                    if (regex.IsMatch(fileName))
                                    {
                                        fileNames.Add(file);
                                        fileFound = true;
                                    }
                                }

                                if (!fileFound)
                                {
                                    Console.WriteLine($"No matching file found.");
                                }

                            }
                            else
                            {
                                Console.WriteLine($"File not found {arg}.");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"File not found {arg}.");
                        }
                    }
                }

                if (skipRest)
                {
                    break;
                }
            }

            EnumerateDevices(DataFlow.Render, listOutputDevices);
            
            EnumerateDevices(DataFlow.Capture, listInputDevices && (!listOutputDevices || (audioSystem != AudioSystem.DirectSound && audioSystem != AudioSystem.ASIO)));

            foreach (var path in pathNames)
            {
                fileNames.AddRange(Directory.GetFiles(path));
            }

            EnumerateVSTs(listVSTs);

            if (enterActiveMode)
            {
                ActiveMode();
            }
        }

        private static void ActiveMode()
        {
            var playback = new Playback(outputDriverNames, chain, vsts);

            switch (audioSystem)
            {
                case AudioSystem.ASIO:
                    Console.WriteLine($"Using {audioSystem}");
                    playback.Asio(outputDeviceMode, outputDeviceNum, inputDeviceMode, inputDeviceNum);
                    break;
                case AudioSystem.WASAPI:
                    Console.WriteLine($"{audioSystem} not supported yet");
                    break;
                case AudioSystem.DirectSound:
                    Console.WriteLine($"{audioSystem} not supported yet");
                    break;
                case AudioSystem.WaveOut:
                    Console.WriteLine($"Using {audioSystem}");
                    playback.WaveOut(outputDeviceMode, outputDeviceNum, inputDeviceMode, inputDeviceNum);
                    break;
            }

        }
        private static void EnumerateVSTs(bool showInfo)
        {
            var first = true;
            foreach (var filePath in fileNames)
            {
                if (showInfo)
                {
                    if (!first)
                    {
                        Console.WriteLine();
                    }
                    first = false;
                    Console.WriteLine($"--------------------------------");
                    Console.WriteLine($"[{Path.GetFileName(filePath)}]");
                }

                var effect = AudioEffect.Create(filePath);

                if (effect == null)
                {
                    continue;
                }

                if (showInfo)
                {
                    Console.WriteLine($"Unique ID = {effect.UniqueID}");
                    Console.WriteLine($"{effect.NumParams} parameters");
                    for (UInt32 i = 0; i < effect.NumParams; ++i)
                    {
                        Console.WriteLine($"Param {i} = {effect.GetParamName(i)}");
                    }
                }

                vsts[effect.UniqueID] = filePath;
            }
        }

        private static void EnumerateDevices(DataFlow dataFlow, bool showInfo)
        {
            switch (audioSystem)
            {
                case AudioSystem.WaveOut:
                    {
                        if (dataFlow == DataFlow.Capture)
                        {
                            if (showInfo)
                            {
                                Console.WriteLine($"Input devices ({audioSystem})");
                            }
                            for (int n = -1; n < WaveIn.DeviceCount; n++)
                            {
                                var caps = WaveIn.GetCapabilities(n);
                                if (showInfo)
                                {
                                    Console.WriteLine($"  {n}: {caps.ProductName}");
                                }
                                //inputDriverNames[n] = caps.NameGuid; ??
                            }
                        }
                        else
                        {
                            if (showInfo)
                            {
                                Console.WriteLine($"Output devices ({audioSystem})");
                            }
                            for (int n = -1; n < WaveOut.DeviceCount; n++)
                            {
                                var caps = WaveOut.GetCapabilities(n);
                                if (showInfo)
                                {
                                    Console.WriteLine($"  {n}: {caps.ProductName}");
                                }
                                //outputDriverNames[n] = caps.NameGuid; ??
                            }
                        }
                        break;
                    }
                case AudioSystem.DirectSound:
                    {
                        if (showInfo)
                        {
                            Console.WriteLine($"Input/output devices ({audioSystem})");
                        }
                        var n = 0;
                        foreach (var dev in DirectSoundOut.Devices)
                        {
                            if (showInfo)
                            {
                                Console.WriteLine($"  {n++}: {dev.Description}");
                            }
                            //inputDriverNames[n] = caps.NameGuid; ??
                            //outputDriverNames[n] = caps.NameGuid; ??
                        }
                        break;
                    }
                case AudioSystem.ASIO:
                    {
                        if (showInfo)
                        {
                            Console.WriteLine($"Input/output devices ({audioSystem})");
                        }
                        var n = 0;
                        foreach (var asio in AsioOut.GetDriverNames())
                        {
                            if (showInfo)
                            {
                                Console.WriteLine($"  {n++}: {asio}");
                            }
                            inputDriverNames[n] = asio;
                            outputDriverNames[n] = asio;
                        }
                        break;
                    }
                case AudioSystem.WASAPI:
                    {
                        if (showInfo)
                        {
                            Console.WriteLine($"{(dataFlow == DataFlow.Capture ? "Input" : "Output")} devices ({audioSystem})");
                        }
                        var enumerator = new MMDeviceEnumerator();
                        var n = 0;
                        foreach (var wasapi in enumerator.EnumerateAudioEndPoints(dataFlow, DeviceState.Active))
                        {
                            if (showInfo)
                            {
                                Console.WriteLine($"  {n++}: {wasapi.FriendlyName}");
                            }
                            if (dataFlow == DataFlow.Capture)
                            {
                                //inputDriverNames[n] = caps.NameGuid; ??
                            }
                            else
                            {
                                //outputDriverNames[n] = caps.NameGuid; ??
                            }
                        }
                        break;
                    }
            }
        }

        private static class FindFilesPatternToRegex
        {
            private static Regex HasQuestionMarkRegEx = new Regex(@"\?", RegexOptions.Compiled);
            private static Regex IllegalCharactersRegex = new Regex("[" + @"\/:<>|" + "\"]", RegexOptions.Compiled);
            private static Regex CatchExtentionRegex = new Regex(@"^\s*.+\.([^\.]+)\s*$", RegexOptions.Compiled);
            private static string NonDotCharacters = @"[^.]*";
            public static Regex Convert(string pattern)
            {
                if (pattern == null)
                {
                    throw new ArgumentNullException();
                }
                pattern = pattern.Trim();
                if (pattern.Length == 0)
                {
                    throw new ArgumentException("Pattern is empty.");
                }
                if (IllegalCharactersRegex.IsMatch(pattern))
                {
                    throw new ArgumentException("Pattern contains illegal characters.");
                }
                bool hasExtension = CatchExtentionRegex.IsMatch(pattern);
                bool matchExact = false;
                if (HasQuestionMarkRegEx.IsMatch(pattern))
                {
                    matchExact = true;
                }
                else if (hasExtension)
                {
                    matchExact = CatchExtentionRegex.Match(pattern).Groups[1].Length != 3;
                }
                string regexString = Regex.Escape(pattern);
                regexString = "^" + Regex.Replace(regexString, @"\\\*", ".*");
                regexString = Regex.Replace(regexString, @"\\\?", ".");
                if (!matchExact && hasExtension)
                {
                    regexString += NonDotCharacters;
                }
                regexString += "$";
                Regex regex = new Regex(regexString, RegexOptions.Compiled | RegexOptions.IgnoreCase);
                return regex;
            }
        }

    }
}
