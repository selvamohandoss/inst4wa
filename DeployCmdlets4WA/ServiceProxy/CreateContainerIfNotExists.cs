using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Globalization;
using System.Security.Cryptography;

namespace DeployCmdlets4WA.ServiceProxy
{
    public sealed class CreateContainerIfNotExists
    {
        private CreateContainerIfNotExists() { }

        private const string Linefeed = "\n";

        public static void CreateIfNotExists(string containerName, string accountName, string accountKey)
        {
            string containerUrl = string.Format(CultureInfo.InvariantCulture, Utilities.Utils.ContainerURLFormat,
                accountName, containerName.ToLowerInvariant());
            Uri containerUri = new Uri(containerUrl);

            HttpWebRequest request = CreateRequest(containerUri);
            request.ContentLength = 0;
            request.ServicePoint.Expect100Continue = false;

            string canonicalizedRequest = CanonicalizeRequest(request, containerUri, accountName);

            string sha = GetSHA(canonicalizedRequest, Convert.FromBase64String(accountKey));
            request.Headers.Add("Authorization", string.Format(CultureInfo.InvariantCulture, "{0} {1}:{2}", "SharedKey", accountName, sha));

            try
            {
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                }
            }
            catch (WebException ex)
            {
                HttpWebResponse response = ex.Response as HttpWebResponse;
                //Conflict means container already exists.
                if (response.StatusCode != HttpStatusCode.Conflict)
                {
                    throw;
                }
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

        private static string CanonicalizeRequest(HttpWebRequest request, Uri containerUri, string accountName)
        {
            StringBuilder canonicalizedString = new StringBuilder();

            canonicalizedString.Append(request.Method);

            //For Content-Encoding, Content-Language, Content-Length
            canonicalizedString.Append(new string(Linefeed[0], 3));
            canonicalizedString.Append(request.ContentLength);

            //For Content-MD5, Content-Type, Date, If-Modified-Since, If-Match, If-None-Match, If-Unmodified-Since, Range
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
            canonicalizedResource.Append(containerUri.AbsolutePath);

            canonicalizedString.Append(Linefeed);
            canonicalizedString.Append(canonicalizedResource);

            canonicalizedString.Append(Linefeed);
            canonicalizedString.Append("restype:container");

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
