using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.Storage;
using Windows.Storage.Pickers;
using Friday.Visualization.DSP;
using ManagedBass;
using Prism.Commands;
using Prism.Mvvm;

namespace Friday.Core
{
    public class BassAudioPlayer : BindableBase, IAudioPlayer
    {
        private int _handle;

        public TimeSpan Duration { get; set; }
        public TimeSpan Position { get; set; }
        public double PlaybackSpeed { get; set; }
        public double Volume { get; set; }
        public float CurrentVolumePeek { get; set; }
        public ICommand PlayCommand { get; }
        public ICommand PauseCommand { get; }
        public IStorageFile CurrentPlayingFile { get; set; }

        private bool _isPlaying;

        public bool IsPlaying
        {
            get { return _isPlaying; }
            private set { SetProperty(ref _isPlaying, value); }
        }


        public BassAudioPlayer()
        {
            PlayCommand = new DelegateCommand(async () => await Play());
        }

        public async Task Play()
        {
            if (CurrentPlayingFile == null) return;

            var filePath = CurrentPlayingFile.Path;
            await Task.Run(() =>
            {
                Bass.Start();

                Bass.Init();

                _handle = Bass.CreateStream(filePath, 0, 0, BassFlags.Float);

                Bass.ChannelPlay(_handle);
            });

            IsPlaying = true;
        }



        public bool GetFftData(float[] fftDataBuffer)
        {
            return Bass.ChannelGetData(_handle, fftDataBuffer, (int)(DataFlags.Available|DataFlags.FFT2048)) > 0;
        }

        public int GetFftFrequencyIndex(int frequency)
        {
            return FFTFrequency2Index(frequency, (int)FftSize.Fft2048, 44100);
        }


        /// <summary>
        /// Returns the index of a specific frequency for FFT data.
        /// </summary>
        /// <param name="frequency">The frequency (in Hz) for which to get the index.</param>
        /// <param name="length">The FFT data length (e.g. 4096 for BASS_DATA_FFT4096).</param>
        /// <param name="samplerate">The sampling rate of the device or stream (e.g. 44100).</param>
        /// <returns>The index within the FFT data array (max. to length/2 -1).</returns>
        /// <remarks>Example: If the stream is 44100Hz, then 16500Hz will be around bin 191 of a 512 sample FFT (512*16500/44100).
        /// Or, if you are using BASS_DATA_FFT4096 on a stream with a sample rate of 44100 a tone at 540Hz will be at: 540*4096/44100 = 50 (so a bit of the energy will be in fft[51], but mostly in fft[50]).
        /// </remarks>
        public static int FFTFrequency2Index(int frequency, int length, int samplerate)
        {
            int num = (int)Math.Round((double)length * (double)frequency / (double)samplerate);
            if (num > length / 2 - 1)
                num = length / 2 - 1;
            return num;
        }

        /// <summary>
        /// Returns the frequency of a specific index in FFT data.
        /// </summary>
        /// <param name="index">The index within the FFT data array (half the requested).</param>
        /// <param name="length">The FFT data length (e.g. 4096 for BASS_DATA_FFT4096).</param>
        /// <param name="samplerate">The sampling rate of the device or stream (e.g. 44100).</param>
        /// <returns>The frequency (in Hz) which is represented by the index.</returns>
        /// <remarks>Example: If the stream is 44100Hz, then bin 191 of a 512 sample FFT (191*44100/512) will represent 16451Hz.
        /// Or, if you are using BASS_DATA_FFT4096 on a stream with a sample rate of 44100 an index of 50 will represent a tone of 538Hz: 50*44100/4096 = 50.
        /// </remarks>
        public static int FFTIndex2Frequency(int index, int length, int samplerate)
        {
            return (int)Math.Round((double)index * (double)samplerate / (double)length);
        }
    }
}
