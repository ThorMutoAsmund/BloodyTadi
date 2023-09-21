using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;

namespace DLLViewer
{
    class VstSampleProvider : ISampleProvider, IDisposable
    {
        private ISampleProvider provider;
        private bool isWriterDisposed;
        private AudioEffect effect;

        public WaveFormat WaveFormat => WaveFormat.CreateIeeeFloatWaveFormat(this.provider.WaveFormat.SampleRate, 2);

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
            this.effect = effect;
            this.effect.Open();

            this.provider = sourceWaveProvider.ToSampleProvider();
        }

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
                if (this.provider.WaveFormat.Channels == 1)
                {
                    read = this.provider.Read(buffer, 0, count/2);

                    this.binR = buffer;
                }
                else if (this.provider.WaveFormat.Channels == 2)
                {
                    read = this.provider.Read(buffer, 0, count);

                    Array.Resize(ref this.binR, count / 2);
                    for (int i = 0; i < count; i += 2)
                    {
                        buffer[i / 2] = buffer[i];
                        this.binR[i / 2] = buffer[i + 1];
                    }
                }

                Array.Resize(ref this.boutL, count / 2);                
                Array.Resize(ref this.boutR, count / 2);

                this.effect.VstProcessReplacing(buffer, this.binR, this.boutL, this.boutR, (UInt32)(count / 2));

                for (int i = 0; i < count; i += 2)
                {
                    buffer[i] = this.boutL[i / 2];
                    buffer[i+1] = this.boutR[i / 2];
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
}
