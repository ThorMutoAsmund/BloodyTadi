using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;

namespace DLLViewer
{
    public class Pcm16BitToSampleProvider2 : SampleProviderConverterBase
    {
        /// <summary>
        /// Initialises a new instance of Pcm16BitToSampleProvider
        /// </summary>
        /// <param name="source">Source wave provider</param>
        public Pcm16BitToSampleProvider2(IWaveProvider source)
            : base(source)
        {
        }

        /// <summary>
        /// Reads samples from this sample provider
        /// </summary>
        /// <param name="buffer">Sample buffer</param>
        /// <param name="offset">Offset into sample buffer</param>
        /// <param name="count">Samples required</param>
        /// <returns>Number of samples read</returns>
        public override int Read(float[] buffer, int offset, int count)
        {
            int sourceBytesRequired = count * 4;
            EnsureSourceBuffer(sourceBytesRequired);
            int bytesRead = source.Read(sourceBuffer, 0, sourceBytesRequired);
            int outIndex = offset;
            for (int n = 0; n < bytesRead; n += 4)
            {
                var f = BitConverter.ToUInt32(sourceBuffer, n);
                //long f2 = ((f & 0x000000FF)<<24) +
                //    ((f & 0x0000FF00) << 8) +
                //    ((f & 0x00FF0000) >> 8) +
                //    ((f & 0xFF000000) >> 24);
                buffer[outIndex++] = (f / (65536f * 65536f))-0.5f; // HERE!
            }
            return bytesRead / 4;
        }
    }

    class VstSampleProvider : ISampleProvider, IDisposable
    {
        private IWaveProvider sourceWaveProvider;
        private ISampleProvider provider;
        private bool isWriterDisposed;
        private AudioEffect effect;

        public WaveFormat WaveFormat => this.provider.WaveFormat;

        public static VstSampleProvider Create(IWaveProvider sourceWaveProvider, string filePath)
        {
            var effect = AudioEffect.Create(filePath);
            if (effect == null)
            {
                return null;
            }

            Console.WriteLine($"Added {effect.UniqueID}");

            return new VstSampleProvider(sourceWaveProvider, effect);
        }
        
        public VstSampleProvider(IWaveProvider sourceWaveProvider, AudioEffect effect)
        {
            this.sourceWaveProvider = sourceWaveProvider;
            this.effect = effect;
            this.effect.Open();

            this.provider = new Pcm16BitToSampleProvider2(this.sourceWaveProvider);
        }

        private float[] bin;
        private float[] binL;
        private float[] binR;
        private float[] boutL;
        private float[] boutR;

        // Count is usually: 150 * sampleFreqKHz * channels
        //  So it is 1200  for an    8 kHz mono   16bit
        // and it is 13230 for an 44.1 kHz stereo 16bit
        public int Read(float[] buffer, int offset, int count)
        {
            int read = 0;

            if (count > 0 && !this.isWriterDisposed)
            {
                Array.Resize(ref this.bin, count);
                read = this.provider.Read(this.bin, 0, count);
                 
                if (this.provider.WaveFormat.Channels == 1)
                {
                    this.binL = this.bin;
                    Array.Resize(ref this.binR, count);
                    Array.Copy(this.binL, this.binR, count);
                    this.boutL = buffer;
                    Array.Resize(ref this.boutR, count);
                }
                else if (this.provider.WaveFormat.Channels == 2)
                {
                    Array.Resize(ref this.binL, count / 2);
                    Array.Resize(ref this.binR, count / 2);
                    Array.Resize(ref this.boutL, count / 2);
                    Array.Resize(ref this.boutR, count / 2);
                    for (int i = 0; i < count; i += 2)
                    {
                        this.binL[i / 2] = this.bin[i];
                        this.binR[i / 2] = this.bin[i+1];
                    }
                }

                //this.effect.VstProcessReplacing(new float[2][] { this.binL, this.binR }, new float[2][] { this.boutL, this.boutR }, (UInt32)(count / this.provider.WaveFormat.Channels));
                boutL = binL;
                boutR = binR;

                if (this.provider.WaveFormat.Channels == 2)
                {
                    for (int i = 0; i < count; i += 2)
                    {
                        buffer[i] = this.boutL[i / 2];
                        buffer[i+1] = this.boutR[i / 2];
                    }
                }

                read = count;
            }

            if (count == 0)
            {
                Dispose(); // auto-dispose in case users forget
            }

            return read;
        }

        public void Dispose()
        {
            if (!this.isWriterDisposed)
            {
                this.isWriterDisposed = true;
                this.effect.Close();
                //writer.Dispose();
            }
        }
    }

    class VSTWaveProvider_old : IWaveProvider, IDisposable
    {
        private IWaveProvider sourceWaveProvider;
        //private readonly WaveFileWriter writer;
        private bool isWriterDisposed;
        private AudioEffect effect;
        private ISampleProvider provider;

        private VSTWaveProvider_old(IWaveProvider sourceWaveProvider, AudioEffect effect)
        {
            this.sourceWaveProvider = sourceWaveProvider;
            this.effect = effect;
            this.effect.Open();

            // Test effect
            //var numChannels = 2;
            //UInt32 blockSize = 1024;
            //var inSamples = new float[numChannels][];
            //inSamples[0] = new float[blockSize];
            //inSamples[1] = new float[blockSize];

            //var outSamples = new float[numChannels][];
            //outSamples[0] = new float[blockSize];
            //outSamples[1] = new float[blockSize];

            //this.effect.VstProcessReplacing(inSamples, outSamples, blockSize);

            this.provider = new Pcm16BitToSampleProvider(this.sourceWaveProvider);
            //var bp = new BufferedWaveProvider();
            //var provider2 = new SampleToWaveProvider16(
            //provider2.

            //provider2.Read()
        }

        public static VSTWaveProvider_old Create(IWaveProvider sourceWaveProvider, string filePath)
        {
            var effect = AudioEffect.Create(filePath);
            if (effect == null)
            {
                return null;
            }

            Console.WriteLine($"Added {effect.UniqueID}");

            return new VSTWaveProvider_old(sourceWaveProvider, effect);
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            var bin = new float[count / sizeof(float)];
            var bout = new float[count / sizeof(float)];
            var gg = provider.Read(bin, 0, count / sizeof(float));

            int read = 0;
            //var read = this.sourceWaveProvider.Read(buffer, offset, count);
            //var sp = this.sourceWaveProvider.ToSampleProvider();
            
            if (count > 0 && !this.isWriterDisposed)
            {
                //this.effect.VstProcessReplacing(new byte[2][] { buffer, buffer }, new byte[2][] { buffer, buffer }, (UInt32)count/sizeof(float));
                this.effect.VstProcessReplacing(new float[2][] { bin, bin }, new float[2][] { bout, bout }, (UInt32)count / sizeof(float));

                read = count;

                //writer.Write(buffer, offset, read);
            }
            if (count == 0)
            {
                Dispose(); // auto-dispose in case users forget
            }
            return read;
        }

        public WaveFormat WaveFormat { get { return sourceWaveProvider.WaveFormat; } }

        public void Dispose()
        {
            if (!this.isWriterDisposed)
            {
                this.isWriterDisposed = true;
                this.effect.Close();
                //writer.Dispose();
            }
        }
    }
}
