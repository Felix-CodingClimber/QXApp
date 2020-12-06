using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Graphics.Display;
using Windows.Graphics.Imaging;
using Windows.Media;
using Windows.Media.Capture;
using Windows.Media.Devices; 
using Windows.Devices.Sensors;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using System.Runtime.InteropServices.WindowsRuntime;
using ZXing;

namespace QXScan.Core
{
    public class QXScanner : IDisposable
    {
        public delegate void CaptureEventHandler(object sender, CaptureEventArgs e);
        public delegate void CaptureInitializeEventHandler(object sender, CaptureInitializeEventArgs e);
        public delegate void CaptureResultHandler(object sender, CaptureResultEventArgs e);

        public event CaptureInitializeEventHandler CaptureStart;       
        public event CaptureResultHandler CaptureImage;
        public event CaptureEventHandler CaptureFail;
        public event CaptureEventHandler CaptureStop;
   
        protected virtual void OnCaptureStart(CaptureInitializeEventArgs e)
        {
            CaptureStart?.Invoke(this, e);             
        } 

        protected virtual void OnCaptureImage(CaptureResultEventArgs e)
        {
            CaptureImage?.Invoke(this, e);
        }
            
        protected virtual void OnCaptureFail(CaptureEventArgs e)
        {
            CaptureFail?.Invoke(this, e);
        }

        protected virtual void OnCaptureStop(CaptureEventArgs e)
        {
            CaptureStop?.Invoke(this, e);
        }

        private static readonly Guid RotationKey = new Guid("C380465D-2271-428C-9B83-ECEA3B4A85C1");

        private MediaCapture camera;
        public MediaCapture Camera
        {
            get
            {
                return this.camera;
            }
        }

        private BarcodeReader ZXReader;
        private StreamResolution resolution;
        public bool IsFront { get; set; }
        public bool IsMobile { get; set; }

        private volatile bool _isPreviewing = false;
        public bool Previewing
        {
            get
            {
                return this._isPreviewing;
            }
        }

        private bool _isInitialized = false;

        private bool _suspend { get; set; }
        public int Interval { get; set; }
                 
        private bool disposed = false;

        public QXScanner()
        {
            this.Interval = 350;

            this.ZXReader = new BarcodeReader() { AutoRotate = true };            
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    if (this.camera != null)
                    {
                        this.camera.Dispose();
                    }
                }

                this.camera = null;
               
