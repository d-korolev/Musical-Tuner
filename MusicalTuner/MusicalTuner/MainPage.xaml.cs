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

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=391641

namespace MusicalTuner
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    /// 



    public sealed partial class MainPage : Page
    {

        private string value = "";
        public MainPage()
        {
            this.InitializeComponent();

            this.NavigationCacheMode = NavigationCacheMode.Required;
        }

      
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            //ComboBox comboBoxGuitar = new ComboBox();
            //comboBoxGuitar.SelectionChanged += GuiterTunesCombo_SelectionChanged;
            

        }

        private void TuneDropList(object sender, DragEventArgs e)
        {

        }

        private void GuiterTunesCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            value = ((ComboBoxItem)GuiterTunesCombo)

            if (Standard.IsSelected == true)
            {
                btnNote1.Content = "E";
            }
            else
            {
                btnNote1.Content = "failed";
            }
            
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
          
        }
    }
}
