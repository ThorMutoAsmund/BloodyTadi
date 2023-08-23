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
        static int? outputDeviceNum;
        static int? inputDeviceNum;
        static AudioFileReader audioFile;

        [STAThread]
        static void Main(string[] args)
        {
            // Get file path
            if (args.Length < 1 || args[0] == "/?")
            {
                Console.WriteLine("Work with VST DLLs.");
                Console.WriteLine();
                Console.WriteLine("VSTINFO [path][filename]");
                Console.WriteLine("VSTINFO /o");
                Console.WriteLine("VSTINFO /i");
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

            audioFile = new AudioFileReader(@"c:\temp\example.mp3");
            fileNames = new List<string>();
            pathNames = new List<string>();

            string param = String.Empty;
            var enterActiveMode = true;

            bool listOutputDevices = false;
            bool listInputDevices = false;
            bool listVSTs = false;

            foreach (var arg in args)
            {
                if (arg.StartsWith("/"))
                {
                    param = arg.Substring(1); // Remove slash

                    switch (param.Substring(0, 1).ToLowerInvariant())
                    {
                        case "o":
                            {
                                if (param.Length == 1)
                                {
                                    listOutputDevices = true;
                                    enterActiveMode = false;
                                }
                                else
                                {
                                    param = param.Substring(1);
                                    if (!Int32.TryParse(param, out var outputDeviceNum))
                                    {
                                        Console.WriteLine($"Error parsing {param}.");
                                        return;
                                    }
                                }
                                continue;
                            }
                        case "i":
                            {
                                if (param.Length == 1)
                                {
                                    listInputDevices = true;
                                    enterActiveMode = false;
                                }
                                else
                                {
                                    param = param.Substring(1);
                                    if (!Int32.TryParse(param, out var inputDeviceNum))
                                    {
                                        Console.WriteLine($"Error parsing {param}.");
                                        return;
                                    }
                                }
                                continue;
                            }
                        case "l" when param.Length == 1:
                            {
                                listVSTs = true;
                                enterActiveMode = false;
                                continue;
                            }
                        case "d":
                            {
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
            }

            ListDevices(DataFlow.Render, listOutputDevices);
            
            ListDevices(DataFlow.Capture, listInputDevices && (!listOutputDevices || (audioSystem != AudioSystem.DirectSound && audioSystem != AudioSystem.ASIO)));

            if (listVSTs)
            {
                Console.WriteLine(String.Join(",", fileNames.ToArray()));

                foreach (var path in pathNames)
                {
                    fileNames.AddRange(Directory.GetFiles(path));
                }
                ListVSTs();
            }

            if (enterActiveMode)
            {
                ActiveMode();
            }
        }

        private static void ActiveMode()
        {
            switch (audioSystem)
            {
                case AudioSystem.ASIO:
                    AsioPlayback();
                    break;
                case AudioSystem.WASAPI:
                    Console.WriteLine($"{audioSystem} not supported yet");
                    break;
                case AudioSystem.DirectSound:
                    Console.WriteLine($"{audioSystem} not supported yet");
                    break;
                case AudioSystem.WaveOut:
                    Console.WriteLine($"{audioSystem} not supported yet");
                    break;
            }

        }

        private static void AsioPlayback()
        {
            outputDeviceNum = outputDeviceNum ?? 0;
            inputDeviceNum = inputDeviceNum ?? 0;


            if (!outputDriverNames.ContainsKey(outputDeviceNum.Value))
            {
                Console.WriteLine($"Output device {outputDeviceNum.Value} not found.");
                return;
            }

            // https://github.com/naudio/NAudio/blob/master/Docs/AsioRecording.md

            var asioOut = new AsioOut(outputDriverNames[outputDeviceNum.Value]);

            var inputChannelCount = asioOut.DriverInputChannelCount;
            var outputChannelCount = asioOut.DriverOutputChannelCount;

            var inputChannelOffset = 0;
            var recordChannelCount = 2;
            var sampleRate = 44100;
            var outputChannelOffset = 0;

            var ms = new MemoryStream();
            var rs = new RawSourceWaveStream(ms, new WaveFormat(sampleRate, 16, 1));

            //var samples[] = new Sa

            unsafe void OnAsioOutAudioAvailable(object sender, AsioAudioAvailableEventArgs e)
            {
                // e.InputBuffers.Length == 2
                // e.OutputBuffers.Length == 2
                // e.SamplesPerBuffer == 512

                for (int n = 0; n < e.SamplesPerBuffer; ++n)
                {                    
                    var value = *((float*)e.InputBuffers[0] + n);
                    *((float*)e.OutputBuffers[0] + n) = value;
                }

                //var dest = new byte[e.SamplesPerBuffer*4];
                //Console.WriteLine(e.AsioSampleType);

                //Marshal.Copy(e.InputBuffers[0], dest, 0, e.SamplesPerBuffer * 4);
                //Marshal.Copy(dest, 0, e.OutputBuffers[0], e.SamplesPerBuffer * 4);
                //Marshal.Copy(e.InputBuffers[1], dest, 0, e.SamplesPerBuffer * 4);
                //Marshal.Copy(dest, 0, e.OutputBuffers[1], e.SamplesPerBuffer * 4);

                //Buffer.MemoryCopy((void*)e.InputBuffers[0], (void*)e.OutputBuffers[0], e.SamplesPerBuffer, e.SamplesPerBuffer);
                //Buffer.MemoryCopy((void*)e.InputBuffers[1], (void*)e.OutputBuffers[1], e.SamplesPerBuffer, e.SamplesPerBuffer);
                e.WrittenToOutputBuffers = true;

                //e.OutputBuffers
                //Marshal.Copy(e.InputBuffers[i], buf, 0, e.SamplesPerBuffer);

                //Console.WriteLine($"{e.SamplesPerBuffer}  {e.InputBuffers.Length}  {e.OutputBuffers.Length}");

                //Marshal.Copy(e.InputBuffers[i], buf, 0, e.SamplesPerBuffer);

                //byte[] buf = new byte[e.SamplesPerBuffer];
                //for (int i = 0; i < e.InputBuffers.Length; i++)
                //{
                //    Marshal.Copy(e.InputBuffers[i], buf, 0, e.SamplesPerBuffer);
                //    //Aggiungo in coda al buffer
                //    buffer.AddSamples(buf, 0, buf.Length);
                //}
                //var samples = e.GetAsInterleavedSamples(samples);
                //foreach (var s in samples)
                //{
                //    //ms.Write(");
                //}
                //writer.WriteSamples(samples, 0, samples.Length);
                // Write recorded data to sample provider
            }


            asioOut.ChannelOffset = outputChannelOffset;
            asioOut.InputChannelOffset = inputChannelOffset;
            asioOut.AudioAvailable += OnAsioOutAudioAvailable;
            asioOut.InitRecordAndPlayback(audioFile, recordChannelCount, sampleRate);

            //asioOut.Init(audioFile);

            asioOut.Play();

            Console.ReadLine();

            asioOut.Stop();
            asioOut.Dispose();
        }

        private static void ListVSTs()
        {
            var first = true;
            foreach (var filePath in fileNames)
            {
                if (!first)
                {
                    Console.WriteLine();
                }
                first = false;
                Console.WriteLine($"--------------------------------");
                Console.WriteLine($"[{Path.GetFileName(filePath)}]");

                var effect = AudioEffect.Create(filePath);

                if (effect == null)
                {
                    continue;
                }

                Console.WriteLine($"Unique ID = {effect.UniqueID}");

                Console.WriteLine($"{effect.NumParams} parameters");
                for (UInt32 i = 0; i < effect.NumParams; ++i)
                {
                    Console.WriteLine($"Param {i} = {effect.GetParamName(i)}");
                }

                //effect.Open();
            }
        }

        private static void ListDevices(DataFlow dataFlow, bool showInfo)
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
