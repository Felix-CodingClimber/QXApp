using System;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.Devices.Sensors;
using Windows.Graphics.Display;
using Windows.Phone.Devices.Notification;
using Windows.Storage;
using Windows.System.Display;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using ZXing;
using ZXing.Client.Result;
using QXScan.Core;
using SQLite.Net;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace QXScan
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private readonly DisplayInformation _displayInformation = DisplayInformation.GetForCurrentView();
        private readonly SimpleOrientationSensor _orientationSensor = SimpleOrientationSensor.GetDefault();

        private SimpleOrientation _deviceOrientation = SimpleOrientation.NotRotated;
        private DisplayOrientations _displayOrientation = DisplayOrientations.Portrait;

        private readonly DisplayRequest _displayRequest = new DisplayRequest();
        private QXScanner scanner;

        private VibrationDevice vibDevice;
        private DeviceFormFactorType deviceType;

        private StreamResolution resolution;
        private SQLiteConnection session;

        private bool _stateLoad = false;
        private bool _exit = false;

        private string _code = "";

        public MainPage()
        {
            this.InitializeComponent();

            Window.Current.Activated += Current_Activated;

            // Useful to know when to initialize/clean up the camera
            Application.Current.Suspending += Application_Suspending;
            Application.Current.Resuming += Application_Resuming;

            // Do not cache the state of the UI when suspending/navigating
            NavigationCacheMode = NavigationCacheMode.Disabled;
        }

        private void Current_Activated(object sender, WindowActivatedEventArgs e)
        {
            switch (e.WindowActivationState)
            {
                case CoreWindowActivationState.CodeActivated:
                case CoreWindowActivationState.PointerActivated:
                    if (this.scanner != null)
                    {
                        this.scanner.Resume();
                    }

                    break;

                case CoreWindowActivationState.Deactivated:
                    if (this.scanner != null)
                    {
                        this.scanner.Suspend();
                    }

                    break;
            }
        }

        private async void Application_Suspending(object sender, SuspendingEventArgs e)
        {
            // Handle global application events only if this page is active
            if (Frame.CurrentSourcePageType == typeof(MainPage))
            {
                var deferral = e.SuspendingOperation.GetDeferral();

                await this.scanner.Stop();

                await this.Clean_Scanner();
                
                deferral.Complete();
            }
        }

        private async void Application_Resuming(object sender, object o)
        {
            // Handle global application events only if this page is active
            if (Frame.CurrentSourcePageType == typeof(MainPage))
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    this.loadState();
                }); 

                this._stateLoad = true;

                await this.Create_Scanner();
            }
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                this.loadState();
            }); 

            this._stateLoad = true;

            this.setUI();

            await this.Create_Scanner();
        }

        protected async override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            this._exit = true;
                
            await this.scanner.Stop();

            await this.Clean_Scanner();            
        }

        private async Task Create_Scanner()
        {
            try
            {
                this.session = DbFactory.Open(App.ConnectionString);

                this.scanner = new QXScanner();

                if (this.deviceType == DeviceFormFactorType.Phone)
                {
                    this.scanner.IsMobile = true;
                    this.vibDevice = VibrationDevice.GetDefault();
                } 

                this.scanner.IsFront = false;        
                this.scanner.Interval = 300;

                this.scanner.CaptureStart += Scanner_CaptureStart;
                this.scanner.CaptureImage += Scanner_CaptureImage;      
                this.scanner.CaptureFail += Scanner_CaptureFail;

                await this.scanner.SetPreviewAsync(this.PreviewControl);

                await this.scanner.Start(_displayInformation.CurrentOrientation);
            }
            catch (Exception ex)
            {
                await App.Logger.Write("Initialize Fail: " + ex.Message);

                //camera error
                var dialog = new MessageDialog(ex.Message, "Initialize Fail");

                dialog.Commands.Add(new UICommand("OK") { Id = 0 });

                var result = await dialog.ShowAsync();

                var action = (int)result.Id;
            }
        }

        private async void Scanner_CaptureFail(object sender, CaptureEventArgs e)
        {
            await App.Logger.Write("Scan: " + e.Text);

            if (!this._exit)
            {
                //camera error
                var dialog = new MessageDialog(e.Text, "Scan error");

                dialog.Commands.Add(new UICommand("OK") { Id = 0 });

                var result = await dialog.ShowAsync();

                var action = (int)result.Id;
            }
        }

        private async Task Clean_Scanner()
        { 
            if (this.scanner != null)
            {
                this.scanner.CaptureStart -= Scanner_CaptureStart;
                this.scanner.CaptureImage -= Scanner_CaptureImage;        
                this.scanner.CaptureFail -= Scanner_CaptureFail;

                this.scanner.Dispose();
                this.scanner = null;
            }

            if (this.session != null)
            {
                this.session.Close();
                this.session = null;
            }

            _displayInformation.OrientationChanged -= DisplayInformation_OrientationChanged;

            DisplayInformation.AutoRotationPreferences = DisplayOrientations.None;

            if (this._orientationSensor != null)
            {
                this._orientationSensor.OrientationChanged -= OrientationSensor_OrientationChanged;
            }

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                // Cleanup the UI
                this.PreviewControl.Source = null;

                // Allow the device screen to sleep now that the preview is stopped
                _displayRequest.RequestRelease();
            });
        }
         
        private async void Scanner_CaptureStart(object sender, CaptureInitializeEventArgs e)
        {
            //video camera frame resolution
            this.resolution = e.Resolution;

            try
            {
                _displayRequest.RequestActive();
            }
            catch (Exception ex)
            {
                await App.Logger.Write("Start: " + ex.Message);
            }
        }

        private async void Scanner_Fail(object sender, CaptureEventArgs e)
        {
            await App.Logger.Write("Scan error: " + e.Text);

            if (!this._exit)
            {
                //camera error
                var dialog = new MessageDialog(e.Text);

                dialog.Title = "Scan error";
                dialog.Commands.Add(new UICommand("OK") { Id = 0 });

                var result = await dialog.ShowAsync();

                var action = (int)result.Id;
            }
        }

        private async void Scanner_CaptureImage(object sender, CaptureResultEventArgs e)
        {
            if (e.Result == null)
            {
                this.focusStory.Stop();

                this._code = "";
            }
            else
            {
                this.qrText.Text = e.Result.Text;

                this.focusStory.Begin();

                if (this._code != e.Result.Text)
                {
                    this._code = e.Result.Text;

                    if (App.Config.Sound)
                        this.sound.Play();

                    if (App.Config.Vibrate && this.vibDevice != null)
                    {
                        this.vibDevice.Vibrate(TimeSpan.FromMilliseconds(200));
                    } 

                    this.saveToHistory(e.Result);
                }
            }
        } 

        private async void OrientationSensor_OrientationChanged(SimpleOrientationSensor sender, SimpleOrientationSensorOrientationChangedEventArgs args)
        {
            if (args.Orientation != SimpleOrientation.Faceup && args.Orientation != SimpleOrientation.Facedown)
            {
                _deviceOrientation = args.Orientation;

                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => this.UpdateButtonOrientation());
            }
        }

        private void setUI()
        {
            DisplayInformation.AutoRotationPreferences = DisplayOrientations.Portrait;

            // Populate orientation variables with the current state
            _displayOrientation = _displayInformation.CurrentOrientation;

            if (_orientationSensor != null)
            {
                _deviceOrientation = _orientationSensor.GetCurrentOrientation();

                this._orientationSensor.OrientationChanged += OrientationSensor_OrientationChanged;
            }

            this._displayInformation.OrientationChanged += DisplayInformation_OrientationChanged;
        }

        private void DisplayInformation_OrientationChanged(DisplayInformation sender, object args)
        {
            _displayOrientation = sender.CurrentOrientation;
        }

        private void UpdateButtonOrientation()
        {
            int device = QXScanner.ConvertDeviceOrientationToDegrees(_deviceOrientation);
            int display = QXScanner.ConvertDisplayOrientationToDegrees(_displayOrientation);

            if (_displayInformation.NativeOrientation == DisplayOrientations.Portrait)
            {
                device -= 90;
            }

            // Combine both rotations and make sure that 0 <= result < 360
            var angle = (360 + display + device) % 360;

            // Rotate the buttons in the UI to match the rotation of the device
            var transform = new RotateTransform { Angle = angle };

            this.btnList.RenderTransform = transform;           
            this.btnOpen.RenderTransform = transform; 
        }

        private void saveToHistory(Result qr)
        {             
            var tag = ResultParser.parseResult(qr).Type;

            int cat = BarcodeType.Text;          

            switch (tag) 
            { 
                case ParsedResultType.URI:
                    cat = BarcodeType.Link;
                    break; 

                case ParsedResultType.TEL:
                    cat = BarcodeType.Contact;
                    break; 

                case ParsedResultType.PRODUCT:
                    cat = BarcodeType.Barcode; 
                    break; 

                case ParsedResultType.ISBN:
                    cat = BarcodeType.Book; 
                    break; 

                case ParsedResultType.ADDRESSBOOK:
                    cat = BarcodeType.Namecard; 
                    break; 

                case ParsedResultType.CALENDAR:
                    cat = BarcodeType.Event; 
                    break; 

                case ParsedResultType.WIFI:
                    cat = BarcodeType.Network; 
                    break;

                case ParsedResultType.EMAIL_ADDRESS:
                    cat = BarcodeType.Email;
                    break;

                default:
                    cat = BarcodeType.Text; 
                    break; 
            }
 
            this.session.Execute(DbFactory.InsertCommand, cat, qr.Text, DateTime.Now);

            this.btnOpen.Visibility = cat == BarcodeType.Link ? Visibility.Visible : Visibility.Collapsed;
        }

        private void loadState()
        {
            this.tgSound.IsOn = App.Config.Sound;
            this.tgVibrate.IsOn = App.Config.Vibrate;

            this.deviceType = DeviceTypeHelper.GetDeviceFormFactorType();
        }

        private void saveState()
        {
            App.Config.Sound = this.tgSound.IsOn;
            App.Config.Vibrate = this.tgVibrate.IsOn;

            ApplicationDataContainer settings = ApplicationData.Current.LocalSettings;

            settings.Values["conf"] = StringHelper.Serialize(App.Config);
        }
  
        private async void btnOpen_Click(object sender, RoutedEventArgs e)
        {
            if (StringHelper.IsURL(this.qrText.Text))
            {
                this.scanner.Suspend();

                var dialog = new MessageDialog(this.qrText.Text);

                dialog.Title = "Open the link";
                dialog.Commands.Add(new UICommand("Yes") { Id = 0 });
                dialog.Commands.Add(new UICommand("No") { Id = 1 });

                var result = await dialog.ShowAsync();

                var action = (int)result.Id;

                if (action == 0)
                {
                    await Windows.System.Launcher.LaunchUriAsync(new Uri(this.qrText.Text));
                }
                else
                {
                    this.scanner.Resume();
                }
            }
        }

        private async void Canvas_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            try
            {
                await this.scanner.Focus();
            }
            catch (Exception ex)
            {
                await App.Logger.Write("Tap: " + ex.Message);
            }
        }

        private void tgFlash_Toggled(object sender, RoutedEventArgs e)
        {
            this.scanner.SetTorch(this.tgFlash.IsOn);
        }

        private async void switch_Toggled(object sender, RoutedEventArgs e)
        {
            if (this._stateLoad)
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    this.saveState();
                });
            }
        }

        private void btnList_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(About));
        }
    }
}
