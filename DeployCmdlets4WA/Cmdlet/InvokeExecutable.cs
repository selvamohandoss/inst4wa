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
using System.Threading;
using Microsoft.Win32;
using System.IO;
using System.Net;
using DeployCmdlets4WA.Properties;

namespace DeployCmdlets4WA.Cmdlet
{
    [Cmdlet(VerbsLifecycle.Invoke, "Executable")]
    public class InvokeExecutable : PSCmdlet, IDynamicParameters
    {
        private AutoResetEvent threadBlocker;
        private int downloadProgress;
        private RuntimeDefinedParameterDictionary _runtimeParamsCollection;

        [Parameter(Mandatory = true, HelpMessage = "Location on machine relative to current location OR location of web from where product setup could be downloaded.")]
        public string DownloadLoc { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Provide comma separated list of argument names to pass to MSI being invoked.")]
        public string ArgumentList { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Provide the location of file that should be verified to confirm the execution success.")]
        public string CheckFile { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();

            PreValidate();

            //Cmdlet supports both relative location and web location.
            bool isDownloadLocUri = IsDownloadLocUrl();

            //Validate the location specified in the config.
            String setupLocOnDisc = string.Empty;
            if (isDownloadLocUri == false)
            {
                setupLocOnDisc = Path.GetFullPath(this.DownloadLoc);
                ValidatePath(setupLocOnDisc);
            }

            //If location is URI the Download the setup Else just determine the absolute location of Setup.
            String setupLocation = isDownloadLocUri == true ? Download() : setupLocOnDisc;

            //Execute the setup.
            Install(setupLocation);

            PostValidate();
        }

        // validate params and other conditions necessary for the execution of the cmdlet
        private void PreValidate()
        {
            try
            {
                Uri uri = new Uri(DownloadLoc, UriKind.RelativeOrAbsolute);
            }
            catch (Exception exc)
            {
                throw new ArgumentException(Resources.InvalidDownloadLocMessage, "DownloadLoc", exc);
            }
        }

        // validate that the cmdlet performed its task successfully
        private void PostValidate()
        {
            if (string.IsNullOrEmpty(CheckFile) == false)
            {
                if (File.Exists(CheckFile) == false)
                {
                    throw new Exception(string.Format(Resources.ErrorExecutingExecutable, CheckFile));
                }
            }
        }

        public object GetDynamicParameters()
        {
            _runtimeParamsCollection = new RuntimeDefinedParameterDictionary();
            if (string.IsNullOrEmpty(this.ArgumentList) == true)
            {
                return _runtimeParamsCollection;
            }

            string[] argNames = this.ArgumentList.Split(',');
            foreach (string argName in argNames)
            {
                RuntimeDefinedParameter dynamicParam = new RuntimeDefinedParameter()
                {
                    Name = argName,
                    ParameterType = typeof(string),
                };
                dynamicParam.Attributes.Add(new ParameterAttribute() { Mandatory = false });
                _runtimeParamsCollection.Add(argName, dynamicParam);
            }
            return _runtimeParamsCollection;
        }

        private string Download()
        {
            try
            {
                String tempLocationToSave = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + Path.GetExtension(DownloadLoc));
                using (WebClient setupDownloader = new WebClient())
                {
                    setupDownloader.DownloadProgressChanged += new DownloadProgressChangedEventHandler(setupDownloader_DownloadProgressChanged);
                    setupDownloader.DownloadFileCompleted += new System.ComponentModel.AsyncCompletedEventHandler(setupDownloader_DownloadFileCompleted);
                    setupDownloader.DownloadFileAsync(new Uri(DownloadLoc), tempLocationToSave);

                    Console.Write("Downloading setup - ");
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

        private void Install(string downloadLocation)
        {
            string extension = Path.GetExtension(downloadLocation);
            if (extension == ".exe")
            {
                InstallExe(downloadLocation);
            }
            else
            {
                InstallMSI(downloadLocation);
            }
        }

        private bool IsDownloadLocUrl()
        {
            try
            {
                Uri downloadLocUri = new Uri(this.DownloadLoc);
            }
            catch (UriFormatException)
            {
                return false;
            }
            return true;
        }

        private void ValidatePath(string path)
        {
            if (File.Exists(path) == false)
            {
                throw new ArgumentException(Resources.InvalidDownloadLocMessage, "DownloadLoc");
            }
        }

        private void InstallExe(string loc)
        {
            String command = String.Format("Start-Process -File \"{0}\" -ArgumentList \"{1}\" -Wait", loc, "/sp- /verysilent /norestart /SUPPRESSMSGBOXES");
            Utilities.ExecuteCommands.ExecuteCommand(command, this.Host);
        }

        private void InstallMSI(string loc)
        {
            //msiexec.exe /i foo.msi /qn
            //Silent minor upgrade: msiexec.exe /i foo.msi REINSTALL=ALL REINSTALLMODE=vomus /qn
            String installCmd;
            if (string.IsNullOrEmpty(this.ArgumentList) == false)
            {
                string publicPropVal = GetPublicPropsForMSI();
                installCmd = String.Format("Start-Process -File msiexec.exe -ArgumentList /qn, /i, \"{0}\", \"{1}\" -Wait", loc, publicPropVal);
            }
            else
            {
                installCmd = String.Format("Start-Process -File msiexec.exe -ArgumentList /qn, /i, \"{0}\" -Wait", loc);
            }
            Utilities.ExecuteCommands.ExecuteCommand(installCmd, this.Host);
        }

        private string GetPublicPropsForMSI()
        {
            StringBuilder propStringBuilder = new StringBuilder();
            foreach (KeyValuePair<string, RuntimeDefinedParameter> eachParam in _runtimeParamsCollection)
            {
                //If value contain spaces we need to escape quotes with backtick.
                propStringBuilder.AppendFormat("{0}=`\"{1}`\"", eachParam.Value.Name.ToUpper(), eachParam.Value.Value);
            }
            return propStringBuilder.ToString();
        }
    }
}
