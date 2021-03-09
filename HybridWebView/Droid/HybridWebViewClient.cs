﻿using Android.Content;
using Xamarin.Forms;
using System;
using System.IO;
using System.Linq;
using Android.Webkit;
using Android.Net.Http;
using Android.Graphics;
using Plugin.HybridWebView.Shared;
using WebView = Android.Webkit.WebView;

namespace Plugin.HybridWebView.Droid
{
    public class HybridWebViewClient : WebViewClient
    {
        private readonly WeakReference<HybridWebViewRenderer> _reference;

        public HybridWebViewClient(HybridWebViewRenderer renderer)
        {
            _reference = new WeakReference<HybridWebViewRenderer>(renderer);
        }

        public override WebResourceResponse? ShouldInterceptRequest(WebView? view, IWebResourceRequest? request)
        {
            if (request?.Url != null && _reference != null && _reference.TryGetTarget(out var renderer) && renderer.Element?.ShouldInterceptRequest != null)
            {
                try
                {
                    var res = renderer.Element.ShouldInterceptRequest(request.Url.ToString());
                    if (res != null)
                    {
                        if (string.IsNullOrEmpty(res.MimeType))
                        {
                            var ext = request.Url.ToString().Split('.').LastOrDefault();
                            if (!string.IsNullOrEmpty(ext)) res.MimeType = MimeTypeMap.Singleton.GetMimeTypeFromExtension(ext);
                        }
                        return new WebResourceResponse(res.MimeType, res.Encoding, res.Contents);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                }
            }

            return base.ShouldInterceptRequest(view, request);
        }

        public override void OnReceivedHttpError(Android.Webkit.WebView view, IWebResourceRequest request, WebResourceResponse errorResponse)
        {
            if (_reference == null || !_reference.TryGetTarget(out var renderer)) return;
            if (renderer.Element == null) return;

            renderer.Element.HandleNavigationError(errorResponse.StatusCode, request.Url.ToString());
            renderer.Element.Navigating = false;
        }

        public override void OnReceivedError(Android.Webkit.WebView view, IWebResourceRequest request, WebResourceError error)
        {
            if (_reference == null || !_reference.TryGetTarget(out var renderer)) return;
            if (renderer.Element == null) return;

            renderer.Element.HandleNavigationError((int)error.ErrorCode, request.Url.ToString());
            renderer.Element.Navigating = false;
        }

        public override bool ShouldOverrideUrlLoading(Android.Webkit.WebView view, IWebResourceRequest request)
        {
            if (DoesComponentWantToOverrideUrlLoading(view, request.Url.ToString()))
                return true;

            return base.ShouldOverrideUrlLoading(view, request);
        }

        [Obsolete("deprecated")]
        public override bool ShouldOverrideUrlLoading(Android.Webkit.WebView view, string url)
        {
            if (DoesComponentWantToOverrideUrlLoading(view, url))
                return true;

            return base.ShouldOverrideUrlLoading(view, url);
        }

        private bool DoesComponentWantToOverrideUrlLoading(Android.Webkit.WebView view, string url)
        {
            if (_reference != null && _reference.TryGetTarget(out var renderer) && renderer.Element != null)
            {
                var response = renderer.Element.HandleNavigationStartRequest(url);

                if (response.Cancel || response.OffloadOntoDevice)
                {
                    Device.BeginInvokeOnMainThread(() =>
                    {
                        if (response.OffloadOntoDevice)
                            AttemptToHandleCustomUrlScheme(view, url);

                        view.StopLoading();
                    });
                    return true;
                }
            }
            return false;
        }

        private void CheckResponseValidity(Android.Webkit.WebView view, string url)
        {
            if (_reference == null || !_reference.TryGetTarget(out var renderer)) return;
            if (renderer.Element == null) return;

            var response = renderer.Element.HandleNavigationStartRequest(url);

            if (response.Cancel || response.OffloadOntoDevice)
            {
                Device.BeginInvokeOnMainThread(() =>
                {
                    if (response.OffloadOntoDevice)
                        AttemptToHandleCustomUrlScheme(view, url);

                    view.StopLoading();
                });
            }
        }

        public override void OnPageStarted(Android.Webkit.WebView view, string url, Bitmap favicon)
        {
            if (_reference == null || !_reference.TryGetTarget(out var renderer)) return;
            if (renderer.Element == null) return;

            renderer.Element.Navigating = true;
        }

        private bool AttemptToHandleCustomUrlScheme(Android.Webkit.WebView view, string url)
        {
            if (url.StartsWith("mailto"))
            {
                var emailData = Android.Net.MailTo.Parse(url);

                var email = new Intent(Intent.ActionSendto);

                email.SetData(Android.Net.Uri.Parse("mailto:"));
                email.PutExtra(Intent.ExtraEmail, new string[] { emailData.To });
                email.PutExtra(Intent.ExtraSubject, emailData.Subject);
                email.PutExtra(Intent.ExtraCc, emailData.Cc);
                email.PutExtra(Intent.ExtraText, emailData.Body);

                if (email.ResolveActivity(Forms.Context.PackageManager) != null)
                    Forms.Context.StartActivity(email);

                return true;
            }

            if (url.StartsWith("http"))
            {
                var webPage = new Intent(Intent.ActionView, Android.Net.Uri.Parse(url));
                if (webPage.ResolveActivity(Forms.Context.PackageManager) != null)
                    Forms.Context.StartActivity(webPage);

                return true;
            }

            return false;
        }

        public override void OnReceivedSslError(Android.Webkit.WebView view, SslErrorHandler handler, SslError error)
        {
            if (_reference == null || !_reference.TryGetTarget(out var renderer)) return;
            if (renderer.Element == null) return;

            if (HybridWebViewRenderer.IgnoreSslGlobally)
            {
                handler.Proceed();
            }

            else
            {
                handler.Cancel();
                renderer.Element.Navigating = false;
            }
        }

        public override async void OnPageFinished(Android.Webkit.WebView view, string url)
        {
            if (_reference == null || !_reference.TryGetTarget(out var renderer)) return;
            if (renderer.Element == null || !renderer.Element.Navigating) return;

            renderer.Element.Navigating = false;

            renderer.Element.HandleNavigationCompleted(url);
            await renderer.OnJavascriptInjectionRequest(HybridWebViewControl.InjectedFunction);

            if (renderer.Element.EnableGlobalCallbacks)
            {
                foreach (var function in HybridWebViewControl.GlobalRegisteredCallbacks.ToList())
                {
                    await renderer.OnJavascriptInjectionRequest(HybridWebViewControl.GenerateFunctionScript(function.Key));
                }
            }

            foreach (var function in renderer.Element.LocalRegisteredCallbacks.ToList())
            {
                await renderer.OnJavascriptInjectionRequest(HybridWebViewControl.GenerateFunctionScript(function.Key));
            }

            renderer.Element.CanGoBack = view.CanGoBack();
            renderer.Element.CanGoForward = view.CanGoForward();
            renderer.Element.HandleContentLoaded();
        }
    }
}
