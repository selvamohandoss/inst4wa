#region Copyright Notice
/*
Copyright © Microsoft Open Technologies, Inc.
All Rights Reserved
Apache 2.0 License

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

     http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.

See the Apache Version 2.0 License for specific language governing permissions and limitations under the License.
*/
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Management.Automation;
using DeployCmdlets4WA.Properties;
using System.IO;
using DeployCmdlets4WA.Utilities;
using System.Net;
using System.Globalization;

namespace DeployCmdlets4WA.Cmdlet
{
    [Cmdlet(VerbsCommon.Get, "Unzip")]
    public class GetAndUnzip : PSCmdlet
    {
        [Parameter(Mandatory = true, HelpMessage = "Download location.")]
        [ValidateNotNullOrEmpty]
        public string Url { get; set; }

        [Parameter(Mandatory = true, HelpMessage = "Location to Unzip.")]
        [ValidateNotNullOrEmpty]
        public string UnzipLoc { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();

            //Validate Params
            PreValidate(this.Url, this.UnzipLoc);

            //Download
            string downloadLoc = Download();

            //Unzip
            Unzip(downloadLoc);
        }

        private static void PreValidate(string url, string unzipLoc)
        {
            if (Uri.IsWellFormedUriString(url, UriKind.RelativeOrAbsolute) == false)
            {
                throw new ArgumentException(Resources.IncorrectURL, "url");
            }
            if (Directory.Exists(unzipLoc) == false)
            {
                throw new ArgumentException(Resources.UnzipLocDoesNotExist, "unzipLoc");
            }
        }

        private string Download()
        {
            String tempLocation = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + Path.GetExtension(Url.ToString()));

            DownloadHelper helper = new DownloadHelper();
            helper.Download(new Uri(Url), tempLocation);

            return tempLocation;
        }

        private void Unzip(string downloadLocation)
        {
            String unzipCommand = String.Format(CultureInfo.InvariantCulture, @"function Unzip([string]$locationOfZipFile, [string]$unzipLocation)
                                                {{
                                                    Write-Host $locationOfZipFile
                                                    Write-Host $unzipLocation
                                                    $shell_app = new-object -com shell.application
                                                    $zip_file = $shell_app.namespace($locationOfZipFile)
                                                    $destination = $shell_app.namespace($unzipLocation)
                                                    $destination.Copyhere($zip_file.items())
                                                }}
                                                Unzip ""{0}""  ""{1}""
                                                ", downloadLocation, UnzipLoc);
            ExecuteCommands.ExecuteCommand(unzipCommand, this.Host);
        }
    }
}
