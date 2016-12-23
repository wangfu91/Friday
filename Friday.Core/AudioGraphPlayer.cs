using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.ApplicationModel.Core;
using Windows.Devices.Enumeration;
using Windows.Media;
using Windows.Media.Audio;
using Windows.Media.Render;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Friday.Visualization.DSP;
using Prism.Commands;
using Prism.Mvvm;

namespace Friday.Core
{
    public class AudioGraphPlayer : BindableBase, IAudioPlayer
    {
        #region Fields
        private AudioGraph _audioGraph;
        private DeviceInformation _selectedDevice;
        private AudioFileInputNode _fileInputNode;
        private AudioDeviceOutputNode _deviceOutputNode;
        private AudioFrameOutputNode _frameOutputNode;
        private IStorageFile _currentPlayingFile;
        private TimeSpan _duration = TimeSpan.Zero;
        private TimeSpan _position = TimeSpan.Zero;
        private double _playbackSpeed = 100;
        private double _volume = 100;
        private float _currentVolumePeek;
        private bool _updatingPosition;
        private string _diagnosticsInfo;
        private bool _isPlaying;
        private FftProvider _fftProvider;

        #endregion


        #region Properties

        public ObservableCollection<DeviceInformation> Devices => new ObservableCollection<DeviceInformation>();

        public DeviceInformation SelectedDevice
        {
            get { return _selectedDevice; }
            set { SetProperty(ref _selectedDevice, value); }
        }

        public TimeSpan Duration
        {
            get { return _duration; }
            set { SetProperty(ref _duration, value); }
        }

        public double PlaybackSpeed
        {
            get { return _playbackSpeed; }
            set { SetProperty(ref _playbackSpeed, value); }
        }


        public double Volume
        {
            get { return _volume; }
            set
            {
                if (SetProperty(ref _volume, value))
                {
                    if (_fileInputNode != null)
                        _fileInputNode.OutgoingGain = value / 100.0;
                }
            }
        }

        public float CurrentVolumePeek
        {
            get { return _currentVolumePeek; }
            set { SetProperty(ref _currentVolumePeek, value); }
        }


        public TimeSpan Position
        {
            get { return _position; }
            set
            {
                if (SetProperty(ref _position, value))
                    if (!_updatingPosition)
                        _fileInputNode?.Seek(_position);
            }
        }

        public IStorageFile CurrentPlayingFile
        {
            get { return _currentPlayingFile; }
            set
            {
                if (SetProperty(ref _currentPlayingFile, value))
                {
                    _audioGraph?.Stop();
                    _fileInputNode = null;
                }
            }
        }


        public string DiagnosticsInfo
        {
            get { return _diagnosticsInfo; }
            private set { SetProperty(ref _diagnosticsInfo, value); }
        }


        public bool IsPlaying
        {
            get { return _isPlaying; }
            private set { SetProperty(ref _isPlaying, value); }
        }

        #endregion


        #region Commands

        public ICommand PlayCommand { get; }

        public ICommand PauseCommand { get; }

        public ICommand StopCommand { get; }



        #endregion


        #region ctor

        public AudioGraphPlayer()
        {
            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            timer.Start();
            timer.Tick += TimerOnTick;

            PlayCommand = DelegateCommand.FromAsyncHandler(Play);
            PauseCommand = new DelegateCommand(Pause);
            StopCommand = new DelegateCommand(Stop);
        }

        #endregion


        #region Methods

