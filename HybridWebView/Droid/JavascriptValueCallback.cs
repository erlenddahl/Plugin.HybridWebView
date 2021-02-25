using System;
using Android.Webkit;

namespace Plugin.HybridWebView.Droid
{
    public class JavascriptValueCallback : Java.Lang.Object, IValueCallback
    {
        private Action<string> _callback;
        
        public JavascriptValueCallback(Action<string> callback)
        {
            _callback = callback;
        }

        public void OnReceiveValue(Java.Lang.Object result)
        {
            _callback?.Invoke(Convert.ToString(result));
        }
    }
}
