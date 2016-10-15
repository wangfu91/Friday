using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Devices.Enumeration;
using Windows.Media.Audio;
using Windows.Media.Render;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Prism.Commands;
using Prism.Mvvm;

namespace Friday.Core
{
    public class AudioGraphPlayer : BindableBase
    {
        #region Fields
        private AudioGraph _audioGraph;
        private DeviceInformation _selectedDevice;
        private AudioFileInputNode _fileInputNode;
        private AudioDeviceOutputNode _deviceOutputNode;

        private IStorageFile _currentPlayingFile;
        private TimeSpan _duration = TimeSpan.Zero;
        private TimeSpan _position = TimeSpan.Zero;
        private double _playbackSpeed = 100;
        private double _volume = 100;

        private bool _updatingPosition;
        private string _diagnostics;

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

        public IStorageFile CurrentPalyingFile
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


        public string Diagnostics
        {
            get { return _diagnostics; }
            private set { SetProperty(ref _diagnostics, value); }
        }

        #endregion


        #region Commands

        public DelegateCommand PlayCommand { get; }

        public DelegateCommand StopCommand { get; }

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
            StopCommand = new DelegateCommand(Stop);
        }

        #endregion


        #region Methods

        private async Task Play()
        {
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

            if (_fileInputNode == null)
            {
                if (CurrentPalyingFile == null) return;

                var fileResult = await _audioGraph.CreateFileInputNodeAsync(CurrentPalyingFile);
                if (fileResult.Status != AudioFileNodeCreationStatus.Success) return;
                _fileInputNode = fileResult.FileInputNode;
                _fileInputNode.AddOutgoingConnection(_deviceOutputNode);
                Duration = _fileInputNode.Duration;
                _fileInputNode.PlaybackSpeedFactor = PlaybackSpeed / 100.0;
                _fileInputNode.OutgoingGain = Volume / 100.0;
                _fileInputNode.FileCompleted += FileInputNodeOnFileCompleted;
            }

            _audioGraph.Start();
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

        private void Stop()
        {
            _audioGraph?.Stop();
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
                () => Diagnostics = $"Audio Graph Error: {args.Error}\r\n");
        }

        #endregion


    }
}
