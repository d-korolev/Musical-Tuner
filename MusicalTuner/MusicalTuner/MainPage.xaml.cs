using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using libsound;
using libfilter;
using FFTW;
using Windows.Media;
using Windows.UI;


namespace MusicalTuner
{


    public sealed partial class MainPage : Page
    {
        private SoundIO sio;

        private FFTWrapper fft;
        FilterDesign fd;
        Filter filt;
        private static int bufferSize = 480*10;

        private bool recordingFFT = false;
        private bool recordingZero = false;
        private bool recordingAuto = false;
        private string recordingMode;

        private float[] bufferAuto = new float[bufferSize];
        private float[] bufferZero = new float[bufferSize];
        private float[] bufferFFT = new float[bufferSize];
        private int bufferIdx = 0;
        private int bufferIdxFFT = 0;
        private bool youPressedString = false;
        private bool youPressedProcess = false;

        private int selectedString;

        // Zero Crossing related variables

        private int zeroCrossingCounter;
        private bool zeroCrossed = false;
        private float zeroCrossFrequencyZero;
        private float zeroCrossFrequencyFFT;
        private float zeroCrossTime;
        private int sampleCounterBtwZeroCrossings;
        private float zeroCrossPeriod;
        private int firstPoint, lastPoint;
        private bool firstPointIs = false;
        private bool lastPointIs = false;
        private int ignoreSample = 0;
        //private float total;
        //private float totalF;

        //Auto Correlation Variables
        private float[] buffer = new float[bufferSize];
        private int bufferPosition = 0;
        private float targetFrequency;
        private float lowFreq, highFreq;


