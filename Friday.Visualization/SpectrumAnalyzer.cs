using System;
using System.Collections.Generic;
using System.ComponentModel;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Shapes;
using Friday.Visualization.DSP;

namespace Friday.Visualization
{
    /// <summary>
    /// A spectrum analyzer control for visualizing audio level and frequency data.
    /// </summary>
    [TemplatePart(Name = "PART_SpectrumCanvas", Type = typeof(Canvas))]
    public class SpectrumAnalyzer : Control
    {
        #region Fields
        private readonly DispatcherTimer _animationTimer;
        private Canvas _spectrumCanvas;
        private ISpectrumPlayer _soundPlayer;
        private readonly List<Shape> _barShapes = new List<Shape>();
        private readonly List<Shape> _peakShapes = new List<Shape>();
        private float[] _channelData = new float[2048];
        private float[] _channelPeakData;
        private double _barWidth = 1;
        private int _maximumFrequencyIndex = 2047;
        private int _minimumFrequencyIndex;
        private int[] _barIndexMax;
        private int[] _barLogScaleIndexMax;
        #endregion

        #region Constants
        private const int ScaleFactorLinear = 9;
        private const int ScaleFactorSqr = 2;
        private const double MinDbValue = -90;
        private const double MaxDbValue = 0;
        private const double DbScale = (MaxDbValue - MinDbValue);
        private const int DefaultUpdateInterval = 25;
        #endregion

        #region Dependency Properties

        #region MaximumFrequency
        /// <summary>
        /// Identifies the <see cref="MaximumFrequency" /> dependency property. 
        /// </summary>
        public static readonly DependencyProperty MaximumFrequencyProperty = DependencyProperty.Register(
            "MaximumFrequency", typeof(int), typeof(SpectrumAnalyzer), new PropertyMetadata(20000, OnMaximumFrequencyChanged));

        private static void OnMaximumFrequencyChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            var spectrumAnalyzer = o as SpectrumAnalyzer;
            spectrumAnalyzer?.OnMaximumFrequencyChanged((int)e.OldValue, (int)e.NewValue);
        }

        /// <summary>
        /// Called after the <see cref="MaximumFrequency"/> value has changed.
        /// </summary>
        /// <param name="oldValue">The previous value of <see cref="MaximumFrequency"/></param>
        /// <param name="newValue">The new value of <see cref="MaximumFrequency"/></param>
        protected virtual void OnMaximumFrequencyChanged(int oldValue, int newValue)
        {
            UpdateBarLayout();
        }

        /// <summary>
        /// Gets or sets the maximum display frequency (right side) for the spectrum analyzer.
        /// </summary>
        /// <remarks>In usual practice, this value should be somewhere between 0 and half of the maximum sample rate. If using
        /// the maximum sample rate, this would be roughly 22000.</remarks>
        public int MaximumFrequency
        {
            // IMPORTANT: To maintain parity between setting a property in XAML and procedural code, do not touch the getter and setter inside this dependency property!
            get
            {
                return (int)GetValue(MaximumFrequencyProperty);
            }
            set
            {
                SetValue(MaximumFrequencyProperty, value);
            }
        }
        #endregion

        #region Minimum Frequency
        /// <summary>
        /// Identifies the <see cref="MinimumFrequency" /> dependency property. 
        /// </summary>
        public static readonly DependencyProperty MinimumFrequencyProperty = DependencyProperty.Register(
            "MinimumFrequency", typeof(int), typeof(SpectrumAnalyzer), new PropertyMetadata(20, OnMinimumFrequencyChanged));

