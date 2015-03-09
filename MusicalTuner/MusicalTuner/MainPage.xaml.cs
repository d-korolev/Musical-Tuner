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

        private bool recordingFFT = false;
        private bool recordingZero = false;
        private bool recordingAuto = false;
       
        private float[] buffer = new float[960];
        private int bufferIdx = 0;
        private bool youPressedMe = false;
        
        // Zero Crossing related variables

        private int zeroCrossingCounter;
        private bool zeroCrossed = false;
        private float zeroCrossFrequency;
        private float zeroCrossTime;
        private int sampleCounterBtwZeroCrossings;
        private float zeroCrossPeriod;
        private int firstPoint, lastPoint;
        private bool firstPointIs = false;
        private bool lastPointIs = false;
        private int ignoreSample = 0;

     //   private float[] bufferZeroCrossing = new float[4800];



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
            //sio.start();
            //sio.audioInEvent += sio_audioInEvent;
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
            data = filt.filter(data);
            process_audio_Auto(data, 100, 600, 1, 1);
            
        }


       

        private void process_audio_FFT(float[] data)
        {
            if (!recordingFFT)
                return;

            uint N = Convert.ToUInt32(data.Length);
            float sampleRate = sio.getInputSampleRate();
            
            fft = new FFTWrapper(N);
            float[] fftmag = fft.fftMag(data);
            // Detect and display dominant freq.
            float maxValue = fftmag.Max();
            float maxIndex = fftmag.ToList().IndexOf(maxValue);
            float detectedFrequency = maxIndex * (sampleRate / N);
            Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                this.pitchOut.Text = "Pitch: " + (detectedFrequency).ToString("#0") + " Hz";
               
            });

        }


        private void process_audio_Zero(float[] data)
        {
              if (!recordingZero)
                return;

              
              float sampleRate = sio.getInputSampleRate();

            //Filling in new 960 samples buffer
              int i = 0;
              ignoreSample = 1;
            while (bufferIdx < buffer.Length)
            {
                buffer[bufferIdx] = data[i + ignoreSample];
                bufferIdx++;
                i++;
                if (i + ignoreSample >= data.Length)
                {
                    //ignoreSample = 0;
                    return;
                }
              
            }

              uint N = Convert.ToUInt32(buffer.Length);

              for (int j = 1; j < N; j++)
              {
                  if (buffer[j] * buffer[j-1] < 0)
                  {
                      zeroCrossed = true;
                      zeroCrossingCounter ++;
                      
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
                          zeroCrossFrequency = 1 / ((sampleCounterBtwZeroCrossings * (N/sampleRate)) / N); 
                      }
                      
                  }
                  
              }

           
              Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
              {
                  this.pitchOut.Text = "Pitch: " + (zeroCrossFrequency).ToString("#0") + " Hz";

              });

        }


       
           
         // These work by shifting the signal until it seems to correlate with itself.
        // In other words if the signal looks very similar to (signal shifted 200 samples) than the fundamental period is probably 200 samples
        // Note that the algorithm only works well when there's only one prominent fundamental.
        // This could be optimized by looking at the rate of change to determine a maximum without testing all periods.
        private void process_audio_Auto(float[] input, double minHz, double maxHz, int nCandidates, int nResolution)
        {
            float sampleRate = sio.getInputSampleRate();
            float numOfChannel = sio.getInputNumChannels();
            int nLowPeriodInSamples = hzToPeriodInSamples(maxHz, sampleRate);
            int nHiPeriodInSamples = hzToPeriodInSamples(minHz, sampleRate);
            if (nHiPeriodInSamples <= nLowPeriodInSamples) throw new Exception("Bad range for pitch detection.");
            //if (numOfChannel != 1) throw new Exception("Only mono supported.");
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
                if (detectedPitch > 0)
                {
                    this.pitchOut.Text = "Pitch: " + (detectedPitch).ToString("#0.##") + " Hz";
                    //changeColor(1);    
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
            if (!youPressedMe)
            {
                btnFFT.Background = new SolidColorBrush(Colors.Green);
                youPressedMe = true;
            }
            else
            {
                btnFFT.Background = new SolidColorBrush(Colors.Red);
                youPressedMe = false;
            }
            sio.start();
            sio.audioInEvent += sio_audioInEvent_FFT;
        }

        private void TextBlock_SelectionChanged(object sender, RoutedEventArgs e)
        {

        }

        private void Button_Click_FFT(object sender, RoutedEventArgs e)
        {
          
            if (!youPressedMe)
            {
                btnFFT.Background = new SolidColorBrush(Colors.Green);
                youPressedMe = true;
                sio.start();
                sio.audioInEvent += sio_audioInEvent_FFT;
            }
            else
            {
                btnFFT.Background = new SolidColorBrush(Colors.Red);
                youPressedMe = false;
                sio.stop();
            }
            
        }

        private void Button_Click_ZeroCrossing(object sender, RoutedEventArgs e)
        {
            if (!youPressedMe)
            {
                btnZero.Background = new SolidColorBrush(Colors.Green);
              
                youPressedMe = true;
                sio.start();
                sio.audioInEvent += sio_audioInEvent_Zero;
            }
            else
            {
                btnZero.Background = new SolidColorBrush(Colors.Red);
                youPressedMe = false;
                sio.stop();
            }
            //sio.start();
            //sio.audioInEvent += sio_audioInEvent_ZeroCrossing;
        }

        private void Button_Click_Autocorrelation(object sender, RoutedEventArgs e)
        {
            if (!youPressedMe)
            {
                btnAuto.Background = new SolidColorBrush(Colors.Green);
                youPressedMe = true;
                sio.start();
                sio.audioInEvent += sio_audioInEvent_Auto;
            }
            else
            {
                btnAuto.Background = new SolidColorBrush(Colors.Red);
                youPressedMe = false;
                sio.stop();
            }
            //sio.start();
            //sio.audioInEvent += sio_audioInEvent_AutoCorrelation;
        }
    }
}
