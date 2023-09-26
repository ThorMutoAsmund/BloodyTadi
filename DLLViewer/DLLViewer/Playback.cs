using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DLLViewer
{
    // See
    // https://github.com/naudio/NAudio/blob/master/Docs/AsioPlayback.md
    // https://markheath.net/post/how-to-record-and-play-audio-at-same

    public class Playback
    {
        private AudioFileReader audioFileReader;
        private Dictionary<int, string> outputDriverNames;
        public List<AudioNode> Chain { get; private set; }
        private Dictionary<string, string> vstFileMappings;
        private List<VstSampleProvider> vsts;
        private List<string> vstNames;
        private VstSampleProvider currentVST;

        public Playback(Dictionary<int, string> outputDriverNames, List<AudioNode> chain, Dictionary<string, string> vstFileMappings)
        {
            this.audioFileReader = new AudioFileReader(@"example.wav");
            this.outputDriverNames = outputDriverNames;
            this.Chain = chain;
            this.vstFileMappings = vstFileMappings;
        }


        private bool ProcessCommand(string arg)
        {
            if (this.currentVST == null || String.IsNullOrEmpty(arg))
            {
                return true;
            }

            var effect = this.currentVST.Effect;
            string param = arg.Substring(0, 1);

            switch (param.ToLowerInvariant())
            {
 
                case "v":
                    {
                        int selectedVSTNum = 0;
                        if (arg.Length > 1)
                        {
                            if (!int.TryParse(arg.Substring(1), out selectedVSTNum))
                            {
                                return false;
                            }
                        }
                        SelectVST(selectedVSTNum);

                        break;
                    }
                case "l":
                    {
                        effect.ListParameters(true);
                        break;
                    }
                default:
                    {
                        var split = arg.Split(new char[] { ' ' });
                        if (split.Length != 2)
                        {
                            return false;
                        }
                        if (!UInt32.TryParse(split[0], out var index))
                        {
                            return false;
                        }
                        if (!float.TryParse(split[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var parameter))
                        {
                            return false;
                        }

                        effect.SetParameter(index, parameter);

                        Console.WriteLine(effect.GetParamDisplay(index));
                        //Console.WriteLine($" {effect.GetParamLabel(index)}");

                        break;
                    }
            }

            return true;
        }

        private void SelectVST(int vstIndex)
        {
            if (vstIndex >= 0 && vstIndex < vsts.Count)
            {
                this.currentVST = vsts[vstIndex];
                Console.WriteLine($"Selected {this.vstNames[vstIndex]}");
            }
            else
            {
                Console.WriteLine("Index outside vst list scope");
            }
        }

        private void GetInput()
        {
            if (this.vsts.Count > 0)
            {
                SelectVST(0);
            }
            string command = String.Empty;
            ConsoleKeyInfo key;

            do
            {
                Console.Write("$ ");
                do
                {
                    while (!Console.KeyAvailable)
                    {
                        Thread.Sleep(25);
                    }
                    key = Console.ReadKey(true);
                    if (key.KeyChar >= 0x20)
                    {
                        command += key.KeyChar;
                        Console.Write(key.KeyChar);
                    }
                    else if (key.Key == ConsoleKey.Backspace)
                    {
                        command = command.Substring(0, command.Length - 1);
                        Console.Write($"{(char)8} {(char)8}");
                    }
                    //else
                    //{
                    //    Console.Write($"({(int)key.KeyChar})");
                    //}
                }
                while (key.Key != ConsoleKey.Enter && key.Key != ConsoleKey.Escape);

                if (key.Key != ConsoleKey.Escape)
                {
                    Console.WriteLine();
                    if (!ProcessCommand(command))
                    {
                        Console.WriteLine($"Error in command {command}");
                    }

                    command = String.Empty;
                }
            }
            while (key.Key != ConsoleKey.Escape);
            Console.WriteLine("Finished");
        }

        public void WaveOut(OutputDeviceMode outputDeviceMode, int outputDeviceNum, InputDeviceMode inputDeviceMode, int inputDeviceNum)
        {
            this.vsts = new List<VstSampleProvider>();
            this.vstNames = new List<string>();

            if (outputDeviceMode == OutputDeviceMode.Unset)
            {
                outputDeviceNum = 0;
            }
            if (inputDeviceMode == InputDeviceMode.Unset)
            {
                inputDeviceNum = 0;
            }

            // things to be disposed
            var disposeList = new List<IDisposable>();

            // create input provider
            IWaveProvider finalProvider;
            IWaveIn recorder = null;

            if (inputDeviceMode == InputDeviceMode.Demo)
            {
                finalProvider = this.audioFileReader;
            }
            else 
            {
                BufferedWaveProvider bufferedWaveProvider;

                void RecorderOnDataAvailable(object sender, WaveInEventArgs waveInEventArgs)
                {
                    bufferedWaveProvider.AddSamples(waveInEventArgs.Buffer, 0, waveInEventArgs.BytesRecorded);
                }

                recorder = new WaveInEvent();
                bufferedWaveProvider = new BufferedWaveProvider(recorder.WaveFormat);
                recorder.DataAvailable += RecorderOnDataAvailable;
                finalProvider = bufferedWaveProvider;
                disposeList.Add(recorder);
            }

            // add plugins to chain
            if (this.Chain.Count > 0)
            {
                var firstLink = this.Chain[0];
                if (this.vstFileMappings.ContainsKey(firstLink.UniqueID))
                {
                    var vstProvider = VstSampleProvider.Create(finalProvider, this.vstFileMappings[firstLink.UniqueID]);
                    if (vstProvider != null)
                    {
                        this.vsts.Add(vstProvider);
                        this.vstNames.Add(this.vstFileMappings[firstLink.UniqueID]);
                        disposeList.Add(vstProvider);
                        var conv = new SampleToWaveProvider16(vstProvider);
                        finalProvider = conv;
                    }
                }
            }

            // set up playback
            using (var player = new WaveOutEvent())
            {
                player.Init(finalProvider);
                player.Play();
                recorder?.StartRecording();

                GetInput();

                recorder?.StopRecording();
                player.Stop();

                foreach (var itemToDispose in disposeList)
                {
                    itemToDispose.Dispose();
                }
            }
        }

        public void DirectSound(OutputDeviceMode outputDeviceMode, int outputDeviceNum, InputDeviceMode inputDeviceMode, int inputDeviceNum)
        {
            this.vsts = new List<VstSampleProvider>();
            this.vstNames = new List<string>();

            if (outputDeviceMode == OutputDeviceMode.Unset)
            {
                outputDeviceNum = 0;
            }
            if (inputDeviceMode == InputDeviceMode.Unset)
            {
                inputDeviceNum = 0;
            }

            // things to be disposed
            var disposeList = new List<IDisposable>();

            // create input provider
            IWaveProvider finalProvider;
            IWaveIn recorder = null;

            if (inputDeviceMode == InputDeviceMode.Demo)
            {
                finalProvider = this.audioFileReader;
            }
            else
            {
                BufferedWaveProvider bufferedWaveProvider;

                void RecorderOnDataAvailable(object sender, WaveInEventArgs waveInEventArgs)
                {
                    bufferedWaveProvider.AddSamples(waveInEventArgs.Buffer, 0, waveInEventArgs.BytesRecorded);
                }

                recorder = new WaveInEvent();
                bufferedWaveProvider = new BufferedWaveProvider(recorder.WaveFormat);
                recorder.DataAvailable += RecorderOnDataAvailable;
                finalProvider = bufferedWaveProvider;
                disposeList.Add(recorder);
            }

            // add plugins to chain
            if (this.Chain.Count > 0)
            {
                var firstLink = this.Chain[0];
                if (this.vstFileMappings.ContainsKey(firstLink.UniqueID))
                {
                    var vstProvider = VstSampleProvider.Create(finalProvider, this.vstFileMappings[firstLink.UniqueID]);
                    if (vstProvider != null)
                    {
                        this.vsts.Add(vstProvider);
                        this.vstNames.Add(this.vstFileMappings[firstLink.UniqueID]);
                        disposeList.Add(vstProvider);
                        var conv = new SampleToWaveProvider16(vstProvider);
                        finalProvider = conv;
                    }
                }
            }

            // set up playback
            using (var player = new DirectSoundOut())
            {
                player.Init(finalProvider);
                player.Play();
                recorder?.StartRecording();

                GetInput();

                recorder?.StopRecording();
                player.Stop();

                foreach (var itemToDispose in disposeList)
                {
                    itemToDispose.Dispose();
                }
            }
        }

        public void Asio(OutputDeviceMode outputDeviceMode, int outputDeviceNum, InputDeviceMode inputDeviceMode, int inputDeviceNum)
        {
            if (outputDeviceMode == OutputDeviceMode.Unset)
            {
                outputDeviceNum = 0;
            }
            if (inputDeviceMode == InputDeviceMode.Unset)
            {
                inputDeviceNum = 0;
            }

            if (!outputDriverNames.ContainsKey(outputDeviceNum))
            {
                Console.WriteLine($"Output device {outputDeviceNum} not found.");
                return;
            }

            // https://github.com/naudio/NAudio/blob/master/Docs/AsioRecording.md

            var asioOut = new AsioOut(outputDriverNames[outputDeviceNum]);

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
            
            if (inputDeviceMode == InputDeviceMode.Demo)
            {
                asioOut.Init(audioFileReader);
            }
            else
            {
                asioOut.InitRecordAndPlayback(audioFileReader, recordChannelCount, sampleRate);
            }

            asioOut.Play();

            Console.ReadLine();

            asioOut.Stop();
            asioOut.Dispose();
        }
    }
}
