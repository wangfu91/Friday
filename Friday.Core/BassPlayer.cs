using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.Storage;
using Friday.Visualization;
using Prism.Mvvm;
using Prism.Commands;
using ManagedBass;

namespace Friday.Core
{
    public class BassPlayer : BindableBase, IAudioPlayer
    {

        #region DllImports
        [DllImport("bass.dll")]
        static extern bool BASS_SetConfig(int config, int newValue);
        const int BASS_CONFIG_DEV_BUFFER = 27;
        #endregion

        #region Fields
        private bool _isPlaying;
        private int _activeStreamHandle;
        private double _volume;
        private double _channelLength;
        private double _channelPosition;
        private IStorageFile _currentPlayingFile;

        #endregion


        #region ctor
        public BassPlayer()
        {
            PlayCommand = new DelegateCommand(Play);
            PauseCommand = new DelegateCommand(Pause);
            StopCommand = new DelegateCommand(Stop);

            Init();
        }

        #endregion


        #region Properties

        public bool IsPlaying
        {
            get { return _isPlaying; }
            set { SetProperty(ref _isPlaying, value); }
        }

        public double Volume
        {
            get { return _volume; }
            set { SetProperty(ref _volume, value); }
        }


        public double ChannelLength
        {
            get { return _channelLength; }
            set { SetProperty(ref _channelLength, value); }
        }

        public double ChannelPosition
        {
            get { return _channelPosition; }
            set { SetProperty(ref _channelPosition, value); }
        }

        public IStorageFile CurrentPlayingFile
        {
            get { return _currentPlayingFile; }
            set { SetProperty(ref _currentPlayingFile, value); }
        }

        #region Commands

        public ICommand PlayCommand { get; private set; }

        public ICommand PauseCommand { get; private set; }

        public ICommand StopCommand { get; private set; }

        #endregion

        #endregion



        #region Methods


        private void Init()
        {
            BASS_SetConfig(BASS_CONFIG_DEV_BUFFER, 230);
            Bass.UpdatePeriod = 1000;
            Bass.Init();
        }


        public void LoadFile(string file)
        {
            Stop();
            _activeStreamHandle = Bass.CreateStream(file);

        }


        public void Play()
        {
            IsPlaying = true;
            Bass.ChannelPlay(_activeStreamHandle);
            Bass.Start();

        }

        public void Pause()
        {
            IsPlaying = false;
        }

        public void Stop()
        {
            IsPlaying = false;
        }

        #endregion



        #region ISpectrumPlayer Implements

        public bool GetFFTData(float[] fftDataBuffer)
        {
            return Bass.ChannelGetData(_activeStreamHandle, fftDataBuffer, (int)(0 | -2147483645)) > 0;
        }

        public int GetFFTFrequencyIndex(int frequency)
        {
            var fftDataSize = 2048;
            var sampleFrequency = 44100;
            var result = FFTFrequency2Index(frequency, fftDataSize, sampleFrequency);
            return result;
        }


        // Un4seen.Bass.Utils
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
            {
                num = length / 2 - 1;
            }
            return num;
        }

        #endregion
    }
}
