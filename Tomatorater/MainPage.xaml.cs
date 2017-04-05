using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
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
        bool suggestBoxFocused = true;
        public MainPage()
        {
            this.InitializeComponent();

            this.SizeChanged += MainPage_SizeChanged;
            Window.Current.CoreWindow.CharacterReceived += coreWindow_CharacterReceived;

            // Set preferred window size on desktop
            // Source: http://stackoverflow.com/questions/31885979/windows-10-uwp-app-setting-window-size-on-desktop
            ApplicationView.PreferredLaunchViewSize = new Size(750, 650);
            ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.PreferredLaunchViewSize;
        }

        private async void Application_Resuming(object sender, object o)
        {
            // Handle global application events only if this page is active
            if (Frame.CurrentSourcePageType == typeof(MainPage))
            {
                await SetupUiAsync();
            }
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            await SetupUiAsync();
        }

        /// <summary>
        /// When a character is pressed outside of suggestBox, focus box and insert into box
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void coreWindow_CharacterReceived(CoreWindow sender, CharacterReceivedEventArgs args)
        {
            if (!suggestBoxFocused)
            {
                suggestBox.Focus(FocusState.Programmatic);
                if (args.KeyCode != 8)   //fixes backspace bug
                    suggestBox.Text += (char)args.KeyCode;
                else if (suggestBox.Text.Length >= 1)
                    suggestBox.Text = suggestBox.Text.Remove(suggestBox.Text.Length -1);
                    
            }
        }

        private void MainPage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width > e.NewSize.Height)
                VisualStateManager.GoToState(this, "Landscape", true);
            else
                VisualStateManager.GoToState(this, "Portrait", true);
        }

        /// <summary>
        /// Scrape movie ratings from HTML
        /// </summary>
        /// <param name="movieTitle"></param>
        private async void ScrapeRatings(string movieTitle)
        {
            //Start progrss ring
            progressRing.IsActive = true;

            HtmlWeb web = new HtmlWeb();
            string url = "https://www.rottentomatoes.com/m/" + Sanitize(movieTitle) + "/";
            var doc = await web.LoadFromWebAsync(url);
            string tomatoMeter = doc.DocumentNode
                .Descendants()
                .First(o => o.GetAttributeValue("id", "") == "tomato_meter_link")
                .Descendants()
                .Where(e => e.Name == "span").Skip(1).Take(1).Single().InnerText;

            string audienceScore = doc.DocumentNode
                .Descendants()
                .First(o => o.GetAttributeValue("id", "") == "scorePanel")
                .Elements("div").Skip(1).Take(1).Single()
                .Elements("div").Take(1).Single()
                .Elements("a").Take(1).Single()
                .Elements("div").Take(1).Single()
                .Elements("div").Skip(1).Take(1).Single()
                .Elements("div").Take(1).Single()
                .Elements("span").Take(1).Single().InnerText;

            string title = doc.DocumentNode
                .Descendants()
                .First(o => o.GetAttributeValue("id", "") == "heroImageContainer")
                .Elements("div").Take(1).Single()
                .Elements("h1").Take(1).Single().InnerText.Trim();

            string tomatoMeterIcon = doc.DocumentNode
                .Descendants()
                .First(o => o.GetAttributeValue("id", "") == "tomato_meter_link")
                .Elements("span").Take(1).Single().OuterHtml.ToString();
            if (tomatoMeterIcon.Contains("certified_fresh"))
                tomatoMeterIcon = "certified_fresh";
            else if (tomatoMeterIcon.Contains("fresh"))
                tomatoMeterIcon = "fresh";
            else
                tomatoMeterIcon = "rotten";

            //End progrss ring
            progressRing.IsActive = false;


            //Display the scores :D
            RatingDisplay.Visibility = Visibility.Visible;
            TomatoImage.Visibility = Visibility.Visible;
            PopcornImage.Visibility = Visibility.Visible;

            MirrorBox.Visibility = Visibility.Collapsed;
            MovieTitleBox.Visibility = Visibility.Visible;

            //MovieTitle.Text = movieInfo.GetNamedString("Title") + " (" + movieInfo.GetNamedString("Year") + ")";
            MovieTitle.Text = title;
            this.tomatoMeter.Text = tomatoMeter;
            tomatoUserMeter.Text = audienceScore;

            
            if (tomatoMeterIcon == "certified_fresh")
                TomatoImage.Source = new BitmapImage(new Uri("ms-appx:///Images/CF_120x120.png"));
            else if (tomatoMeterIcon == "fresh")
                TomatoImage.Source = new BitmapImage(new Uri("ms-appx:///Images/fresh.png"));
            else if (tomatoMeterIcon == "rotten")
                TomatoImage.Source = new BitmapImage(new Uri("ms-appx:///Images/rotten.png"));
            else
                TomatoImage.Visibility = Visibility.Collapsed;
            
        }

        /// <summary>
        /// Cleans up the string - replaces spaces with underscores, converts to lowercase, removes stange characters, trims ends
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        private string Sanitize(string str)
        {
            str = str.Trim();
            str = str.Replace(" ", "_").ToLower();
            str = System.Text.RegularExpressions.Regex.Replace(str, @"[^\w\.@-]", "");
            return str;
        }

        /// <summary>
        /// Get movie ratings
        /// </summary>
        /// <param name="movieTitle"></param>
        private async void CallApi(string movieTitle)
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
                        tomatoMeter.Text = movieInfo.GetNamedArray("Ratings").GetObjectAt(1).GetNamedString("Value");
                        tomatoUserMeter.Text = movieInfo.GetNamedArray("Ratings").GetObjectAt(0).GetNamedString("Value");
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

        private async void CallAutoSuggestApi(string movieTitle)
        {
            try
            {
                //Build URI
                //string url = "http://api.themoviedb.org/3/search/movie?api_key=4049439bdbe69a57684251f2362857d9&search_type=ngram&query=" + movieTitle;
                //string url = "http://www.omdbapi.com/?type=movie&r=json&s=" + movieTitle;
                string url = "https://www.rottentomatoes.com/api/private/v2.0/search?t=movie&limit=5&q=" + Uri.EscapeDataString(movieTitle);

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

        /// <summary>
        /// Creates list of movies from JSON input to send to suggestBox
        /// </summary>
        /// <param name="json"></param>
        private void CreateAutoComplete(string json)
        {
            JsonObject movieInfo = JsonObject.Parse(json);

            //Get list of titles
            var x = movieInfo.GetNamedArray("movies");
            var t = x.Select(y => {

                var jsonObject = y.GetObject();

                return jsonObject.GetNamedValue("name").GetString();

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
            //CallApi(sender.Text);
            ScrapeRatings(sender.Text);
        }

        /// <summary>
        /// Drops virtual keyboard when enter is pressed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void suggestBox_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                HideKeyboard();
            }
        }

        /// <summary>
        /// Hides the virtual keyboard if open
        /// </summary>
        private void HideKeyboard()
        {
            suggestBox.IsEnabled = false;
            suggestBox.IsEnabled = true;
        }

        private void suggestBox_GotFocus(object sender, RoutedEventArgs e)
        {
            suggestBoxFocused = true;
        }

        private void suggestBox_LostFocus(object sender, RoutedEventArgs e)
        {
            suggestBoxFocused = false;
        }

        /// <summary>
        /// Customize StatusBar & TitleBar
        /// Add a reference to mobile extensions = https://social.msdn.microsoft.com/Forums/sqlserver/en-US/e4ea5195-4335-47c4-ab96-55680f030f8a/where-is-windowsuiviewmanagementstatusbar-class-c-uwp?forum=wpdevelop
        /// Sample code = https://blogs.msdn.microsoft.com/gianlucb/2015/10/08/uwp-windows-10-app-titlebar-and-status-bar-customization/
        /// Sample implimentation = https://github.com/Microsoft/Windows-universal-samples/blob/93bdfb92b3da76f2e49c959807fc5643bf0940c9/Samples/CameraStarterKit/cs/MainPage.xaml.cs
        /// </summary>
        private async Task SetupUiAsync()
        {
            //PC customization
            if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.ApplicationView"))
            {
                var titleBar = ApplicationView.GetForCurrentView().TitleBar;
                if (titleBar != null)
                {
                    titleBar.ButtonBackgroundColor = Colors.Red;
                    titleBar.ButtonForegroundColor = Colors.White;
                    titleBar.BackgroundColor = Colors.Red;
                    titleBar.ForegroundColor = Colors.White;

                    titleBar.ButtonInactiveBackgroundColor = Color.FromArgb(255, 255, 100, 100);
                    titleBar.InactiveBackgroundColor = Color.FromArgb(255, 255, 100, 100);
                    titleBar.InactiveForegroundColor = Colors.White;
                }
            }

            //Mobile customization
            if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
            {
                var statusBar = StatusBar.GetForCurrentView();
                if (statusBar != null)
                {
                    statusBar.BackgroundOpacity = 1;
                    statusBar.BackgroundColor = Colors.Red;
                    statusBar.ForegroundColor = Colors.White;
                }
            }
        }
    }
}
