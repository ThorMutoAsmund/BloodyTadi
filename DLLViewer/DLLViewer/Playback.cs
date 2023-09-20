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
    // See
    // https://github.com/naudio/NAudio/blob/master/Docs/AsioPlayback.md
    // https://markheath.net/post/how-to-record-and-play-audio-at-same

    public class Playback
    {
        private AudioFileReader audioFile;

        public Playback()
        {
            this.audioFile = new AudioFileReader(@"c:\temp\example.mp3");
        }

        public void WaveOut(int? outputDeviceNum, int? inputDeviceNum, Dictionary<int, string> outputDriverNames, bool playTestSample = true)
        {
            using (var outputDevice = new WaveOutEvent())
            {
                outputDevice.Init(this.audioFile);
                outputDevice.Play();

                Console.ReadLine();

                outputDevice.Stop();
                outputDevice.Dispose();

                //while (outputDevice.PlaybackState == PlaybackState.Playing)
                //{
                //    Thread.Sleep(1000);
                //}
            }
        }

        public void Asio(int? outputDeviceNum, int? inputDeviceNum, Dictionary<int, string> outputDriverNames, bool playTestSample = false)
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
            
            if (playTestSample)
            {
                asioOut.Init(audioFile);
            }
            else
            {
                asioOut.InitRecordAndPlayback(audioFile, recordChannelCount, sampleRate);
            }

            asioOut.Play();

            Console.ReadLine();

            asioOut.Stop();
            asioOut.Dispose();
        }
    }
}