        public MainPage()
        {
            this.InitializeComponent();

            this.NavigationCacheMode = NavigationCacheMode.Required;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            // Create sound input
            sio = new SoundIO();

            // Initialize FilterDesign object, create a filter impulse response, then wrap that in a Filter object
            fd = new FilterDesign();
            float[] impulseResponse = fd.FIRDesignWindowed(0.0f, 0.1f, WindowType.HAMMING);
            filt = new Filter(impulseResponse);
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
            recordingAuto = true;
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
                float detectedFrequencyFFT = maxIndex * (sampleRate / N);
                bufferIdxFFT = 0;
                recordingFFT = false;
                Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    this.pitchOut.Text = detectedFrequencyFFT.ToString("#0.##");

                });

            }

            //float sampleRate = sio.getInputSampleRate();

            //Filling in new 1920 samples buffer
            //int i = 0;
            //ignoreSample = 1;
            //while (bufferIdxFFT < bufferFFT.Length)
            //{
            //    bufferFFT[bufferIdxFFT] = data[i + ignoreSample];
            //    bufferIdxFFT++;
            //    i++;
            //    if (i + ignoreSample >= data.Length)
            //    {
            //        ignoreSample = 0;
            //        return;
            //    }

            //}
            //uint N = Convert.ToUInt32(bufferFFT.Length);
            //fft = new FFTWrapper(N);
            //float[] fftmag = fft.fftMag(bufferFFT);
            //// Detect and display dominant freq.
            //float maxValue = fftmag.Max();
            //float maxIndex = fftmag.ToList().IndexOf(maxValue);
            //float detectedFrequencyFFT = maxIndex * (sampleRate / N);
            //bufferIdxFFT = 0;
            //recordingFFT = false;
            //Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            //{
            //    this.pitchOut.Text = detectedFrequencyFFT.ToString("#0.##");

            //});

        }


        private void process_audio_Zero(float[] data)
        {
            if (!recordingZero)
                return;


            float sampleRate = sio.getInputSampleRate();

            //Filling in new 1920 samples buffer
            int i = 0;
            ignoreSample = 1;
            while (bufferIdx < bufferZero.Length)
            {
                bufferZero[bufferIdx] = data[i + ignoreSample];
                bufferIdx++;
                i++;
                if (i + ignoreSample >= data.Length)
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
                this.pitchOut.Text = zeroCrossFrequencyZero.ToString("#0.##");

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
                res[i] = periodInSamplesToHz((bestIndices[i] + nLowPeriodInSamples), sampleRate);
            double detectedPitch = res[0];
            Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (detectedPitch > 0)// lowFreq && detectedPitch < highFreq)
                {
                    this.pitchOut.Text = (detectedPitch).ToString("#0.##");
                    double pitchGage = Math.Abs(detectedPitch - this.targetFrequency);
                    changeColor(pitchGage);
                }
            });

        }

        private double interpolate(int[] bestIndices, double[] bestValues)

        {
            int[] x = bestIndices;
            double[] y = bestValues;
            double a0 = y[2] / (x[2] - x[0]) / (x[2] - x[1]);
            double a1 = y[0] / (x[0] - x[2]) / (x[0] - x[1]);
            double a2 = y[1] / (x[1] - x[2]) / (x[1] - x[0]);

            double A = a0 + a1 + a2;
            double B = -((a0 * (x[0] + x[1])) + (a1 * (x[2] + x[1])) + (a2 * (x[2] + x[0])));
            double d2 = 2 * (((y[1] - y[0]) / (x[1] - x[0])) - ((y[0] - y[2]) / (x[0] - x[2]))) / (x[1] - x[2]);
            double d1 = ((y[0] - y[2]) / (x[0] - x[2])) + ((d2 / 2) * (x[0] - x[2]));
            double d0 = y[0];
            double maxV = -d1 / d2 / 2 ;
            double max2 = -A / B / 2 ;
            return maxV;
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
            LinearGradientBrush gradient = new LinearGradientBrush();
            gradient.StartPoint = new Point(0.5, 0);
            gradient.EndPoint = new Point(0.5, 1);
            if (pitchDelta >= 0 && pitchDelta <= 2)
            {
                GradientStop color1 = new GradientStop();
                color1.Color = Colors.Black;
                color1.Offset = 0;
                gradient.GradientStops.Add(color1);
                GradientStop color2 = new GradientStop();
                color2.Color = Color.FromArgb(255, 114, 238, 108);
                color2.Offset = 1;
                gradient.GradientStops.Add(color2);
                centerFrequency.Fill = gradient;
                plusTen.Fill = gradient;
                minusTen.Fill = gradient;
                plusTwenty.Fill = gradient;
                minusTwenty.Fill = gradient;
                plusThirty.Fill = gradient;
                minusThirty.Fill = gradient;
            }
            else if (pitchDelta > 2 && pitchDelta <= 5)
            {
                //// Create a LinearGradientBrush and use it to 
                //// paint the Bars
                GradientStop color1 = new GradientStop();
                color1.Color = Colors.Black;
                color1.Offset = 0;
                gradient.GradientStops.Add(color1);
                GradientStop color2 = new GradientStop();
                color2.Color = Color.FromArgb(
                                              255, // Specifies the transparency of the color.
                                              247, // Specifies the amount of red.
                                              133, // specifies the amount of green.
                                              18); // Specifies the amount of blue.;
                color2.Offset = 1;
                gradient.GradientStops.Add(color2);
                centerFrequency.Fill = gradient;
                plusTen.Fill = gradient;
                minusTen.Fill = gradient;
                plusTwenty.Fill = gradient;
                minusTwenty.Fill = gradient;
                plusThirty.Fill = gradient;
                minusThirty.Fill = gradient;

            }
            else
            {
                GradientStop color1 = new GradientStop();
                color1.Color = Colors.Yellow;
                color1.Offset = 0.2;
                gradient.GradientStops.Add(color1);

                GradientStop color2 = new GradientStop();
                color2.Color = Colors.Orange;
                color2.Offset = 0.5;
                gradient.GradientStops.Add(color2);

                GradientStop color3 = new GradientStop();
                color3.Color = Colors.Red;
                color3.Offset = 0.8;
                gradient.GradientStops.Add(color3);

                centerFrequency.Fill = gradient;
                plusTen.Fill = gradient;
                minusTen.Fill = gradient;
                plusTwenty.Fill = gradient;
                minusTwenty.Fill = gradient;
                plusThirty.Fill = gradient;
                minusThirty.Fill = gradient;

            }
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

        }

        private void TextBlock_SelectionChanged(object sender, RoutedEventArgs e)
        {

        }

        private void Button_Click_FFT(object sender, RoutedEventArgs e)
        {

            if (!youPressedProcess)
            {
                btnFFT.Background = new SolidColorBrush(Colors.Green);
                youPressedProcess = true;
                recordingMode = "FFT";

                sio.start();
                sio.audioInEvent += sio_audioInEvent_FFT;
            }
            else if (string.Equals(recordingMode, "FFT"))
            {
                btnFFT.Background = new SolidColorBrush(Colors.Red);
                youPressedProcess = false;
                sio.stop();
                sio.audioInEvent -= sio_audioInEvent_FFT;
            }

        }

        private void Button_Click_ZeroCrossing(object sender, RoutedEventArgs e)
        {
            if (!youPressedProcess)
            {
                btnZero.Background = new SolidColorBrush(Colors.Green);
                recordingMode = "ZC";
                youPressedProcess = true;
                sio.start();
                sio.audioInEvent += sio_audioInEvent_Zero;
            }
            else if (string.Equals(recordingMode, "ZC"))
            {
                btnZero.Background = new SolidColorBrush(Colors.Red);
                youPressedProcess = false;
                sio.stop();
                sio.audioInEvent -= sio_audioInEvent_Zero;
            }
            //sio.start();
            //sio.audioInEvent += sio_audioInEvent_ZeroCrossing;
        }

        private void Button_Click_Autocorrelation(object sender, RoutedEventArgs e)
        {
            if (!youPressedProcess)
            {
                btnAuto.Background = new SolidColorBrush(Colors.Green);
                youPressedProcess = true;
                recordingMode = "AC";

                sio.start();
                sio.audioInEvent += sio_audioInEvent_Auto;
            }
            else if(string.Equals(recordingMode,"AC"))
            {
                btnAuto.Background = new SolidColorBrush(Colors.Red);
                youPressedProcess = false;
                sio.stop();
                sio.audioInEvent -= sio_audioInEvent_Auto;

            }
            //sio.start();
            //sio.audioInEvent += sio_audioInEvent_AutoCorrelation;
        }

        private void Button_Click_String1(object sender, RoutedEventArgs e)
        {
            if (!youPressedString)
            {
                youPressedString = true;
                selectedString = 1;
                btnString1.Background = new SolidColorBrush(Colors.Green);
                if (btnString1.Content.Equals("E"))
                {
                    this.targetFrequency = 329.6f;
                    this.lowFreq = this.targetFrequency - 30.0f;
                    this.highFreq = this.targetFrequency + 30.0f; 
                    pitchOutTarget.Text = "329.6";
                }
                else 
                {
                    this.targetFrequency = 311.1f;
                    this.lowFreq = this.targetFrequency - 30.0f;
                    this.highFreq = this.targetFrequency + 30.0f; ; 
                    pitchOutTarget.Text = "311.1";
                }
            }
            else if(selectedString==1)
            {
                btnString1.Background = new SolidColorBrush(Colors.Red);
                youPressedString = false;
            }
        }

        private void Button_Click_String2(object sender, RoutedEventArgs e)
        {
            if (!youPressedString)
            {
                youPressedString = true;
                selectedString = 2;
                btnString2.Background = new SolidColorBrush(Colors.Green);
                if (btnString2.Content.Equals("B"))
                {
                    this.targetFrequency = 246.9f;
                    this.lowFreq = this.targetFrequency -30.0f;
                    this.highFreq = this.targetFrequency + 30.0f; 

                    pitchOutTarget.Text = "246.9";
                }
                else
                {
                    this.targetFrequency = 220.0f;
                    this.lowFreq = this.targetFrequency - 30.0f;
                    this.highFreq = this.targetFrequency + 30.0f; 
                    pitchOutTarget.Text = "220.0";
                }
            }
            else if (selectedString==2)
            {
                btnString2.Background = new SolidColorBrush(Colors.Red);
                youPressedString = false;
            }
        }

        private void Button_Click_String3(object sender, RoutedEventArgs e)
        {
            if (!youPressedString)
            {
                btnString3.Background = new SolidColorBrush(Colors.Green);
                youPressedString = true;
                selectedString = 3;

                if (btnString3.Content.Equals("G"))
                {
                    this.targetFrequency = 196.0f;
                    this.lowFreq = this.targetFrequency - 30.0f;
                    this.highFreq = this.targetFrequency + 30.0f; 
                    pitchOutTarget.Text = "196.0";
                }
                else
                {
                    this.targetFrequency = 196.0f;
                    this.lowFreq = this.targetFrequency - 30.0f;
                    this.highFreq = this.targetFrequency + 30.0f;
                    pitchOutTarget.Text = "196.0";
                }
            }
            else if (selectedString==3)
            {
                btnString3.Background = new SolidColorBrush(Colors.Red);
                youPressedString = false;
            }
        }

        private void Button_Click_String4(object sender, RoutedEventArgs e)
        {
            if (!youPressedString)
            {
                btnString4.Background = new SolidColorBrush(Colors.Green);
                youPressedString = true;
                selectedString = 4;
                if (btnString4.Content.Equals("D"))
                {
                    this.targetFrequency = 146.8f;
                    this.lowFreq = this.targetFrequency - 30.0f;
                    this.highFreq = this.targetFrequency + 30.0f;
                    pitchOutTarget.Text = "146.8";
                }
            }
            else if (selectedString == 4)
            {
                btnString4.Background = new SolidColorBrush(Colors.Red);
                youPressedString = false;
            }
        }

        private void Button_Click_String5(object sender, RoutedEventArgs e)
        {
            if (!youPressedString)
            {
                btnString5.Background = new SolidColorBrush(Colors.Green);
                youPressedString = true;
                selectedString = 5;
                if (btnString5.Content.Equals("A"))
                {
                    this.targetFrequency = 110.0f;
                    this.lowFreq = this.targetFrequency - 30.0f;
                    this.highFreq = this.targetFrequency + 30.0f;
                    pitchOutTarget.Text = "110.0";
                }
            }
            else if (selectedString == 5)
            {
                btnString5.Background = new SolidColorBrush(Colors.Red);
                youPressedString = false;
            }
        }

        private void Button_Click_String6(object sender, RoutedEventArgs e)
        {
            if (!youPressedString)
            {
                btnString6.Background = new SolidColorBrush(Colors.Green);
                youPressedString = true;
                selectedString = 6;
                if (btnString6.Content.Equals("E"))
                {
                    this.targetFrequency = 82.4f;
                    this.lowFreq = this.targetFrequency - 30.0f;
                    this.highFreq = this.targetFrequency + 30.0f;
                    pitchOutTarget.Text = "82.4";
                }
                else
                {
                    this.targetFrequency = 73.4f;
                    this.lowFreq = this.targetFrequency - 30.0f;
                    this.highFreq = this.targetFrequency + 30.0f;
                    pitchOutTarget.Text = "73.4";
                }
            }
            else if (selectedString == 6)
            {
                btnString6.Background = new SolidColorBrush(Colors.Red);
                youPressedString = false;
            }
        }

    }
}