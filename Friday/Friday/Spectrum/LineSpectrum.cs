using System;
using Friday.Dsp;
using SkiaSharp;
using Xamarin.Forms;

namespace Friday.Spectrum
{
    public class LineSpectrum : SpectrumBase
    {
        private readonly SKPaint _greenFillPaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Color = SKColors.Green
        };

        private int _barCount;
        private double _barSpacing;
        private double _barWidth;
        private Size _currentSize;

        public LineSpectrum(FftSize fftSize)
        {
            FftSize = fftSize;
        }

        public double BarWidth => _barWidth;

        public double BarSpacing
        {
            get => _barSpacing;
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value));
                _barSpacing = value;
                UpdateFrequencyMapping();

                RaisePropertyChanged("BarSpacing");
                RaisePropertyChanged("BarWidth");
            }
        }

        public int BarCount
        {
            get => _barCount;
            set
            {
                if (value <= 0)
                    throw new ArgumentOutOfRangeException(nameof(value));
                _barCount = value;
                SpectrumResolution = value;
                UpdateFrequencyMapping();

                RaisePropertyChanged("BarCount");
                RaisePropertyChanged("BarWidth");
            }
        }

        public Size CurrentSize
        {
            get => _currentSize;
            protected set
            {
                _currentSize = value;
                RaisePropertyChanged("CurrentSize");
            }
        }

        public void CreateSpectrumLine(SKSurface surface, Size size)
        {
            var canvas = surface.Canvas;
            if (!UpdateFrequencyMappingIfNessesary(size))
                return;

            var fftBuffer = new float[(int)FftSize];

            //get the fft result from the spectrum provider
            if (SpectrumProvider.GetFftData(fftBuffer))
            {
                CreateSpectrumLineInternal(canvas, fftBuffer, size);
            }
        }

        private void CreateSpectrumLineInternal(SKCanvas canvas, float[] fftBuffer, Size size)
        {
            var height = (float)size.Height;
            var width = (float)size.Width;

            canvas.Clear();

            canvas.Translate(0, height);

            //prepare the fft result for rendering 
            SpectrumPointData[] spectrumPoints = CalculateSpectrumPoints(height * 0.4, fftBuffer);

            //connect the calculated points with lines
            for (int i = 0; i < spectrumPoints.Length; i++)
            {
                SpectrumPointData p = spectrumPoints[i];
                int barIndex = p.SpectrumPointIndex;

                var xCoord = (float)(BarSpacing * (barIndex + 1) + (_barWidth * barIndex) + _barWidth / 2);
                _greenFillPaint.StrokeWidth = (float)_barWidth;
                canvas.DrawLine(xCoord, 0, xCoord, -(float)p.Value, _greenFillPaint);
            }
        }

        public override void UpdateFrequencyMapping()
        {
            _barWidth = Math.Max(((_currentSize.Width - (BarSpacing * (BarCount + 1))) / BarCount), 0.00001);
            base.UpdateFrequencyMapping();
        }

        public bool UpdateFrequencyMappingIfNessesary(Size newSize)
        {
            if (newSize != CurrentSize)
            {
                CurrentSize = newSize;
                UpdateFrequencyMapping();
            }

            return newSize.Width > 0 && newSize.Height > 0;
        }

    }
}
