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
                if (args.KeyCode == 27)  //ignores esc key
                    return;
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
        /// Displays a Movie on the screen
        /// </summary>
        /// <param name="movie"></param>
        private void DisplayMovie(Movie movie)
        {
            //Set up views
            RatingDisplay.Visibility = Visibility.Visible;
            TomatoImage.Visibility = Visibility.Visible;
            PopcornImage.Visibility = Visibility.Visible;
            MirrorBox.Visibility = Visibility.Collapsed;
            MovieTitleBox.Visibility = Visibility.Visible;

            //Print attributes
            MovieTitle.Text = movie.Title + " ("+ movie.Year + ")";
            tomatoMeter.Text = movie.MeterScore.ToString() + "%";
            tomatoUserMeter.Text = movie.AudienceScore.ToString() + "%";
            
            //Display Tomatometer
            if (movie.MeterClass == "certified_fresh")
                TomatoImage.Source = new BitmapImage(new Uri("ms-appx:///Images/CF_120x120.png"));
            else if (movie.MeterClass == "fresh")
                TomatoImage.Source = new BitmapImage(new Uri("ms-appx:///Images/fresh.png"));
            else if (movie.MeterClass == "rotten")
                TomatoImage.Source = new BitmapImage(new Uri("ms-appx:///Images/rotten.png"));
            else if (movie.MeterClass == "N/A") //probably upcoming movie
            {
                TomatoImage.Visibility = Visibility.Collapsed;
                tomatoMeter.Text = "n/a";
            }
            else //tomatometer not avialiable (probably upcoming movie)
                TomatoImage.Visibility = Visibility.Collapsed;

            //Display Audience Score
            if (movie.AudienceClass == "want")
            {
                audienceTitle.Text = "WANT TO SEE";
                PopcornImage.Source = new BitmapImage(new Uri("ms-appx:///Images/want.png"));
            }
            else if (movie.AudienceClass == "upright")
            {
                audienceTitle.Text = "AUDIENCE SCORE";
                PopcornImage.Source = new BitmapImage(new Uri("ms-appx:///Images/popcorn.png"));
            }
            else if (movie.AudienceClass == "spilled")
            {
                audienceTitle.Text = "AUDIENCE SCORE";
                PopcornImage.Source = new BitmapImage(new Uri("ms-appx:///Images/spilt.png"));
            }
            else if (movie.AudienceClass == "N/A")
            {
                audienceTitle.Text = "AUDIENCE SCORE";
                PopcornImage.Visibility = Visibility.Collapsed;
                tomatoUserMeter.Text = "not yet";
            }
            else
                PopcornImage.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Scrape movie ratings from HTML
        /// </summary>
        /// <param name="movieTitle"></param>
        private async void ScrapeRatings(Movie movie)
        {
            //Start progrss ring
            progressRing.IsActive = true;

            //Retrieve HTML for scraping
            HtmlWeb web = new HtmlWeb();
            string url = "https://www.rottentomatoes.com" + movie.Url;
            var doc = await web.LoadFromWebAsync(url);

            //Scrape audience class - *[@id="scorePanel"]/div[2]/h3/text()
            string audienceClass = doc.DocumentNode
                .Descendants()
                .First(o => o.GetAttributeValue("id", "") == "scorePanel")
                .Elements("div").Skip(1).Take(1).Single()
                .Elements("h3").Take(1).Single().InnerText.Trim();
            if (audienceClass == "Want to See")
                movie.AudienceClass = "want";

            //Scrape audience score
            try
            {
                string audienceScoreString = doc.DocumentNode
                .Descendants()
                .First(o => o.GetAttributeValue("id", "") == "scorePanel")
                .Elements("div").Skip(1).Take(1).Single()
                .Elements("div").Take(1).Single()
                .Elements("a").Take(1).Single()
                .Elements("div").Take(1).Single()
                .Elements("div").Skip(1).Take(1).Single()
                .Elements("div").Take(1).Single()
                .Elements("span").Take(1).Single().InnerText;
                movie.AudienceScore = Convert.ToInt32(audienceScoreString.Replace("%", ""));
            }
            catch (InvalidOperationException e)
            {
                movie.AudienceClass = "N/A";
            }

            //End progrss ring
            progressRing.IsActive = false;

            //Display the scores :D
            DisplayMovie(movie);
        }

        private async void CallAutoSuggestApi(string movieTitle)
        {
            try
            {
                //Build URI
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

        private async void CallSearchApi(string movieTitle)
        {
            try
            {
                //Build URI
                string url = "https://www.rottentomatoes.com/api/private/v2.0/search?t=movie&limit=1&q=" + Uri.EscapeDataString(movieTitle);

                //Send GET request
                HttpClient client = new System.Net.Http.HttpClient();
                HttpResponseMessage response = await client.GetAsync(url);

                //Respond to GET request
                if (response.IsSuccessStatusCode)
                {
                    //Parse to JSON string
                    string json = await response.Content.ReadAsStringAsync();

                    ScrapeRatings(ExtractMovie(json));
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

                Movie movie = new Movie();
                movie.Title = jsonObject.GetNamedValue("name").GetString();
                if (jsonObject.GetNamedValue("year").ValueType == JsonValueType.Number)
                    movie.Year = (int)jsonObject.GetNamedValue("year", JsonValue.CreateNumberValue(0)).GetNumber();
                else
                    movie.Year = 0;
                movie.Url = jsonObject.GetNamedValue("url").GetString();
                movie.MeterClass = jsonObject.GetNamedValue("meterClass").GetString();
                movie.MeterScore = (int)jsonObject.GetNamedValue("meterScore", JsonValue.CreateNumberValue(0)).GetNumber();
                return movie;

            }).ToList();

            //Assign list to autocomplete box
            suggestBox.ItemsSource = t.Take(5);
        }

        private Movie ExtractMovie(string json)
        {
            JsonObject movieInfo = JsonObject.Parse(json);
            JsonArray movies = movieInfo.GetNamedArray("movies");

            Movie movie = new Movie();
            movie.Title = movies.GetObjectAt(0).GetNamedValue("name").GetString();
            if (movies.GetObjectAt(0).GetNamedValue("year").ValueType == JsonValueType.Number)
                movie.Year = (int)movies.GetObjectAt(0).GetNamedValue("year", JsonValue.CreateNumberValue(0)).GetNumber();
            else
                movie.Year = 0;
            movie.Url = movies.GetObjectAt(0).GetNamedValue("url").GetString();
            movie.MeterClass = movieInfo.GetNamedArray("movies").GetObjectAt(0).GetNamedValue("meterClass").GetString();
            movie.MeterScore = (int)movieInfo.GetNamedArray("movies").GetObjectAt(0).GetNamedValue("meterScore", JsonValue.CreateNumberValue(0)).GetNumber();
            return movie;
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
            if (args.ChosenSuggestion != null)
            {
                // User selected an item from the suggestion list, take an action on it here.
                ScrapeRatings((Movie)args.ChosenSuggestion);
            }
            else
            {
                // Use args.QueryText to determine what to do.
                // Construct Movie
                CallSearchApi(args.QueryText);
            }
            Debug.WriteLine("QuerySubmitted");
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