                this.disposed = true;
            }            
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }
                
        private async Task InitializeAsync()
        {  
            // Create MediaCapture and its settings
            var mediaInitSettings = new MediaCaptureInitializationSettings
            {
                VideoDeviceId = await this.getCameraId(),
                PhotoCaptureSource = PhotoCaptureSource.VideoPreview,
                StreamingCaptureMode = StreamingCaptureMode.Video                                 
            };

            this.camera = new MediaCapture();

            await this.camera.InitializeAsync(mediaInitSettings);

            this._isInitialized = true;             
        }

        private async Task<string> getCameraId()
        {
            // Attempt to get the back camera if one is available, but use any camera device if not
           
            if (this.IsFront)
            {
                var devFront = await FindCameraDeviceByPanelAsync(Windows.Devices.Enumeration.Panel.Front);

                if (devFront != null)
                {
                    return devFront.Id;
                }
                else
                {
                    this.IsFront = false;
                }
            }

            var devBack = await FindCameraDeviceByPanelAsync(Windows.Devices.Enumeration.Panel.Back);

            if (devBack != null)
            {
                return devBack.Id;
            }
            else
            {
                throw new Exception("Not found camera.");
            } 
        }

        public async Task Start(DisplayOrientations orientation)
        {
            if (this.camera != null)
            {
                // Initialize the preview to the current orientation
                if (!_isPreviewing)
                {
                    this._isPreviewing = true;

                    await this.camera.StartPreviewAsync();

                    await this.SetRotation(orientation);

                    await this.Focus();

                    this.captureScreen();

                    OnCaptureStart(new CaptureInitializeEventArgs(this.resolution));
                }
            }
        }

        public async Task Stop()
        {
            if (this._isPreviewing)
            {
                this._isPreviewing = false;

                await this.camera.StopPreviewAsync();

                OnCaptureStop(new CaptureEventArgs("Scan stop"));
            }
        }

        public void Suspend()
        {
            this._suspend = true;
        }

        public void Resume()
        {
            this._suspend = false; 
        }
         
        public async Task SetPreviewAsync(CaptureElement previewControl)
        { 
            await this.InitializeAsync();

            if (this._isInitialized)
            {  
                await this.SetResolution();

                this.SetFocus();

                previewControl.Source = this.camera;
                previewControl.FlowDirection = FlowDirection.LeftToRight;
            }            
        }

        public async Task SetRotation(DisplayOrientations orientation)
        {
            if (this._isPreviewing)
            { 
                // Calculate which way and how far to rotate the preview
                int rotationDegrees = ConvertDisplayOrientationToDegrees(orientation);

                if (this.IsFront && this.IsMobile)
                {
                    rotationDegrees += 180;
                }

                // Add rotation metadata to the preview stream to make sure the aspect ratio / dimensions match when rendering and getting preview frames
                var props = this.camera.VideoDeviceController.GetMediaStreamProperties(MediaStreamType.VideoPreview);

                props.Properties.Add(RotationKey, rotationDegrees);

                await this.camera.SetEncodingPropertiesAsync(MediaStreamType.VideoPreview, props, null);
            }
        }
         
        private void SetFocus()
        {
            if (this.camera != null)
            { 
                var focusControl = this.camera.VideoDeviceController.FocusControl;

                if (focusControl.Supported)
                {
                    var focusRange = focusControl.SupportedFocusRanges.Contains(AutoFocusRange.FullRange) ? AutoFocusRange.FullRange : focusControl.SupportedFocusRanges.FirstOrDefault();
                    var focusMode = focusControl.SupportedFocusModes.Contains(FocusMode.Continuous) ? FocusMode.Continuous : focusControl.SupportedFocusModes.FirstOrDefault();

                    //set AF 
                    var focusSettings = new FocusSettings()
                    {
                        Mode = focusMode,
                        AutoFocusRange = focusRange,
                        DisableDriverFallback = true,
                        WaitForFocus = false
                    };

                    focusControl.Configure(focusSettings);
                }

                var flash = this.camera.VideoDeviceController.FlashControl;

                if (flash.Supported)
                {
                    flash.Enabled = false;
                }
            }
        }

        public void SetTorch(bool isEnable)
        {
            if (this.camera != null)
            {
                var torch = this.camera.VideoDeviceController.TorchControl;

                if (torch.Supported)
                {
                    torch.Enabled = isEnable;
                }
            }
        }

        private async Task SetResolution()
        {
            // Query all properties of the device 
            var allVideoProperties = this.camera.VideoDeviceController.GetAvailableMediaStreamProperties(MediaStreamType.VideoPreview).Select(x => new StreamResolution(x));

            //16:9
            var matchingFormats = allVideoProperties.Where(x => x.AspectRatio > 1.34 && x.Height * x.Width < 1000000).OrderByDescending(x => x.Height * x.Width);

            var prop = matchingFormats.FirstOrDefault();

            if (prop != null)
                this.resolution = prop;
            else
                this.resolution = allVideoProperties.FirstOrDefault();

            await this.camera.VideoDeviceController.SetMediaStreamPropertiesAsync(MediaStreamType.VideoPreview, this.resolution.EncodingProperties);
        }

        public async Task Focus()
        {
            if (this._isPreviewing && !this._suspend)
            {
                var focusControl = this.camera.VideoDeviceController.FocusControl;
                                
                if (focusControl.Supported)
                {
                    await focusControl.FocusAsync();
                }
            }
        }

        private async Task captureScreen()
        {
            var buffer = new byte[4 * this.resolution.Width * this.resolution.Height].AsBuffer();

            while (this._isPreviewing)
            {
                await Task.Delay(this.Interval);

                if (this._suspend)
                {
                    continue;
                }
                
                // Get information about the preview                      
                var videoFrame = new VideoFrame(BitmapPixelFormat.Bgra8, (int)this.resolution.Width, (int)this.resolution.Height);

                try
                {
                    // Capture the preview frame
                    if (this.camera != null)
                    {
                        await this.camera.GetPreviewFrameAsync(videoFrame);

                        var previewFrame = videoFrame.SoftwareBitmap; 

                        previewFrame.CopyToBuffer(buffer);

                        var qr = this.ZXReader.Decode(buffer.ToArray(), previewFrame.PixelWidth, previewFrame.PixelHeight, RGBLuminanceSource.BitmapFormat.BGRA32);

                        OnCaptureImage(new CaptureResultEventArgs(qr));                     
                    }
                }
                catch (Exception ex)
                {
                    OnCaptureFail(new CaptureEventArgs(ex.Message)); 
                }
                finally
                {
                    if (videoFrame != null)
                    {
                        videoFrame.Dispose();
                        videoFrame = null;
                    }
                }
            }

            buffer = null;
        }

        private static async Task<DeviceInformation> FindCameraDeviceByPanelAsync(Windows.Devices.Enumeration.Panel desiredPanel)
        {
            // Get available devices for capturing pictures
            var allVideoDevices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);

            // Get the desired camera by panel
            DeviceInformation desiredDevice = allVideoDevices.FirstOrDefault(x => x.EnclosureLocation != null && x.EnclosureLocation.Panel == desiredPanel);

            // If there is no device mounted on the desired panel, return the first device found
            return desiredDevice ?? allVideoDevices.FirstOrDefault();
        }

        private static VideoRotation GetVideoRotation(DisplayOrientations displayOrientation, bool counterclockwise)
        {
            switch (displayOrientation)
            {
                case DisplayOrientations.Landscape:
                    return VideoRotation.None;

                case DisplayOrientations.Portrait:
                    return (counterclockwise) ? VideoRotation.Clockwise270Degrees : VideoRotation.Clockwise90Degrees;

                case DisplayOrientations.LandscapeFlipped:
                    return VideoRotation.Clockwise180Degrees;

                case DisplayOrientations.PortraitFlipped:
                    return (counterclockwise) ? VideoRotation.Clockwise90Degrees :
                    VideoRotation.Clockwise270Degrees;

                default:
                    return VideoRotation.None;
            }
        }

        public static int ConvertDeviceOrientationToDegrees(SimpleOrientation orientation)
        {
            switch (orientation)
            {
                case SimpleOrientation.Rotated90DegreesCounterclockwise:
                    return 90;
                case SimpleOrientation.Rotated180DegreesCounterclockwise:
                    return 180;
                case SimpleOrientation.Rotated270DegreesCounterclockwise:
                    return 270;
                case SimpleOrientation.NotRotated:
                default:
                    return 0;
            }
        }

        public static int ConvertDisplayOrientationToDegrees(DisplayOrientations orientation)
        {
            switch (orientation)
            {
                case DisplayOrientations.Portrait:
                    return 90;
                case DisplayOrientations.LandscapeFlipped:
                    return 180;
                case DisplayOrientations.PortraitFlipped:
                    return 270;
                case DisplayOrientations.Landscape:
                default:
                    return 0;
            }
        }
    }    
}
