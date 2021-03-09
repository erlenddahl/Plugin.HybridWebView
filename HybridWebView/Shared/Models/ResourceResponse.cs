using System.IO;

namespace Plugin.HybridWebView.Shared.Models
{
    public class ResourceResponse
    {
        public string MimeType { get; set; }
        public string Encoding { get; set; }
        public Stream Contents { get; set; }
        public ResourceResponse(string mime, string encoding, Stream contents)
        {
            MimeType = mime;
            Encoding = encoding;
            Contents = contents;
        }
    }
}