using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Globalization;
using System.Security.Cryptography;
using System.Web;
using System.Management.Automation;

namespace DeployCmdlets4WA.ServiceProxy
{

    public sealed class CopyBlob
    {
        private CopyBlob(){}

        private const string Linefeed = "\n";

        public static void Copy(string from, string to, string accountName, string accountKey)
        {
            Uri toUri = new Uri(to);

            HttpWebRequest request = CreateRequest(toUri);
            request.Headers.Add("x-ms-copy-source", from);
            request.ContentLength = 0;
            request.ServicePoint.Expect100Continue = false;
            string canonicalizedRequest = CanonicalizeRequest(request, toUri, accountName);
            string sha = GetSHA(canonicalizedRequest, Convert.FromBase64String(accountKey));

            request.Headers.Add("Authorization", string.Format(CultureInfo.InvariantCulture, "{0} {1}:{2}", "SharedKey", accountName, sha));

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                WaitTillComplete(response.Headers["x-ms-copy-status"], toUri, accountName, accountKey);
            }
        }

        private static void WaitTillComplete(string stateOfCopyOperation, Uri blobUri, string accountName, string accountKey)
        {
            if (stateOfCopyOperation == "success") { return; }

            while (true)
            {
                System.Threading.Thread.Sleep(5000);
                BlobProperties properties = GetBlobProperties.Get(blobUri, accountName, accountKey);
                
                if (properties.CopyStatus == CopyStatus.Success)
                {
                    break;
                }
                if (properties.CopyStatus != CopyStatus.Pending)
                {
                    throw new ApplicationFailedException(string.Format(CultureInfo.InvariantCulture, "Error copying blob - Copy status {0} - Error description {1}", properties.CopyStatus, properties.CopyStatusDescription));
                }
                Console.WriteLine("Copying blob in progress - copied {0} of {1}", properties.BytesCopied, properties.BytesTotal);
            }
        }

        private static HttpWebRequest CreateRequest(Uri to)
        {
            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(to);
            request.Method = "PUT";
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
            canonicalizedString.Append(request.ContentLength.ToString(CultureInfo.InvariantCulture));

            canonicalizedString.Append(Linefeed); //For Content-MD5
            canonicalizedString.Append(Linefeed); //For empty content type.
            canonicalizedString.Append(Linefeed); //For empty date because we have it in header.
            canonicalizedString.Append(Linefeed); //If-Modified-Since
            canonicalizedString.Append(Linefeed); //If-Match
            canonicalizedString.Append(Linefeed); //If-None-Match
            canonicalizedString.Append(Linefeed); //If-Unmodified-Since
            canonicalizedString.Append(Linefeed); //Range

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