        private async Task Play()
        {
            if (IsPlaying)
            {
                Pause();
                return; 
            }

            if (_audioGraph == null)
            {
                var settings = new AudioGraphSettings(AudioRenderCategory.Media)
                {
                    PrimaryRenderDevice = SelectedDevice
                };

                var createResult = await AudioGraph.CreateAsync(settings);
                if (createResult.Status != AudioGraphCreationStatus.Success) return;

                _audioGraph = createResult.Graph;
                _audioGraph.UnrecoverableErrorOccurred += OnAudioGraphError;
            }

            if (_deviceOutputNode == null)
            {
                var deviceResult = await _audioGraph.CreateDeviceOutputNodeAsync();
                if (deviceResult.Status != AudioDeviceNodeCreationStatus.Success) return;
                _deviceOutputNode = deviceResult.DeviceOutputNode;
            }

            if (_frameOutputNode == null)
            {
                _frameOutputNode = _audioGraph.CreateFrameOutputNode();
                _audioGraph.QuantumProcessed += GraphOnQuantumProcessed;
            }

            if (_fileInputNode == null)
            {
                if (CurrentPlayingFile == null) return;

                var fileResult = await _audioGraph.CreateFileInputNodeAsync(CurrentPlayingFile);
                if (fileResult.Status != AudioFileNodeCreationStatus.Success) return;
                _fileInputNode = fileResult.FileInputNode;
                _fileInputNode.AddOutgoingConnection(_deviceOutputNode);
                _fileInputNode.AddOutgoingConnection(_frameOutputNode);
                Duration = _fileInputNode.Duration;
                _fileInputNode.PlaybackSpeedFactor = PlaybackSpeed / 100.0;
                _fileInputNode.OutgoingGain = Volume / 100.0;
                _fileInputNode.FileCompleted += FileInputNodeOnFileCompleted;
            }

            Debug.WriteLine($" CompletedQuantumCount: {_audioGraph.CompletedQuantumCount}");
            Debug.WriteLine($"SamplesPerQuantum: {_audioGraph.SamplesPerQuantum}");
            Debug.WriteLine($"LatencyInSamples: {_audioGraph.LatencyInSamples}");
            var channelCount = (int)_audioGraph.EncodingProperties.ChannelCount;
            _fftProvider = new FftProvider(channelCount, FftSize.Fft2048);
            _audioGraph.Start();
            IsPlaying = true;
        }

        private async void GraphOnQuantumProcessed(AudioGraph sender, object args)
        {
            var frame = _frameOutputNode.GetFrame();
            ProcessFrameOutput(frame);
            //Debug.WriteLine($"\t peek = {peek:R}");
            //await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
            //    CoreDispatcherPriority.Normal,
            //    () => CurrentVolumePeek = peek * 100);
        }

        private unsafe void ProcessFrameOutput(AudioFrame frame)
        {
            using (var buffer = frame.LockBuffer(AudioBufferAccessMode.Read))
            using (var reference = buffer.CreateReference())
            {
                // Get hold of the buffer pointer.
                byte* dataInBytes;
                uint capacityInBytes;
                ((IMemoryBufferByteAccess)reference).GetBuffer(
                    out dataInBytes, out capacityInBytes);
                var dataInFloat = (float*)dataInBytes;
                for (var n = 0; n + 1 < _audioGraph.SamplesPerQuantum; n++)
                {
                    _fftProvider.Add(dataInFloat[n], dataInFloat[n++]);
                    //max = Math.Max(Math.Abs(dataInFloat[n]), max);
                }
            }
        }


        public async Task InitializeAsync()
        {
            var outputDevices = await DeviceInformation.FindAllAsync(DeviceClass.AudioRender);
            foreach (var device in outputDevices.Where(d => d.IsEnabled))
            {
                Devices.Add(device);
            }
            SelectedDevice = Devices.FirstOrDefault(d => d.IsDefault);
        }

        private void Pause()
        {
            _audioGraph?.Stop();
            IsPlaying = false;
        }

        private void Stop()
        {
            // Causes all nodes in the graph to discard any data currently in their audio buffers.
            _audioGraph?.ResetAllNodes();
            IsPlaying = false;
        }

        #endregion


        #region Event handlers

        private void TimerOnTick(object sender, object e)
        {
            try
            {
                _updatingPosition = true;
                if (_fileInputNode != null)
                {
                    Position = _fileInputNode.Position;
                }
            }
            finally
            {
                _updatingPosition = false;
            }
        }

        private async void FileInputNodeOnFileCompleted(AudioFileInputNode sender, object args)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                CoreDispatcherPriority.Normal,
                () =>
                {
                    _audioGraph.Stop();
                    Position = TimeSpan.Zero;
                });
        }

        private async void OnAudioGraphError(AudioGraph sender, AudioGraphUnrecoverableErrorOccurredEventArgs args)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                CoreDispatcherPriority.Normal,
                () => DiagnosticsInfo = $"Audio Graph Error: {args.Error}\r\n");
        }

        public bool GetFftData(float[] fftDataBuffer)
        {
            if (_fftProvider == null) return false;
            return _fftProvider.GetFftData(fftDataBuffer);
        }

        public int GetFftFrequencyIndex(int frequency)
        {
            if (_fftProvider == null) return 0;

            var fftSize = (int)_fftProvider.FftSize;
            var f = _audioGraph.EncodingProperties.SampleRate / 2.0;
            return (int)((frequency / f) * (fftSize / 2.0));
        }

        #endregion
    }
}
