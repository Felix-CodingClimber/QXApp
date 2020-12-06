using System;
using System.IO;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Windows.UI.ViewManagement;
using Windows.UI.Core;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.Storage;
using QXScan.Core;

namespace QXScan
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    sealed partial class App : Application
    {
        public static QXConfig Config = new QXConfig();
        public static QXLog Logger = new QXLog();
        public static Rect Bounds;

        public static string ConnectionString = Path.Combine(ApplicationData.Current.LocalFolder.Path, Config.DB);

        private bool _stateLoaded = false;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            ////Microsoft.ApplicationInsights.WindowsAppInitializer.InitializeAsync(
            ////    Microsoft.ApplicationInsights.WindowsCollectors.Metadata |
            ////    Microsoft.ApplicationInsights.WindowsCollectors.Session);

            this.InitializeComponent();

            this.Suspending += OnSuspending;
            this.UnhandledException += App_UnhandledException;

            Logger.File = Config.LogFile;
        }

        private async void App_UnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            await Logger.Write("App Exception: " + e.Message);
        }

        protected override void OnActivated(IActivatedEventArgs e)
        {
            var s = e.PreviousExecutionState;
        }         

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected async override void OnLaunched(LaunchActivatedEventArgs e)
        {
#if DEBUG
            if (System.Diagnostics.Debugger.IsAttached)
            {
                this.DebugSettings.EnableFrameRateCounter = true;
            }
#endif
            Frame rootFrame = Window.Current.Content as Frame;
                        
            SystemNavigationManager.GetForCurrentView().BackRequested += App_BackRequested;
            
            //get window size
            var appview = ApplicationView.GetForCurrentView();
            Bounds = appview.VisibleBounds;

            await this.loadState(); 

            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active
            if (rootFrame == null)
            {   
                // Create a Frame to act as the navigation context and navigate to the first page
                rootFrame = new Frame();

                rootFrame.NavigationFailed += OnNavigationFailed;

                if (e.PreviousExecutionState == ApplicationExecutionState.Terminated)
                {
                    //TODO: Load state from previously suspended application
                    // Restore values stored in app data.                   
                }

                //Mobile customization
                if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
                {
                    var statusBar = StatusBar.GetForCurrentView();

                    if (statusBar != null)
                    { 
                        await statusBar.HideAsync();
                    }
                }

                // Place the frame in the current Window
                Window.Current.Content = rootFrame;
            }

            if (e.PrelaunchActivated == false)
            {
                if (rootFrame.Content == null)
                {
                    // When the navigation stack isn't restored navigate to the first page,
                    // configuring the new page by passing required information as a navigation
                    // parameter
                    rootFrame.Navigate(typeof(MainPage), e.Arguments);
                }
                // Ensure the current window is active
                Window.Current.Activate();
            }
        }

        private void App_BackRequested(object sender, BackRequestedEventArgs e)
        {
            Frame rootFrame = Window.Current.Content as Frame;

            if (rootFrame.CanGoBack && e.Handled == false)
            {
                e.Handled = true;

                rootFrame.GoBack();
            }
            else
            {
                //Application.Current.Exit();
            }
        }

        /// <summary>
        /// Invoked when Navigation to a certain page fails
        /// </summary>
        /// <param name="sender">The Frame which failed navigation</param>
        /// <param name="e">Details about the navigation failure</param>
        async void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            await Logger.Write("Failed to load Page " + e.SourcePageType.FullName);
        }

        /// <summary>
        /// Invoked when application execution is being suspended.  Application state is saved
        /// without knowing whether the application will be terminated or resumed with the contents
        /// of memory still intact.
        /// </summary>
        /// <param name="sender">The source of the suspend request.</param>
        /// <param name="e">Details about the suspend request.</param>
        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
             
            deferral.Complete();
        }
 
        private async Task loadState()
        { 
            if (!this._stateLoaded)
            { 
                ApplicationDataContainer settings = ApplicationData.Current.LocalSettings;

                if (settings.Values.ContainsKey("conf"))
                {
                    string json = settings.Values["conf"].ToString();

                    if (!string.IsNullOrEmpty(json))
                        Config = StringHelper.Deserialize<QXConfig>(json);              
                }
                else
                {
                    // Create file; replace if exists.
                    var folder = ApplicationData.Current.LocalFolder;
                    var file = await folder.CreateFileAsync(Config.LogFile, CreationCollisionOption.ReplaceExisting);
 
                    using (var session = DbFactory.Open(ConnectionString))
                    {
                        session.CreateTable<History>();
                    }                    
                }

                this._stateLoaded = true;
            }
        }
    }
}
