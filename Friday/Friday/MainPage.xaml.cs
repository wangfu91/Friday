using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Friday.AudioPlayer;
using Friday.Dsp;
using Friday.Spectrum;
using Plugin.FilePicker;
using Plugin.FilePicker.Abstractions;
using SkiaSharp.Views.Forms;
using Xamarin.Forms;

namespace Friday
{
    public partial class MainPage : ContentPage
    {
        private LineSpectrum _lineSpectrum;

        private IAudioProvider _audioProvider;

        private readonly FftSize fftSize = FftSize.Fft4096;
        public MainPage()
        {
            InitializeComponent();

            _audioProvider = new BassAudioPlayer();

            Device.StartTimer(TimeSpan.FromSeconds(1f / 60), () =>
            {
                canvasView.InvalidateSurface();
                return true;
            });
        }

        private async void Button_Clicked(object sender, EventArgs e)
        {
            await OnLoadAsync();
        }

        private async Task OnLoadAsync()
        {
            var fileData = await CrossFilePicker.Current.PickFile();

            if (fileData == null) return;

            fileNameLabel.Text = fileData.FileName;

            _audioProvider.CurrentPlayingFile = fileData.FilePath;

            if (_audioProvider.IsPlaying)
                _audioProvider.Stop();

            await _audioProvider.Play();

            //linespectrum and voiceprint3dspectrum used for rendering some fft data
            //in oder to get some fft data, set the previously created spectrumprovider 
            _lineSpectrum = new LineSpectrum(fftSize)
            {
                SpectrumProvider = _audioProvider,
                UseAverage = true,
                BarCount = 200,
                BarSpacing = 1,
                IsXLogScale = false,
                ScalingStrategy = ScalingStrategy.Sqrt,
                MinimumFrequency = 20,
                MaximumFrequency = 20000
            };
        }

        private void canvasView_PaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            if (_lineSpectrum != null && _audioProvider.IsPlaying)
            {
                var size = new Size(e.Info.Width, e.Info.Height);
                _lineSpectrum.CreateSpectrumLine(e.Surface, size);
            }
        }

    }


}
