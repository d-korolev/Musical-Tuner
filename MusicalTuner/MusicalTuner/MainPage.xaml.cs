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
using Windows.UI.Core;


namespace MusicalTuner
{
    

    public sealed partial class MainPage : Page
    {
        private SoundIO sio;

        // Create filter design and filter objects
        FilterDesign fd;
        Filter filt;

        private FFTWrapper fft;

        private bool recording = false;
        private int samples_to_wait = 0;
        public MainPage()
        {
            this.InitializeComponent();

            this.NavigationCacheMode = NavigationCacheMode.Required;
        }

      
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            // Create sound input
            sio = new SoundIO();
            sio.start();
            sio.audioInEvent += sio_audioInEvent;

            // Initialize FilterDesign object, create a filter impulse response, then wrap that in a Filter object
            fd = new FilterDesign();
            float[] impulseResponse = fd.FIRDesignWindowed(0.0f, 0.2f, WindowType.HAMMING);
            filt = new Filter(impulseResponse);
        }

        void sio_audioInEvent(float[] data)
        {
            recording = true;
            data = filt.filter(data);
            detectPitchCalculation(data,100.0, 1000, 1, 1);
            //process_audio(data);
        }

       
        // Here we start recording, so we wait for 4800 more samples to pass, then check FFTs
        private void start_recording(int idx)
        {
            samples_to_wait = 480 - idx;
            recording = true;
        }

        private void process_audio(float[] data)
        {
            if (!recording)
                return;

            uint N = Convert.ToUInt32(data.Length);
            float sampleRate = sio.getInputSampleRate();

            fft = new FFTWrapper(N);
            float[] fftmag = fft.fftMag(data);
            //// Detect Freq.
            float maxValue = fftmag.Max();
            float maxIndex = fftmag.ToList().IndexOf(maxValue);
            float detectedFrequency = maxIndex * (sampleRate / N);
            Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (detectedFrequency>440)
                {
                    this.pitchOut.Text = "Output Frequency: " + (detectedFrequency).ToString("#0.##") + " Hz";
                    this.octaveOut.Text = "Mag: " + maxValue;
                }

            });

        }

        private int getMaxIdx(float[] input)
        {
            int maxIdx = 0;
            for (int i = 1; i < input.Length; ++i)
            {
                if (Math.Abs(input[i]) > Math.Abs(input[maxIdx]))
                    maxIdx = i;
            }
            return maxIdx;
        }


        // These work by shifting the signal until it seems to correlate with itself.
        // In other words if the signal looks very similar to (signal shifted 200 samples) than the fundamental period is probably 200 samples
        // Note that the algorithm only works well when there's only one prominent fundamental.
        // This could be optimized by looking at the rate of change to determine a maximum without testing all periods.
        private void detectPitchCalculation(float[] input, double minHz, double maxHz, int nCandidates, int nResolution)
        {
            float sampleRate = sio.getInputSampleRate();
            float numOfChannel = sio.getInputNumChannels();
            int nLowPeriodInSamples = hzToPeriodInSamples(maxHz, sampleRate);
            int nHiPeriodInSamples = hzToPeriodInSamples(minHz, sampleRate);
            if (nHiPeriodInSamples <= nLowPeriodInSamples) throw new Exception("Bad range for pitch detection.");
            if (numOfChannel != 1) throw new Exception("Only mono supported.");
            float[] samples = input;
            if (samples.Length < nHiPeriodInSamples) throw new Exception("Not enough samples.");

            // both algorithms work in a similar way
            // they yield an array of data, and then we find the index at which the value is highest.
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
            int[] bestIndices = findBestCandidates(nCandidates, ref results); //note findBestCandidates modifies parameter
            // convert back to Hz
            float[] res = new float[nCandidates];
            for (int i = 0; i < nCandidates; i++)
                res[i] = periodInSamplesToHz(bestIndices[i] + nLowPeriodInSamples, sampleRate);
            float detectedPitch = res[0];
            Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (detectedPitch > 440)
                {
                    this.pitchOut.Text = "Output Frequency: " + (detectedPitch).ToString("#0.##") + " Hz";
                }
            });

        }
        private static int[] findBestCandidates(int n, ref double[] inputs)
        {
            if (inputs.Length < n) throw new Exception("Length of inputs is not long enough.");
            int[] res = new int[n]; // will hold indices with the highest amounts.

            for (int c = 0; c < n; c++)
            {
                // find the highest.
                double fBestValue = double.MinValue;
                int nBestIndex = -1;
                for (int i = 0; i < inputs.Length; i++)
                    if (inputs[i] > fBestValue) { nBestIndex = i; fBestValue = inputs[i]; }

                // record this highest value
                res[c] = nBestIndex;

                // now blank out that index.
                inputs[nBestIndex] = double.MinValue;
            }
            return res;
        }


        private static int hzToPeriodInSamples(double hz, float sampleRate)
        {
            return (int)(1 / (hz / (double)sampleRate));
        }
        private static float periodInSamplesToHz(int period, float sampleRate)
        {
            return 1 / (period / sampleRate);
        }

        private void GuiterTunesCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
          

            if (Standard.IsSelected == true)
            {
                btnNote1.Content = "E";
                btnNote2.Content = "B";
                btnNote3.Content = "G";
                btnNote4.Content = "D";
                btnNote5.Content = "A";
                btnNote6.Content = "E";

            }
            else if (EDrop.IsSelected == true)
            {
                btnNote1.Content = "E";
                btnNote2.Content = "B";
                btnNote3.Content = "G";
                btnNote4.Content = "D";
                btnNote5.Content = "A";
                btnNote6.Content = "D";
            }
            else
            {
                btnNote1.Content = "D";
                btnNote2.Content = "A";
                btnNote3.Content = "D";
                btnNote4.Content = "G";
                btnNote5.Content = "A";
                btnNote6.Content = "D";
            }
            
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
          
        }

        private void TextBlock_SelectionChanged(object sender, RoutedEventArgs e)
        {

        }

        private void Button_Click_FFT(object sender, RoutedEventArgs e)
        {

        }

        private void Button_Click_ZeroCrossing(object sender, RoutedEventArgs e)
        {

        }

        private void Button_Click_Autocorrelation(object sender, RoutedEventArgs e)
        {

        }
    }
}
