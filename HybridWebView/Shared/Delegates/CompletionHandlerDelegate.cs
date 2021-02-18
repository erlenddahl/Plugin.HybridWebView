namespace Plugin.HybridWebView.Shared.Delegates
{
    public class CompletionHandlerDelegate
    {
        /// <summary>
        /// The publishing Uri
        /// </summary>
        public string Uri { get; set; }
        
        /// <summary>
        /// The error code encountered
        /// </summary>
        public int Code { get; set; }
    }
}