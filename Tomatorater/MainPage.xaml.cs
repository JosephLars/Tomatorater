using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Data.Json;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.System;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace Tomatorater
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();

            this.SizeChanged += MainPage_SizeChanged;

            // Set preferred window size on desktop
            // Source: http://stackoverflow.com/questions/31885979/windows-10-uwp-app-setting-window-size-on-desktop
            ApplicationView.PreferredLaunchViewSize = new Size(750, 650);
            ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.PreferredLaunchViewSize;
        }

        private void MainPage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width > e.NewSize.Height)
                VisualStateManager.GoToState(this, "Landscape", true);
            else
                VisualStateManager.GoToState(this, "Portrait", true);
        }

        public async void CallApi(string movieTitle)
        {
            //Start progrss ring
            progressRing.IsActive = true;

            //Build URI
            string url = "http://www.omdbapi.com/?y=&plot=short&type=movie&tomatoes=true&r=json&t=" + movieTitle;

            try
            {
                //Send GET request
                var client = new System.Net.Http.HttpClient();
                var response = await client.GetAsync(url);

                //Respond to GET request
                if (response.IsSuccessStatusCode)
                {
                    //Parse to JSON string
                    string json = await response.Content.ReadAsStringAsync();
                    JsonObject movieInfo = JsonObject.Parse(json);

                    if (movieInfo.GetNamedString("Response") == "False")
                    {
                        Debug.WriteLine("No result");
                        return;
                    }
                    else if (movieInfo.GetNamedString("Title").ToLower().Replace("  ", "") != suggestBox.Text.ToLower().Replace("  ", ""))
                    {
                        Debug.WriteLine("Out of scope");
                        return;
                    }
                    else if (!string.IsNullOrWhiteSpace(suggestBox.Text) && movieInfo.GetNamedString("Response") == "True")
                    {
                        //Success :D
                        RatingDisplay.Visibility = Visibility.Visible;
                        TomatoImage.Visibility = Visibility.Visible;
                        PopcornImage.Visibility = Visibility.Visible;

                        MirrorBox.Visibility = Visibility.Collapsed;
                        MovieTitleBox.Visibility = Visibility.Visible;

                        MovieTitle.Text = movieInfo.GetNamedString("Title") + " (" + movieInfo.GetNamedString("Year") + ")";
                        tomatoMeter.Text = movieInfo.GetNamedString("tomatoMeter") + "%";
                        tomatoUserMeter.Text = movieInfo.GetNamedString("tomatoUserMeter") + "%";
                        if (movieInfo.GetNamedString("tomatoImage") == "certified")
                            CertIm.Visibility = Visibility.Visible;
                        else
                            CertIm.Visibility = Visibility.Collapsed;

                        if (movieInfo.GetNamedString("tomatoMeter") == "N/A")
                            TomatoImage.Visibility = Visibility.Collapsed;
                        else if (Int32.Parse(movieInfo.GetNamedString("tomatoMeter")) >= 60)
                            TomatoImage.Source = new BitmapImage(new Uri("ms-appx:///Images/fresh.png"));
                        else
                            TomatoImage.Source = new BitmapImage(new Uri("ms-appx:///Images/rotten.png"));

                        if (movieInfo.GetNamedString("tomatoUserMeter") == "N/A")
                            PopcornImage.Visibility = Visibility.Collapsed;
                        else if (Int32.Parse(movieInfo.GetNamedString("tomatoUserMeter")) >= 60)
                            PopcornImage.Source = new BitmapImage(new Uri("ms-appx:///Images/popcorn.png"));
                        else
                            PopcornImage.Source = new BitmapImage(new Uri("ms-appx:///Images/spilt.png"));
                    }
                }
                else
                {
                    //Connection Error
                    Debug.WriteLine("No response");
                }
            }
            catch (HttpRequestException e)
            {
                //No Internet
                //display.Text = "Check your connection";
            }
            finally
            {
                //End progrss ring
                progressRing.IsActive = false;
            }
        }

        public async void CallAutoSuggestApi(string movieTitle)
        {
            try
            {
                //Build URI
                //string url = "http://api.themoviedb.org/3/search/movie?api_key=4049439bdbe69a57684251f2362857d9&search_type=ngram&query=" + movieTitle;
                string url = "http://www.omdbapi.com/?type=movie&r=json&s=" + movieTitle;

                //Send GET request
                HttpClient client = new System.Net.Http.HttpClient();
                HttpResponseMessage response = await client.GetAsync(url);

                //Respond to GET request
                if (response.IsSuccessStatusCode)
                {
                    //Parse to JSON string
                    string json = await response.Content.ReadAsStringAsync();

                    CreateAutoComplete(json);
                }
                else
                {
                    //Connection Error
                    Debug.WriteLine("Error occured, the status code is: {0}", response.StatusCode);
                }
            }
            catch (HttpRequestException e)
            {
                //Connection Error
                Debug.WriteLine("No response");
            }

        }

        private void CreateAutoComplete(string json)
        {
            JsonObject movieInfo = JsonObject.Parse(json);

            //retun empty string if false response
            if (movieInfo.ContainsKey("Response"))
                if (movieInfo.GetNamedValue("Response").GetString() == "False")
                {
                    suggestBox.ItemsSource = new List<string>();
                    return;
                }

            //Get list of titles
            var x = movieInfo.GetNamedArray("Search");
            var t = x.Select(y => {

                var jsonObject = y.GetObject();

                return jsonObject.GetNamedValue("Title").GetString();

            }).ToList();

            //Assign list to autocomplete box
            suggestBox.ItemsSource = t.Take(5);
        }

        private void suggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            RatingDisplay.Visibility = Visibility.Collapsed;
            MovieTitleBox.Visibility = Visibility.Collapsed;
            if (string.IsNullOrWhiteSpace(suggestBox.Text))
            {
                //Go to home layout
                MirrorBox.Visibility = Visibility.Collapsed;
                //Header.Visibility = Visibility.Visible;
                Caption.Visibility = Visibility.Visible;

                suggestBox.IsSuggestionListOpen = false;
                progressRing.IsActive = false;
            }
            else
            {
                //Go to editing layout
                //Header.Visibility = Visibility.Collapsed;
                Caption.Visibility = Visibility.Collapsed;
                MirrorBox.Visibility = Visibility.Visible;

                if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
                {
                    CallAutoSuggestApi(sender.Text);
                }

            }
        }

        private void suggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            Debug.WriteLine("QuerySubmitted");
            CallApi(sender.Text);
        }

        private void suggestBox_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                suggestBox.IsEnabled = false;
                suggestBox.IsEnabled = true;
            }
        }
    }
}
