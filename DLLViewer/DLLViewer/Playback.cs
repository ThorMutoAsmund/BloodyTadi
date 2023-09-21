using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.IO;

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
        public Dictionary<string, string> VSTs { get; private set; }

        public Playback(Dictionary<int, string> outputDriverNames, List<AudioNode> chain, Dictionary<string, string> vsts)
        {
            this.audioFileReader = new AudioFileReader(@"example.wav");
            this.outputDriverNames = outputDriverNames;
            this.Chain = chain;
            this.VSTs = vsts;
        }

        public void WaveOut(OutputDeviceMode outputDeviceMode, int outputDeviceNum, InputDeviceMode inputDeviceMode, int inputDeviceNum)
        {
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
                if (this.VSTs.ContainsKey(firstLink.UniqueID))
                {
                    var vstProvider = VstSampleProvider.Create(finalProvider, this.VSTs[firstLink.UniqueID]);
                    if (vstProvider != null)
                    {
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

                Console.ReadLine();

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
