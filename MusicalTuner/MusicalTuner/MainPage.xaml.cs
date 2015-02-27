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

        private FFTWrapper fft;

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
            sio.audioInEvent += sio_audioInEvent;
        }

        void sio_audioInEvent(float[] data)
        {
            recording = true;
            process_audio(data);
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
                this.pitchOut.Text = "Output Frequency: " + (detectedFrequency ).ToString("#0.##") + " KHz";
                this.octaveOut.Text = "Mag: " + maxValue;
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
