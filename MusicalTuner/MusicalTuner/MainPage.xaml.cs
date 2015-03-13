namespace MusicalTuner
{
    using FFTW;
using libfilter;
using libsound;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;

    public sealed partial class MainPage : Page
    {
        enum RecordingMode
        {
            Fft,
            Zc,
            Ac,
        }

        private SoundIO sio;

        private FFTWrapper fft;
        FilterDesign fd;
        Filter filt;
        Filter filtZC;

        private static int bufferSize = 480*10;

        private bool recordingFFT = false;
        private bool recordingZero = false;
        private RecordingMode recordingMode;

        private float[] bufferAuto = new float[bufferSize];
        private float[] bufferZero = new float[bufferSize];
        private float[] bufferFFT = new float[bufferSize];
        private int bufferIdx = 0;
        private bool youPressedString = false;
        private bool youPressedProcess = false;

        private int selectedString;

        // Zero Crossing related variables

        private int zeroCrossingCounter;
        private bool zeroCrossed = false;
        private float zeroCrossFrequencyZero;
        private int sampleCounterBtwZeroCrossings;
        private int firstPoint, lastPoint;
        private bool firstPointIs = false;
        private bool lastPointIs = false;
        private int ignoreSample = 0;
        private double total;
        //private float totalF;

        //Auto Correlation Variables
        private float[] buffer = new float[bufferSize];
        private int bufferPosition = 0;
        private float targetFrequency;
        private float lowFreq, highFreq;
        private readonly LinearGradientBrush gradientBlack = new LinearGradientBrush();
        private readonly LinearGradientBrush gradientRed = new LinearGradientBrush();
        private readonly LinearGradientBrush gradientGreen = new LinearGradientBrush();

        public MainPage()
        {
            this.InitializeComponent();

            this.NavigationCacheMode = NavigationCacheMode.Required;

            GradientStop black1 = new GradientStop();
            GradientStop black2 = new GradientStop();
            GradientStop green1 = new GradientStop();
            GradientStop green2 = new GradientStop();
            GradientStop red1 = new GradientStop();
            GradientStop red2 = new GradientStop();

            black1.Color = Colors.Black;
            black1.Offset = 0;
            black2.Color = Color.FromArgb(255, 218, 209, 209);
            black2.Offset = 1;

            green1.Color = Color.FromArgb(255, 218, 209, 209);
            green1.Offset = 1;
            green2.Color = Colors.DarkGreen;
            green2.Offset = 0;

            red1.Color = Colors.Red;
            red1.Offset = 0;
            red2.Color = Colors.Gray;
            red2.Offset = 1;

            this.SetGradientColors(this.gradientGreen, green1, green2);
            this.SetGradientColors(this.gradientRed, red1, red2);
            this.SetGradientColors(this.gradientBlack, black1, black2);
        }

        private void SetGradientColors(LinearGradientBrush gradient, GradientStop color1, GradientStop color2)
        {
            gradient.StartPoint = new Point(0.5, 0);
            gradient.EndPoint = new Point(0.5, 1);
            gradient.GradientStops.Add(color1);
            gradient.GradientStops.Add(color2);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            // Create sound input
            sio = new SoundIO();

            // Initialize FilterDesign object, create a filter impulse response, then wrap that in a Filter object
            fd = new FilterDesign();
            float[] impulseResponse = fd.FIRDesignWindowed(0.0f, 0.1f, WindowType.HAMMING);
            filt = new Filter(impulseResponse);

            float[] impulseResponseZC = fd.FIRDesignWindowed(0.0f, 0.1f, WindowType.BLACKMAN);
            filtZC = new Filter(impulseResponseZC);
            buttonInitialization(false);
        }

        private void buttonInitialization(bool stats)
        {
            btnString1.IsEnabled = stats;
            btnString2.IsEnabled = stats;
            btnString3.IsEnabled = stats;
            btnString4.IsEnabled = stats;
            btnString5.IsEnabled = stats;
            btnString6.IsEnabled = stats;
        }

        void sio_audioInEvent_FFT(float[] data)
        {
            recordingFFT = true;
            process_audio_FFT(data);
        }

        void sio_audioInEvent_Zero(float[] data)
        {
            recordingZero = true;
            process_audio_Zero(data);
        }

        void sio_audioInEvent_Auto(float[] data)
        {
            float[] filteredData = filt.filter(data);
            Array.Copy(filteredData, 0, this.buffer, this.bufferPosition, data.Length);
            bufferPosition = (bufferPosition + data.Length) % this.buffer.Length;
            if (this.bufferPosition == 0)
            {
                process_audio_Auto(this.buffer, 50, 600, 3, 1);
            }
        }

        private void process_audio_FFT(float[] data)
        {
            if (!recordingFFT)
                return;

            float sampleRate = sio.getInputSampleRate();

            float[] filteredData = filt.filter(data);
            Array.Copy(filteredData, 0, this.buffer, this.bufferPosition, data.Length);
            bufferPosition = (bufferPosition + data.Length) % this.buffer.Length;
            if (this.bufferPosition == 0)
            {
                uint N = Convert.ToUInt32(this.buffer.Length);
                fft = new FFTWrapper(N);

                float[] fftmag = fft.fftMag(this.buffer);
                // Detect and display dominant freq.
                float maxValue = fftmag.Max();
                float maxIndex = fftmag.ToList().IndexOf(maxValue);
                float detectedPitch = maxIndex * (sampleRate / N);
                recordingFFT = false;
                Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    if (detectedPitch > lowFreq && detectedPitch < highFreq)
                    {
                        this.pitchOut.Text = (detectedPitch).ToString("#0.##");
                        double pitchGage = (detectedPitch - this.targetFrequency);
                        changeColor(pitchGage);
                    }
                });
            }
        }

        private void process_audio_Zero(float[] data)
        {
            if (!recordingZero)
            {
                return;
            }

            float sampleRate = sio.getInputSampleRate();
            float[] filteredData = filtZC.filter(data);


            int i = 0;
            ignoreSample = 1;
            while (bufferIdx < bufferZero.Length)
            {
                bufferZero[bufferIdx] = filteredData[i + ignoreSample];
                bufferIdx++;
                i++;
                if (i + ignoreSample >= filteredData.Length)
                {
                    // ignoreSample = 0;
                    return;
                }

            }

            uint N = Convert.ToUInt32(bufferZero.Length);
            bufferIdx = 0;
            recordingZero = false;
            for (int j = 1; j < N; j++)
            {
                if (bufferZero[j] * bufferZero[j - 1] < 0)
                {
                    zeroCrossed = true;
                    zeroCrossingCounter++;

                    if (zeroCrossed && zeroCrossingCounter == 1)
                    {
                        firstPointIs = true;
                        firstPoint = j - 1;
                    }
                    else if (zeroCrossed && zeroCrossingCounter == 3)
                    {
                        lastPoint = j - 1;
                        lastPointIs = true;

                    }
                }
                if (firstPointIs && lastPointIs)
                {
                    sampleCounterBtwZeroCrossings = lastPoint - firstPoint;
                    zeroCrossingCounter = 0;
                    lastPointIs = false;
                    firstPointIs = false;
                    if (sampleCounterBtwZeroCrossings > 0)
                    {
                        //zeroCrossFrequencyZero = 1 / ((sampleCounterBtwZeroCrossings * (N/sampleRate)) / N); 
                        zeroCrossFrequencyZero = sampleRate / sampleCounterBtwZeroCrossings;
                    }

                }

                //total += zeroCrossFrequency;
                //if (j == 1919)
                //{
                //   totalF = total / j;
                //   total = 0;
                //}
            }

            Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (zeroCrossFrequencyZero > lowFreq && zeroCrossFrequencyZero < highFreq)
                {
                    this.SetPitch(zeroCrossFrequencyZero);
                    double pitchGage = (zeroCrossFrequencyZero - this.targetFrequency);
                    changeColor(pitchGage);
                }

            });
        }

        private void process_audio_Auto(float[] input, double minHz, double maxHz, int nCandidates, int nResolution)
        {
            float sampleRate = sio.getInputSampleRate();
            float numOfChannel = sio.getInputNumChannels();
            int nLowPeriodInSamples = hzToPeriodInSamples(maxHz, sampleRate);
            int nHiPeriodInSamples = hzToPeriodInSamples(minHz, sampleRate);
            if (nHiPeriodInSamples <= nLowPeriodInSamples) throw new Exception("Bad range for pitch detection.");
            float[] samples = input;

            if (samples.Length < nHiPeriodInSamples) throw new Exception("Not enough samples.");

            // yield an array of data, and then we find the index at which the value is highest.
            double[] results = new double[nHiPeriodInSamples - nLowPeriodInSamples];
            for (int period = nLowPeriodInSamples; period < nHiPeriodInSamples; period += nResolution)
            {
                double sum = 0;
                // for each sample, find correlation. (If they are far apart, small)
                for (int i = 0; i < samples.Length - period; i++)
                    sum += samples[i] * samples[i + period];

                double mean = sum / (double)samples.Length;
                results[period - nLowPeriodInSamples] = mean;
            }
            // find the best indices
            var candidates = findBestCandidates(nCandidates, ref results); //note findBestCandidates modifies parameter
            int[] bestIndices = candidates.Item1;

            //Interpolate
            double[] bestValues = candidates.Item2;
            double adjustedIndex = interpolate(bestIndices,bestValues);
            // convert back to Hz
            double[] res = new double[nCandidates];
            for (int i = 0; i < nCandidates; i++)
                res[i] = periodInSamplesToHz((bestIndices[i] + nLowPeriodInSamples) + adjustedIndex, sampleRate);
            double detectedPitch = res[0];
           
            Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (detectedPitch>0)// >  lowFreq && detectedPitch < highFreq)
                {
                    this.SetPitch(detectedPitch);
                    double pitchGage = (detectedPitch - this.targetFrequency);
                    changeColor(pitchGage);
                }
            });

        }

        private double interpolate(int[] bestIndices, double[] bestValues)
        {
            int[] x = bestIndices;
            double[] y = bestValues;
            double deltam = (y[1] - y[2]) / (2 * y[0] - y[1] - y[2]) / 2;
            double deltaG = Math.Log(y[1] / y[2]) / Math.Log(Math.Pow(y[0], 2.0) / y[1] / y[2]) / 2;

            return deltam;
        }

        private static Tuple<int[], double[]> findBestCandidates(int n, ref double[] inputs)
        {
            if (inputs.Length < n) throw new Exception("Length of inputs is not long enough.");
            int[] res = new int[n]; // will hold indices with the highest amounts.
            double[] values = new double[n];

            for (int c = 0; c < n; c++)
            {
                // find the highest.
                double fBestValue = double.MinValue;
                int nBestIndex = -1;
                for (int i = 0; i < inputs.Length; i++)
                    if (inputs[i] > fBestValue) { nBestIndex = i; fBestValue = inputs[i]; }

                // record this highest value
                res[c] = nBestIndex;
                values[c] = inputs[nBestIndex];

                // now blank out that index.
                inputs[nBestIndex] = double.MinValue;

            }

            return new Tuple<int[], double[]>(res, values);
        }
        
        private static int hzToPeriodInSamples(double hz, float sampleRate)
        {
            return (int)(1 / (hz / (double)sampleRate));
        }
        
        private static double periodInSamplesToHz(double period, float sampleRate)
        {
            return 1 / (period / (double)sampleRate);
        }
        
        public void changeColor(double pitchDelta)
        {
            // Create a LinearGradientBrush and use it to 
            // paint the rectangle.
            double absDelta = Math.Abs(pitchDelta);
            int deltaFreq;
            if (absDelta > 10)
            {
                deltaFreq = 3;
            }
            else if (absDelta > 5)
            {
                deltaFreq = 2;
            }
            else if (absDelta > 2)
            {
                deltaFreq = 1;
            }
            else
            {
                deltaFreq = 0;
            }

            this.SetColor(deltaFreq * Math.Sign(pitchDelta));
        }

        private void ResetColor()
        {
            centerFrequency.Fill = this.gradientBlack;
            plusTen.Fill = this.gradientBlack;
            minusTen.Fill = this.gradientBlack;
            plusTwenty.Fill = this.gradientBlack;
            minusTwenty.Fill = this.gradientBlack;
            plusThirty.Fill = this.gradientBlack;
            minusThirty.Fill = this.gradientBlack;
        }

        private void SetColor(int deltaFreq)
        {
            this.ResetColor();

            Rectangle rectangle;
            LinearGradientBrush gradient = this.gradientRed;
            switch (deltaFreq)
            {
                case -3:
                    rectangle = minusThirty;
                    break;
                case -2:
                    rectangle = minusTwenty;
                    break;
                case -1:
                    rectangle = minusTen;
                    break;
                case 0:
                    rectangle = centerFrequency;
                    gradient = this.gradientGreen;
                    break;
                case 1:
                    rectangle = plusTen;
                    break;
                case 2:
                    rectangle = plusTwenty;
                    break;
                case 3:
                    rectangle = plusThirty;
                    break;
                default:
                    throw new InvalidOperationException(deltaFreq.ToString());
            }

            rectangle.Fill = gradient;
            }

        private void GuiterTunesCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            buttonInitialization(true);

            if (Standard.IsSelected == true)
            {
                btnString1.Content = "E";
                btnString2.Content = "B";
                btnString3.Content = "G";
                btnString4.Content = "D";
                btnString5.Content = "A";
                btnString6.Content = "E";

            }
            else if (EDrop.IsSelected == true)
            {
                btnString1.Content = "E";
                btnString2.Content = "B";
                btnString3.Content = "G";
                btnString4.Content = "D";
                btnString5.Content = "A";
                btnString6.Content = "D";
            }
            else
            {
                btnString1.Content = "D";
                btnString2.Content = "A";
                btnString3.Content = "G";
                btnString4.Content = "D";
                btnString5.Content = "A";
                btnString6.Content = "D";
            }

            if (this.youPressedString)
            {
                switch (this.selectedString)
                {
                    case 1:
                        this.Button_Click_String1(sender, e);
                        break;
                    case 2:
                        this.Button_Click_String2(sender, e);
                        break;
                    case 3:
                        this.Button_Click_String3(sender, e);
                        break;
                    case 4:
                        this.Button_Click_String4(sender, e);
                        break;
                    case 5:
                        this.Button_Click_String5(sender, e);
                        break;
                    case 6:
                        this.Button_Click_String6(sender, e);
                        break;
                    default:
                        throw new InvalidOperationException(this.selectedString.ToString());
        }
            }
        }

        private void SetPitch(double value)
        {
            this.pitchOut.Text = value.ToString("F2");
        }

        private void SetTarget(float value)
        {
            this.targetFrequency = value;
            this.pitchOutTarget.Text = value.ToString("F2");
            this.lowFreq = value - 15.0f;
            this.highFreq = value + 15.0f;
        }

        private void Button_Click_FFT(object sender, RoutedEventArgs e)
        {
            this.SetRecordingMode(this.btnFFT, RecordingMode.Fft, this.sio_audioInEvent_FFT);
        }

        private void SetRecordingMode(Button button, RecordingMode mode, AudioInCallback audioInCallback)
            {
            if (!this.youPressedProcess)
            {
                button.Background = new SolidColorBrush(Colors.Green);
                this.recordingMode = mode;
                this.youPressedProcess = true;

                this.sio.start();
                this.sio.audioInEvent += audioInCallback;
                this.SetTarget(this.targetFrequency);
            }
            else if (this.recordingMode == mode)
            {
                button.Background = new SolidColorBrush(Colors.Red);
                this.youPressedProcess = false;

                this.sio.stop();
                this.sio.audioInEvent -= audioInCallback;
                this.ResetPage();
            }
        }

        private void Button_Click_ZeroCrossing(object sender, RoutedEventArgs e)
        {
            this.SetRecordingMode(this.btnZero, RecordingMode.Zc, this.sio_audioInEvent_Zero);
        }

        private void Button_Click_Autocorrelation(object sender, RoutedEventArgs e)
        {
            this.SetRecordingMode(this.btnAuto, RecordingMode.Ac, this.sio_audioInEvent_Auto);
        }

        private void ResetPage()
            {
            this.SetPitch(0);
            this.ResetColor();
        }

        private void Button_Click_String1(object sender, RoutedEventArgs e)
        {
            this.SelectString(btnString1, 1, 294.0f, new KeyValuePair<string, float>("E", 329.6f));
                }

        private void Button_Click_String2(object sender, RoutedEventArgs e)
        {
            this.SelectString(btnString2, 2, 220.0f, new KeyValuePair<string, float>("B", 246.9f));
                }

        private void Button_Click_String3(object sender, RoutedEventArgs e)
        {
            this.SelectString(btnString3, 3, 196.0f);
        }

        private void Button_Click_String4(object sender, RoutedEventArgs e)
                {
            this.SelectString(btnString4, 4, 146.8f);
                }

        private void Button_Click_String5(object sender, RoutedEventArgs e)
            {
            this.SelectString(btnString5, 5, 110.0f);
            }

        private void Button_Click_String6(object sender, RoutedEventArgs e)
        {
            this.SelectString(btnString6, 6, 73.4f, new KeyValuePair<string, float>("E", 82.4f));
        }

        private void SelectString(Button button, int selection, float defaultFrequency, params KeyValuePair<string, float>[] frequencies)
        {
            if (!youPressedString)
            {
                button.Background = new SolidColorBrush(Colors.Green);
                youPressedString = true;
                selectedString = selection;

                bool set = false;
                foreach (var item in frequencies)
                {
                    if (button.Content.Equals(item.Key))
            {
                        this.SetTarget(item.Value);
                        set = true;
                        break;
            }
        }

                if (!set)
                {
                    this.SetTarget(defaultFrequency);
                }
            }
            else if (selectedString == selection)
            {
                button.Background = new SolidColorBrush(Colors.Red);
                youPressedString = false;
            }
        }
    }
}