using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Net;
using System.Globalization;

namespace DeployCmdlets4WA.ServiceProxy
{
    public sealed class GetBlobProperties
    {
        private GetBlobProperties() { }

        private const string Linefeed = "\n";

        public static BlobProperties Get(Uri blobUri, string accountName, string accountKey)
        {
            HttpWebRequest request = CreateRequest(blobUri);
            request.ServicePoint.Expect100Continue = false;

            string canonicalizedRequest = CanonicalizeRequest(request, blobUri, accountName);
            string sha = GetSHA(canonicalizedRequest, Convert.FromBase64String(accountKey));
            request.Headers.Add("Authorization", string.Format(CultureInfo.InvariantCulture, "{0} {1}:{2}", "SharedKey", accountName, sha));

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                BlobProperties blobProperties = new BlobProperties()
                {
                    BlobType = response.Headers["x-ms-blob-type"],
                    CopyStatusDescription = response.Headers["x-ms-copy-status-description"],
                    LastModified = response.Headers["Last-Modified"]
                };

                blobProperties.SetCopyProgress(response.Headers["x-ms-copy-progress"]);
                blobProperties.SetCopyStatus(response.Headers["x-ms-copy-status"]);

                return blobProperties;
            }
        }

        private static HttpWebRequest CreateRequest(Uri to)
        {
            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(to);
            request.Method = "HEAD";
            request.UserAgent = "WA-Storage/1.7.0";
            request.Headers.Add("x-ms-date", DateTime.UtcNow.ToString("R", CultureInfo.InvariantCulture));
            request.Headers.Add("x-ms-version", "2012-02-12");
            return request;
        }

        private static string CanonicalizeRequest(HttpWebRequest request, Uri to, string accountName)
        {
            StringBuilder canonicalizedString = new StringBuilder();

            canonicalizedString.Append(request.Method);
            canonicalizedString.Append(Linefeed); //For Content-Encoding
            canonicalizedString.Append(Linefeed); //For Content-Language

            canonicalizedString.Append(Linefeed); //For Content-Length
            canonicalizedString.Append(string.Empty);

            //For Content-MD5
            //For empty content type.
            //For empty date because we have it in header.
            //If-Modified-Since
            //If-Match
            //If-None-Match
            //If-Unmodified-Since
            //Range
            canonicalizedString.Append(new string(Linefeed[0], 8));

            //Sort headers.
            List<string> headerNames = new List<string>();
            foreach (string eachHeader in request.Headers)
            {
                if (eachHeader.ToUpperInvariant().StartsWith("X-MS-", StringComparison.Ordinal) == true)
                {
                    headerNames.Add(eachHeader.ToLowerInvariant());
                }
            }
            headerNames.Sort();

            //Add headers to string.
            foreach (string sortedHeader in headerNames)
            {
                canonicalizedString.Append(Linefeed);
                canonicalizedString.AppendFormat("{0}:{1}", sortedHeader, request.Headers[sortedHeader]);
            }

            //Append resource strings.
            StringBuilder canonicalizedResource = new StringBuilder("/");
            canonicalizedResource.Append(accountName);
            canonicalizedResource.Append(to.AbsolutePath);

            canonicalizedString.Append(Linefeed);
            canonicalizedString.Append(canonicalizedResource);

            return canonicalizedString.ToString();
        }

        private static string GetSHA(string canonicalizedVal, byte[] key)
        {
            byte[] encoded = UTF8Encoding.UTF8.GetBytes(canonicalizedVal);
            using (HMACSHA256 hmacsha1 = new HMACSHA256(key))
            {
                return System.Convert.ToBase64String(hmacsha1.ComputeHash(encoded));
            }
        }
    }
}
