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
using System.Globalization;

namespace DeployCmdlets4WA.Cmdlet
{
    [Cmdlet("Install", "AzureSdkForNodeJs")]
    public class InstallAzureSdkForNodeJS : PSCmdlet
    {
        private const string webPiProgLoc = @"Program Files\Microsoft\Web Platform Installer\WebpiCmd.exe";
        private const string webPiProgx86Loc = @"Program Files (x86)\Microsoft\Web Platform Installer\WebpiCmd.exe";

        [Parameter(Mandatory = true, HelpMessage = "Specify the location where Azure node sdk is installed.")]
        public string AzureNodeSdkLoc { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();

            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "Powershell version - {0}", this.Host.Version.ToString()));

            if (this.Host.Version.Major < 3)
            {
                throw new ApplicationFailedException(Resources.PowersehllV3Required);
            }

            //Download the WebPI.
            String downloadLocation = DownloadWebPI();

            //Install it.
            String pathToWebPIExe = InstallWebPI(downloadLocation);

            //Fire command to install NodeJs.
            String logFileName = InstallAzurePowershell(pathToWebPIExe);

            //Verify if installation was successful.
            if (WasInstallationSuccessful() == false)
            {
                Console.WriteLine(File.ReadAllText(logFileName));
                throw new ApplicationFailedException("Installtion of Windows Azure SDK for node.js failed. Please try installing the SDK separately and retry.");
            }
        }

        private string InstallAzurePowershell(string pathToWebPIExe)
        {
            // .\WebpiCmdLine.exe /Products:AzureNodePowershell
            String logFileName = String.Concat("WebPiLog_", Guid.NewGuid().ToString(), ".txt");
            FileStream logFileFs = File.Create(logFileName);
            logFileFs.Close();

            String installCommand = String.Format(CultureInfo.InvariantCulture, "Start-Process -File \"{0}\" -ArgumentList \" /Install /Products:WindowsAzurePowershell /Log:{1} /AcceptEULA \" -Wait", pathToWebPIExe, logFileName);
            ExecuteCommands.ExecuteCommand(installCommand, this.Host);
            return logFileName;
        }

        private string InstallWebPI(string downloadLocation)
        {
            String installCmd = String.Format(CultureInfo.InvariantCulture, "Start-Process -File msiexec.exe -ArgumentList /qn, /i, \"{0}\" -Wait", downloadLocation);
            String windir = GetWinDir();

            Utilities.ExecuteCommands.ExecuteCommand(installCmd, this.Host);
            
            WriteObject(Resources.VerifyingWebPIInstallation);
            if (File.Exists(Path.Combine(windir, webPiProgLoc)) == true)
            {
                return Path.Combine(windir, webPiProgLoc);
            }
            if (File.Exists(Path.Combine(windir, webPiProgx86Loc)) == true)
            {
                return Path.Combine(windir, webPiProgx86Loc);
            }
            throw new ApplicationException(Resources.ErrorInstallingWebPI);
        }

        private String DownloadWebPI()
        {
            String downloadLocation = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + Path.GetExtension(Resources.WebPIDownloadLoc));
            using(DownloadHelper downloadWebPI = new DownloadHelper())
            {
                downloadWebPI.Download(new Uri(Resources.WebPIDownloadLoc), downloadLocation);
            }
            return downloadLocation;
        }

        private bool WasInstallationSuccessful()
        {
            return File.Exists(this.AzureNodeSdkLoc) == true;
        }

        private static string GetWinDir()
        {
            string windirEnvVar = Environment.GetEnvironmentVariable("windir");
            return Path.GetPathRoot(windirEnvVar);
        }
    }
}