        private static void OnMinimumFrequencyChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            var spectrumAnalyzer = o as SpectrumAnalyzer;
            spectrumAnalyzer?.OnMinimumFrequencyChanged((int)e.OldValue, (int)e.NewValue);
        }

        /// <summary>
        /// Called after the <see cref="MinimumFrequency"/> value has changed.
        /// </summary>
        /// <param name="oldValue">The previous value of <see cref="MinimumFrequency"/></param>
        /// <param name="newValue">The new value of <see cref="MinimumFrequency"/></param>
        protected virtual void OnMinimumFrequencyChanged(int oldValue, int newValue)
        {
            UpdateBarLayout();
        }

        /// <summary>
        /// Gets or sets the minimum display frequency (left side) for the spectrum analyzer.
        /// </summary>
        public int MinimumFrequency
        {
            // IMPORTANT: To maintain parity between setting a property in XAML and procedural code, do not touch the getter and setter inside this dependency property!
            get
            {
                return (int)GetValue(MinimumFrequencyProperty);
            }
            set
            {
                SetValue(MinimumFrequencyProperty, value);
            }
        }

        #endregion

        #region BarCount
        /// <summary>
        /// Identifies the <see cref="BarCount" /> dependency property. 
        /// </summary>
        public static readonly DependencyProperty BarCountProperty = DependencyProperty.Register(
            "BarCount", typeof(int), typeof(SpectrumAnalyzer), new PropertyMetadata(32, OnBarCountChanged));

        private static void OnBarCountChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            var spectrumAnalyzer = o as SpectrumAnalyzer;
            spectrumAnalyzer?.OnBarCountChanged((int)e.OldValue, (int)e.NewValue);
        }

        /// <summary>
        /// Called after the <see cref="BarCount"/> value has changed.
        /// </summary>
        /// <param name="oldValue">The previous value of <see cref="BarCount"/></param>
        /// <param name="newValue">The new value of <see cref="BarCount"/></param>
        protected virtual void OnBarCountChanged(int oldValue, int newValue)
        {
            UpdateBarLayout();
        }

        /// <summary>
        /// Gets or sets the number of bars to show on the sprectrum analyzer.
        /// </summary>
        /// <remarks>A bar's width can be a minimum of 1 pixel. If the BarSpacing and BarCount property result
        /// in the bars being wider than the chart itself, the BarCount will automatically scale down.</remarks>
        public int BarCount
        {
            // IMPORTANT: To maintain parity between setting a property in XAML and procedural code, do not touch the getter and setter inside this dependency property!
            get
            {
                return (int)GetValue(BarCountProperty);
            }
            set
            {
                SetValue(BarCountProperty, value);
            }
        }
        #endregion

        #region BarSpacing
        /// <summary>
        /// Identifies the <see cref="BarSpacing" /> dependency property. 
        /// </summary>
        public static readonly DependencyProperty BarSpacingProperty = DependencyProperty.Register(
            "BarSpacing", typeof(double), typeof(SpectrumAnalyzer), new PropertyMetadata(5.0d, OnBarSpacingChanged));

        private static void OnBarSpacingChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            var spectrumAnalyzer = o as SpectrumAnalyzer;
            spectrumAnalyzer?.OnBarSpacingChanged((double)e.OldValue, (double)e.NewValue);
        }

        /// <summary>
        /// Called after the <see cref="BarSpacing"/> value has changed.
        /// </summary>
        /// <param name="oldValue">The previous value of <see cref="BarSpacing"/></param>
        /// <param name="newValue">The new value of <see cref="BarSpacing"/></param>
        protected virtual void OnBarSpacingChanged(double oldValue, double newValue)
        {
            UpdateBarLayout();
        }

        /// <summary>
        /// Gets or sets the spacing between the bars.
        /// </summary>
        public double BarSpacing
        {
            // IMPORTANT: To maintain parity between setting a property in XAML and procedural code, do not touch the getter and setter inside this dependency property!
            get
            {
                return (double)GetValue(BarSpacingProperty);
            }
            set
            {
                SetValue(BarSpacingProperty, value);
            }
        }
        #endregion

        #region PeakFallDelay
        /// <summary>
        /// Identifies the <see cref="PeakFallDelay" /> dependency property. 
        /// </summary>
        public static readonly DependencyProperty PeakFallDelayProperty = DependencyProperty.Register(
            "PeakFallDelay", typeof(int), typeof(SpectrumAnalyzer), new PropertyMetadata(10, OnPeakFallDelayChanged));

        private static void OnPeakFallDelayChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            var spectrumAnalyzer = o as SpectrumAnalyzer;
            spectrumAnalyzer?.OnPeakFallDelayChanged((int)e.OldValue, (int)e.NewValue);
        }

        /// <summary>
        /// Called after the <see cref="PeakFallDelay"/> value has changed.
        /// </summary>
        /// <param name="oldValue">The previous value of <see cref="PeakFallDelay"/></param>
        /// <param name="newValue">The new value of <see cref="PeakFallDelay"/></param>
        protected virtual void OnPeakFallDelayChanged(int oldValue, int newValue)
        {

        }

        /// <summary>
        /// Gets or sets the delay factor for the peaks falling.
        /// </summary>
        /// <remarks>
        /// The delay is relative to the refresh rate of the chart.
        /// </remarks>
        public int PeakFallDelay
        {
            // IMPORTANT: To maintain parity between setting a property in XAML and procedural code, do not touch the getter and setter inside this dependency property!
            get
            {
                return (int)GetValue(PeakFallDelayProperty);
            }
            set
            {
                SetValue(PeakFallDelayProperty, value);
            }
        }
        #endregion

        #region IsFrequencyScaleLinear
        /// <summary>
        /// Identifies the <see cref="IsFrequencyScaleLinear" /> dependency property. 
        /// </summary>
        public static readonly DependencyProperty IsFrequencyScaleLinearProperty = DependencyProperty.Register(
            "IsFrequencyScaleLinear", typeof(bool), typeof(SpectrumAnalyzer), new PropertyMetadata(false, OnIsFrequencyScaleLinearChanged));

        private static void OnIsFrequencyScaleLinearChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            var spectrumAnalyzer = o as SpectrumAnalyzer;
            spectrumAnalyzer?.OnIsFrequencyScaleLinearChanged((bool)e.OldValue, (bool)e.NewValue);
        }

        /// <summary>
        /// Called after the <see cref="IsFrequencyScaleLinear"/> value has changed.
        /// </summary>
        /// <param name="oldValue">The previous value of <see cref="IsFrequencyScaleLinear"/></param>
        /// <param name="newValue">The new value of <see cref="IsFrequencyScaleLinear"/></param>
        protected virtual void OnIsFrequencyScaleLinearChanged(bool oldValue, bool newValue)
        {
            UpdateBarLayout();
        }

        /// <summary>
        /// Gets or sets a value indicating whether the bars are layed out on a linear scale horizontally.
        /// </summary>
        /// <remarks>
        /// If true, the bars will represent frequency buckets on a linear scale (making them all
        /// have equal band widths on the frequency scale). Otherwise, the bars will be layed out
        /// on a logrithmic scale, with each bar having a larger bandwidth than the one previous.
        /// </remarks>
        public bool IsFrequencyScaleLinear
        {
            // IMPORTANT: To maintain parity between setting a property in XAML and procedural code, do not touch the getter and setter inside this dependency property!
            get
            {
                return (bool)GetValue(IsFrequencyScaleLinearProperty);
            }
            set
            {
                SetValue(IsFrequencyScaleLinearProperty, value);
            }
        }
        #endregion

        #region BarHeightScaling
        /// <summary>
        /// Identifies the <see cref="BarHeightScaling" /> dependency property. 
        /// </summary>
        public static readonly DependencyProperty BarHeightScalingProperty = DependencyProperty.Register(
            "BarHeightScaling", typeof(BarHeightScalingStyles), typeof(SpectrumAnalyzer), new PropertyMetadata(BarHeightScalingStyles.Decibel, OnBarHeightScalingChanged));

        private static void OnBarHeightScalingChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            var spectrumAnalyzer = o as SpectrumAnalyzer;
            spectrumAnalyzer?.OnBarHeightScalingChanged((BarHeightScalingStyles)e.OldValue, (BarHeightScalingStyles)e.NewValue);
        }

        /// <summary>
        /// Called after the <see cref="BarHeightScaling"/> value has changed.
        /// </summary>
        /// <param name="oldValue">The previous value of <see cref="BarHeightScaling"/></param>
        /// <param name="newValue">The new value of <see cref="BarHeightScaling"/></param>
        protected virtual void OnBarHeightScalingChanged(BarHeightScalingStyles oldValue, BarHeightScalingStyles newValue)
        {

        }

        /// <summary>
        /// Gets or sets a value indicating to what scale the bar heights are drawn.
        /// </summary>
        public BarHeightScalingStyles BarHeightScaling
        {
            // IMPORTANT: To maintain parity between setting a property in XAML and procedural code, do not touch the getter and setter inside this dependency property!
            get
            {
                return (BarHeightScalingStyles)GetValue(BarHeightScalingProperty);
            }
            set
            {
                SetValue(BarHeightScalingProperty, value);
            }
        }
        #endregion

        #region AveragePeaks
        /// <summary>
        /// Identifies the <see cref="AveragePeaks" /> dependency property. 
        /// </summary>
        public static readonly DependencyProperty AveragePeaksProperty = DependencyProperty.Register(
            "AveragePeaks", typeof(bool), typeof(SpectrumAnalyzer), new PropertyMetadata(false, OnAveragePeaksChanged));

        private static void OnAveragePeaksChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            var spectrumAnalyzer = o as SpectrumAnalyzer;
            spectrumAnalyzer?.OnAveragePeaksChanged((bool)e.OldValue, (bool)e.NewValue);
        }

        /// <summary>
        /// Called after the <see cref="AveragePeaks"/> value has changed.
        /// </summary>
        /// <param name="oldValue">The previous value of <see cref="AveragePeaks"/></param>
        /// <param name="newValue">The new value of <see cref="AveragePeaks"/></param>
        protected virtual void OnAveragePeaksChanged(bool oldValue, bool newValue)
        {

        }

        /// <summary>
        /// Gets or sets a value indicating whether each bar's peak 
        /// value will be averaged with the previous bar's peak.
        /// This creates a smoothing effect on the bars.
        /// </summary>
        public bool AveragePeaks
        {
            // IMPORTANT: To maintain parity between setting a property in XAML and procedural code, do not touch the getter and setter inside this dependency property!
            get
            {
                return (bool)GetValue(AveragePeaksProperty);
            }
            set
            {
                SetValue(AveragePeaksProperty, value);
            }
        }
        #endregion

        #region BarStyle
        /// <summary>
        /// Identifies the <see cref="BarStyle" /> dependency property. 
        /// </summary>
        public static readonly DependencyProperty BarStyleProperty = DependencyProperty.Register(
            "BarStyle", typeof(Style), typeof(SpectrumAnalyzer), new PropertyMetadata(null, OnBarStyleChanged));

        private static void OnBarStyleChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            var spectrumAnalyzer = o as SpectrumAnalyzer;
            spectrumAnalyzer?.OnBarStyleChanged((Style)e.OldValue, (Style)e.NewValue);
        }

        /// <summary>
        /// Called after the <see cref="BarStyle"/> value has changed.
        /// </summary>
        /// <param name="oldValue">The previous value of <see cref="BarStyle"/></param>
        /// <param name="newValue">The new value of <see cref="BarStyle"/></param>
        protected virtual void OnBarStyleChanged(Style oldValue, Style newValue)
        {
            UpdateBarLayout();
        }

        /// <summary>
        /// Gets or sets a style with which to draw the bars on the spectrum analyzer.
        /// </summary>
        public Style BarStyle
        {
            // IMPORTANT: To maintain parity between setting a property in XAML and procedural code, do not touch the getter and setter inside this dependency property!
            get
            {
                return (Style)GetValue(BarStyleProperty);
            }
            set
            {
                SetValue(BarStyleProperty, value);
            }
        }
        #endregion

        #region PeakStyle
        /// <summary>
        /// Identifies the <see cref="PeakStyle" /> dependency property. 
        /// </summary>
        public static readonly DependencyProperty PeakStyleProperty = DependencyProperty.Register(
            "PeakStyle", typeof(Style), typeof(SpectrumAnalyzer), new PropertyMetadata(null, OnPeakStyleChanged));

        private static void OnPeakStyleChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            var spectrumAnalyzer = o as SpectrumAnalyzer;
            spectrumAnalyzer?.OnPeakStyleChanged((Style)e.OldValue, (Style)e.NewValue);
        }

        /// <summary>
        /// Called after the <see cref="PeakStyle"/> value has changed.
        /// </summary>
        /// <param name="oldValue">The previous value of <see cref="PeakStyle"/></param>
        /// <param name="newValue">The new value of <see cref="PeakStyle"/></param>
        protected virtual void OnPeakStyleChanged(Style oldValue, Style newValue)
        {
            UpdateBarLayout();
        }

        /// <summary>
        /// Gets or sets a style with which to draw the falling peaks on the spectrum analyzer.
        /// </summary>
        public Style PeakStyle
        {
            // IMPORTANT: To maintain parity between setting a property in XAML and procedural code, do not touch the getter and setter inside this dependency property!
            get
            {
                return (Style)GetValue(PeakStyleProperty);
            }
            set
            {
                SetValue(PeakStyleProperty, value);
            }
        }
        #endregion

        #region ActualBarWidth
        /// <summary>
        /// Identifies the <see cref="ActualBarWidth" /> dependency property. 
        /// </summary>
        public static readonly DependencyProperty ActualBarWidthProperty = DependencyProperty.Register(
            "ActualBarWidth", typeof(double), typeof(SpectrumAnalyzer), new PropertyMetadata(0.0d, OnActualBarWidthChanged));

        private static void OnActualBarWidthChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            var spectrumAnalyzer = o as SpectrumAnalyzer;
            spectrumAnalyzer?.OnActualBarWidthChanged((double)e.OldValue, (double)e.NewValue);
        }

        /// <summary>
        /// Called after the <see cref="ActualBarWidth"/> value has changed.
        /// </summary>
        /// <param name="oldValue">The previous value of <see cref="ActualBarWidth"/></param>
        /// <param name="newValue">The new value of <see cref="ActualBarWidth"/></param>
        protected virtual void OnActualBarWidthChanged(double oldValue, double newValue)
        {

        }

        /// <summary>
        /// Gets the actual width that the bars will be drawn at.
        /// </summary>
        public double ActualBarWidth
        {
            // IMPORTANT: To maintain parity between setting a property in XAML and procedural code, do not touch the getter and setter inside this dependency property!
            get
            {
                return (double)GetValue(ActualBarWidthProperty);
            }
            protected set
            {
                SetValue(ActualBarWidthProperty, value);
            }
        }
        #endregion

        #region RefreshRate
        /// <summary>
        /// Identifies the <see cref="RefreshInterval" /> dependency property. 
        /// </summary>
        public static readonly DependencyProperty RefreshIntervalProperty = DependencyProperty.Register(
            "RefreshInterval", typeof(int), typeof(SpectrumAnalyzer), new PropertyMetadata(DefaultUpdateInterval, OnRefreshIntervalChanged));

        private static void OnRefreshIntervalChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            var spectrumAnalyzer = o as SpectrumAnalyzer;
            spectrumAnalyzer?.OnRefreshIntervalChanged((int)e.OldValue, (int)e.NewValue);
        }

        /// <summary>
        /// Called after the <see cref="RefreshInterval"/> value has changed.
        /// </summary>
        /// <param name="oldValue">The previous value of <see cref="RefreshInterval"/></param>
        /// <param name="newValue">The new value of <see cref="RefreshInterval"/></param>
        protected virtual void OnRefreshIntervalChanged(int oldValue, int newValue)
        {
            _animationTimer.Interval = TimeSpan.FromMilliseconds(newValue);
        }

        /// <summary>
        /// Gets or sets the refresh interval, in milliseconds, of the Spectrum Analyzer.
        /// </summary>
        /// <remarks>
        /// The valid range of the interval is 10 milliseconds to 1000 milliseconds.
        /// </remarks>
        //[Category("Common")]
        public int RefreshInterval
        {
            // IMPORTANT: To maintain parity between setting a property in XAML and procedural code, do not touch the getter and setter inside this dependency property!
            get
            {
                return (int)GetValue(RefreshIntervalProperty);
            }
            set
            {
                SetValue(RefreshIntervalProperty, value);
            }
        }
        #endregion

        #region FftComplexity
        /// <summary>
        /// Identifies the <see cref="FftComplexity" /> dependency property. 
        /// </summary>
        public static readonly DependencyProperty FftComplexityProperty = DependencyProperty.Register(
            "FftComplexity", typeof(FftSize), typeof(SpectrumAnalyzer), new PropertyMetadata(FftSize.Fft2048, OnFftComplexityChanged));

        private static void OnFftComplexityChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            var spectrumAnalyzer = o as SpectrumAnalyzer;
            spectrumAnalyzer?.OnFftComplexityChanged((FftSize)e.OldValue, (FftSize)e.NewValue);
        }

        /// <summary>
        /// Called after the <see cref="FftComplexity"/> value has changed.
        /// </summary>
        /// <param name="oldValue">The previous value of <see cref="FftComplexity"/></param>
        /// <param name="newValue">The new value of <see cref="FftComplexity"/></param>
        protected virtual void OnFftComplexityChanged(FftSize oldValue, FftSize newValue)
        {
            _channelData = new float[((int)newValue / 2)];
        }

        /// <summary>
        /// Gets or sets the complexity of FFT results the Spectrum Analyzer expects. Larger values
        /// will be more accurate at converting time domain data to frequency data, but slower.
        /// </summary>
        public FftSize FftComplexity
        {
            // IMPORTANT: To maintain parity between setting a property in XAML and procedural code, do not touch the getter and setter inside this dependency property!
            get
            {
                return (FftSize)GetValue(FftComplexityProperty);
            }
            set
            {
                SetValue(FftComplexityProperty, value);
            }
        }
        #endregion

        #endregion

        #region Template Overrides
        /// <summary>
        /// When overridden in a derived class, is invoked whenever application code
        /// or internal processes call System.Windows.FrameworkElement.ApplyTemplate().
        /// </summary>
        protected override void OnApplyTemplate()
        {
            _spectrumCanvas = GetTemplateChild("PART_SpectrumCanvas") as Canvas;
            if (_spectrumCanvas == null) return;
            _spectrumCanvas.SizeChanged += spectrumCanvas_SizeChanged;
            UpdateBarLayout();
        }
        #endregion

        #region Constructors
        static SpectrumAnalyzer()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SpectrumAnalyzer"/> class.
        /// </summary>
        public SpectrumAnalyzer()
        {
            DefaultStyleKey = typeof(SpectrumAnalyzer);

            _animationTimer = new DispatcherTimer()
            {
                Interval = TimeSpan.FromMilliseconds(DefaultUpdateInterval),
            };
            _animationTimer.Tick += animationTimer_Tick;
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Register a sound player from which the spectrum analyzer
        /// can get the necessary playback data.
        /// </summary>
        /// <param name="soundPlayer">A sound player that provides spectrum data through the ISpectrumPlayer interface methods.</param>
        public void RegisterSoundPlayer(ISpectrumPlayer soundPlayer)
        {
            _soundPlayer = soundPlayer;
            soundPlayer.PropertyChanged += soundPlayer_PropertyChanged;
            UpdateBarLayout();
            _animationTimer.Start();
        }
        #endregion

        #region Private Drawing Methods
        private void UpdateSpectrum()
        {
            if (_soundPlayer == null || _spectrumCanvas == null || _spectrumCanvas.RenderSize.Width < 1 || _spectrumCanvas.RenderSize.Height < 1)
                return;

            if (_soundPlayer.IsPlaying && !_soundPlayer.GetFftData(_channelData))
                return;

            UpdateSpectrumShapes();
        }


        private void UpdateSpectrumShapes()
        {
            var allZero = true;
            double fftBucketHeight = 0f;
            double barHeight = 0f;
            double lastPeakHeight = 0f;
            var height = _spectrumCanvas.RenderSize.Height;
            var barIndex = 0;
            var peakDotHeight = Math.Max(_barWidth / 2.0f, 1);
            var barHeightScale = (height - peakDotHeight);

            for (var i = _minimumFrequencyIndex; i <= _maximumFrequencyIndex; i++)
            {
                // If we're paused, keep drawing, but set the current height to 0 so the peaks fall.
                if (!_soundPlayer.IsPlaying)
                {
                    barHeight = 0f;
                }
                else // Draw the maximum value for the bar's band
                {
                    switch (BarHeightScaling)
                    {
                        case BarHeightScalingStyles.Decibel:
                            var dbValue = 20 * Math.Log10(_channelData[i]);
                            fftBucketHeight = ((dbValue - MinDbValue) / DbScale) * barHeightScale;
                            break;
                        case BarHeightScalingStyles.Linear:
                            fftBucketHeight = (_channelData[i] * ScaleFactorLinear) * barHeightScale;
                            break;
                        case BarHeightScalingStyles.Sqrt:
                            fftBucketHeight = (((Math.Sqrt(_channelData[i])) * ScaleFactorSqr) * barHeightScale);
                            break;
                    }

                    if (barHeight < fftBucketHeight)
                        barHeight = fftBucketHeight;
                    if (barHeight < 0f)
                        barHeight = 0f;
                }

                // If this is the last FFT bucket in the bar's group, draw the bar.
                var currentIndexMax = IsFrequencyScaleLinear ? _barIndexMax[barIndex] : _barLogScaleIndexMax[barIndex];
                if (i == currentIndexMax)
                {
                    // Peaks can't surpass the height of the control.
                    if (barHeight > height)
                        barHeight = height;

                    if (AveragePeaks && barIndex > 0)
                        barHeight = (lastPeakHeight + barHeight) / 2;

                    var peakYPos = barHeight;

                    if (_channelPeakData[barIndex] < peakYPos)
                        _channelPeakData[barIndex] = (float)peakYPos;
                    else
                        _channelPeakData[barIndex] = (float)(peakYPos + (PeakFallDelay * _channelPeakData[barIndex])) / (PeakFallDelay + 1);

                    var xCoord = BarSpacing + (_barWidth * barIndex) + (BarSpacing * barIndex) + 1;

                    _barShapes[barIndex].Margin = new Thickness(xCoord, (height - 1) - barHeight, 0, 0);
                    _barShapes[barIndex].Height = barHeight;
                    _peakShapes[barIndex].Margin = new Thickness(xCoord, (height - 1) - _channelPeakData[barIndex] - peakDotHeight, 0, 0);
                    _peakShapes[barIndex].Height = peakDotHeight;

                    if (_channelPeakData[barIndex] > 0.05)
                        allZero = false;

                    lastPeakHeight = barHeight;
                    barHeight = 0f;
                    barIndex++;
                }
            }

            if (allZero && !_soundPlayer.IsPlaying)
                _animationTimer.Stop();
        }

        private void UpdateBarLayout()
        {
            if (_soundPlayer == null || _spectrumCanvas == null)
                return;

            _barWidth = Math.Max(((_spectrumCanvas.RenderSize.Width - (BarSpacing * (BarCount + 1))) / BarCount), 1);
            _maximumFrequencyIndex = Math.Min(_soundPlayer.GetFftFrequencyIndex(MaximumFrequency) + 1, 2047);
            _minimumFrequencyIndex = Math.Min(_soundPlayer.GetFftFrequencyIndex(MinimumFrequency), 2047);

            int actualBarCount;
            actualBarCount = _barWidth >= 1.0d
                ? BarCount
                : Math.Max((int)((_spectrumCanvas.RenderSize.Width - BarSpacing) / (_barWidth + BarSpacing)), 1);
            _channelPeakData = new float[actualBarCount];

            var indexCount = _maximumFrequencyIndex - _minimumFrequencyIndex;
            var linearIndexBucketSize = (int)Math.Round(indexCount / (double)actualBarCount, 0);
            var maxIndexList = new List<int>();
            var maxLogScaleIndexList = new List<int>();
            var maxLog = Math.Log(actualBarCount, actualBarCount);
            for (var i = 1; i < actualBarCount; i++)
            {
                maxIndexList.Add(_minimumFrequencyIndex + (i * linearIndexBucketSize));
                var logIndex = (int)((maxLog - Math.Log((actualBarCount + 1) - i, (actualBarCount + 1))) * indexCount) + _minimumFrequencyIndex;
                maxLogScaleIndexList.Add(logIndex);
            }
            maxIndexList.Add(_maximumFrequencyIndex);
            maxLogScaleIndexList.Add(_maximumFrequencyIndex);
            _barIndexMax = maxIndexList.ToArray();
            _barLogScaleIndexMax = maxLogScaleIndexList.ToArray();

            _spectrumCanvas.Children.Clear();
            _barShapes.Clear();
            _peakShapes.Clear();

            var height = _spectrumCanvas.RenderSize.Height;
            var peakDotHeight = Math.Max(_barWidth / 2.0f, 1);
            for (var i = 0; i < actualBarCount; i++)
            {
                var xCoord = BarSpacing + (_barWidth * i) + (BarSpacing * i) + 1;
                var barRectangle = new Rectangle()
                {
                    Margin = new Thickness(xCoord, height, 0, 0),
                    Width = _barWidth,
                    Height = 0,
                    Style = BarStyle
                };
                _barShapes.Add(barRectangle);
                var peakRectangle = new Rectangle()
                {
                    Margin = new Thickness(xCoord, height - peakDotHeight, 0, 0),
                    Width = _barWidth,
                    Height = peakDotHeight,
                    Style = PeakStyle
                };
                _peakShapes.Add(peakRectangle);
            }

            foreach (var shape in _barShapes)
                _spectrumCanvas.Children.Add(shape);
            foreach (var shape in _peakShapes)
                _spectrumCanvas.Children.Add(shape);

            ActualBarWidth = _barWidth;
        }
        #endregion

        #region Event Handlers
        private void soundPlayer_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "IsPlaying":
                    if (_soundPlayer.IsPlaying && !_animationTimer.IsEnabled)
                        _animationTimer.Start();
                    break;
            }
        }

        private void animationTimer_Tick(object sender, object e)
        {
            UpdateSpectrum();
        }

        private void spectrumCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateBarLayout();
        }

        #endregion
    }
}
