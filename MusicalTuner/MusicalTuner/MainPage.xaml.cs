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


namespace MusicalTuner
{
    

    public sealed partial class MainPage : Page
    {
        private SoundIO sio;
        private AudioTool at;


        private FFTWrapper fft;

        // This is our matched filter that we will use to search for the initialization sequence
        private Filter matchedFilt;

        private bool recording = false;
        private int samples_to_wait = 0;
        private float[] buffer = new float[10 * 480];
        private int bufferIdx = 0;

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

            // Create our audio tool so that we can generate the synchronization code in the appropriate format
            at = new AudioTool(sio.getInputNumChannels(), sio.getInputSampleRate());

            sio.audioInEvent += (float[] data) =>
            {
                process_audio(data);
            };



        }


        // Here we start recording, so we wait for 4800 more samples to pass, then check FFTs
        private void start_recording(int idx)
        {
            samples_to_wait = 3 * 480 - idx;
            recording = true;
        }

        private void process_audio(float[] data)
        {
            if (!recording)
                return;

            // Wait until samples_to_wait is zero, then start feeding samples into our buffer
            if (samples_to_wait >= data.Length)
            {
                // If this is the case, we're not getting anything from this buffer
                samples_to_wait -= data.Length;
                return;
            }

            // Otherwise, fill up buffer until it's full
            int i = 0;
            while (bufferIdx < buffer.Length)
            {
                buffer[bufferIdx] = data[i + samples_to_wait];
                bufferIdx++;
                i++;
                if (i + samples_to_wait >= data.Length)
                {
                    samples_to_wait = 0;
                    return;
                }
            }

            // Now that we have waited long enough, let's take the FFT of the next buffer, display it to the user, and decide if it's a one or a zero
            float[] BUFFER = fft.fftMag(buffer);
            bufferIdx = 0;
            recording = false;

            uint N = Convert.ToUInt32(data.Length);
            float sampleRate = sio.getInputSampleRate();

            //// Detect Freq.
            float maxValue = BUFFER.Max();
            float maxIndex = BUFFER.ToList().IndexOf(maxValue);
            float detectedFrequency = maxIndex * (sampleRate / N);

            Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                this.pitchOut.Text = "Output Frequency: " + (detectedFrequency / 1000.0).ToString("#0.##") + " KHz";
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
    }
}
