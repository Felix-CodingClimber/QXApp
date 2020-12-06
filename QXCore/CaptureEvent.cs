using System;
using ZXing;

namespace QXScan.Core
{
    public class CaptureEventArgs : EventArgs
    {
        private string text;

        public string Text
        {
            get
            {
                return this.text;
            }
        }

        public CaptureEventArgs(string txt)
        {
            this.text = txt;
        }
    }

    public class CaptureInitializeEventArgs : EventArgs
    {
        private StreamResolution _resolution;

        public StreamResolution Resolution
        {
            get
            {
                return this._resolution;
            }
        }

        public CaptureInitializeEventArgs(StreamResolution resolution)
        {
            this._resolution = resolution;
        }
    }

    public class CaptureResultEventArgs : EventArgs
    {         
        public Result Result
        {
            get; set;
        }

        public CaptureResultEventArgs(Result qr)
        {
            this.Result = qr;
        }
    }
}
