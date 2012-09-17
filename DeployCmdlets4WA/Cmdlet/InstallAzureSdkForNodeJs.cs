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
using System.Net;
using System.Threading;
using System.IO;
using DeployCmdlets4WA.Utilities;
using DeployCmdlets4WA.Properties;

namespace DeployCmdlets4WA.Cmdlet
{
    [Cmdlet("Install", "AzureSdkForNodeJs")]
    public class InstallAzureSdkForNodeJs : PSCmdlet
    {
        private AutoResetEvent threadBlocker;

        private int downloadProgress;

        [Parameter(Mandatory = true, HelpMessage = "Specify the location where Azure node sdk is installed.")]
        public string AzureNodeSdkLoc { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();

            //Download the WebPI Commandline
            String downloadLocation = DownloadWebPICmdLine();

            //Unzip it.
            String unzipLocation = Unzip(downloadLocation);

            //Fire command to install NodeJs
            String logFileName = Install(unzipLocation);

            //Verify if installation was successful.
            if (WasInstallationSuccessful() == false)
            {
                Console.WriteLine(File.ReadAllText(logFileName));
                throw new Exception("Installtion of Windows Azure SDK for node.js failed. Please try installing the SDK separately and retry.");
            }
        }

        private string Install(string unzipLocation)
        {
            // .\WebpiCmdLine.exe /Products:AzureNodePowershell
            String logFileName = String.Concat("WebPiLog_", Guid.NewGuid().ToString(), ".txt");
            FileStream logFileFs = File.Create(logFileName);
            logFileFs.Close();

            String pathToWebPIExe = Path.Combine(unzipLocation, "WebpiCmd.exe");
            String installCommand = String.Format("Start-Process -File \"{0}\" -ArgumentList \" /Install /Products:AzureNodeSDK /Log:{1} /AcceptEULA \" -Wait", pathToWebPIExe, logFileName);
            ExecuteCommands.ExecuteCommand(installCommand, this.Host);
            return logFileName;
        }

        private string Unzip(string downloadLocation)
        {
            String unzipLocation = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(unzipLocation);
            String unzipCommand = String.Format(@"function Unzip([string]$locationOfZipFile, [string]$unzipLocation)
                                                {{
                                                    Write-Host $locationOfZipFile
                                                    Write-Host $unzipLocation
                                                    $shell_app = new-object -com shell.application
                                                    $zip_file = $shell_app.namespace($locationOfZipFile)
                                                    $destination = $shell_app.namespace($unzipLocation)
                                                    $destination.Copyhere($zip_file.items())
                                                }}
                                                Unzip ""{0}""  ""{1}""
                                                ", downloadLocation, unzipLocation);
            ExecuteCommands.ExecuteCommand(unzipCommand, this.Host);
            return unzipLocation;
        }

        private String DownloadWebPICmdLine()
        {
            try
            {
                String tempLocationToSave = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".zip");
                using (WebClient setupDownloader = new WebClient())
                {
                    setupDownloader.DownloadProgressChanged += new DownloadProgressChangedEventHandler(setupDownloader_DownloadProgressChanged);
                    setupDownloader.DownloadFileCompleted += new System.ComponentModel.AsyncCompletedEventHandler(setupDownloader_DownloadFileCompleted);
                    setupDownloader.DownloadFileAsync(new Uri(Resources.WebPICmdlineURL), tempLocationToSave);

                    Console.Write("Downloading AzureSDKForNode.JS setup - ");
                    threadBlocker = new AutoResetEvent(false);
                    threadBlocker.WaitOne();
                }
                return tempLocationToSave;
            }
            finally
            {
                if (threadBlocker != null) { threadBlocker.Close(); }
            }
        }

        private void setupDownloader_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            if ((e.ProgressPercentage % 10 == 0) && (e.ProgressPercentage > downloadProgress))
            {
                downloadProgress = e.ProgressPercentage;
                Console.Write(String.Concat(" ", e.ProgressPercentage, "%"));
            }
        }

        private void setupDownloader_DownloadFileCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            Console.WriteLine(String.Empty);
            threadBlocker.Set();
        }

        private bool WasInstallationSuccessful()
        {
            string cloudServiceDllLoc = Path.Combine(this.AzureNodeSdkLoc, "Microsoft.WindowsAzure.Management.CloudService.dll");
            return File.Exists(cloudServiceDllLoc) == true;
        }
    }
}
