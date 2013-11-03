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
using System.Globalization;
using DeployCmdlets4WA.Utilities;

namespace DeployCmdlets4WA.Cmdlet
{
    [Cmdlet(VerbsLifecycle.Invoke, "Executable")]
    public class InvokeExecutable : PSCmdlet, IDynamicParameters, IDisposable
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

        [Parameter(Mandatory = false, HelpMessage = "Number of times command should try to download the file from specified URL.")]
        public int RetryCount { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();

            PreValidate(this.DownloadLoc);

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
        private static void PreValidate(string downloadLoc)
        {
            if (Uri.IsWellFormedUriString(downloadLoc, UriKind.RelativeOrAbsolute) == false)
            {
                throw new ArgumentException(Resources.InvalidDownloadLocMessage, "downloadLoc");
            }
        }

        // validate that the cmdlet performed its task successfully
        private void PostValidate()
        {
            if (string.IsNullOrEmpty(CheckFile) == false)
            {
                if (File.Exists(CheckFile) == false)
                {
                    throw new ApplicationFailedException(string.Format(CultureInfo.InvariantCulture, Resources.ErrorExecutingExecutable, CheckFile));
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
            if (RetryCount == 0)
            {
                RetryCount = 3; //Try 3 times before giving up if not specified.
            }
            String tempLocation = String.Empty;
            int tryCount = 0;
            while (tryCount < RetryCount)
            {
                try
                {
                    tempLocation = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + Path.GetExtension(DownloadLoc.ToString()));
                    using (DownloadHelper helper = new DownloadHelper())
                    {
                        helper.Download(new Uri(DownloadLoc), tempLocation);
                    }
                    break;
                }
                catch (Exception ex)
                {
                    tryCount++;
                    if (tryCount < RetryCount == false)
                    {
                        throw ex;
                    }
                    else
                    {
                        String errorMessage = String.Format(CultureInfo.InvariantCulture, "Retry count - {0}. Error downloading the file - {1}", tryCount, ex.Message);
                        Console.WriteLine(errorMessage);
                    }
                }
            }
            return tempLocation;
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

        private static void ValidatePath(string downloadLoc)
        {
            if (File.Exists(downloadLoc) == false)
            {
                throw new ArgumentException(Resources.InvalidDownloadLocMessage, "downloadLoc");
            }
        }

        private void InstallExe(string loc)
        {
            String command = String.Format(CultureInfo.InvariantCulture, "Start-Process -File \"{0}\" -ArgumentList \"{1}\" -Wait", loc, "/sp- /verysilent /norestart /SUPPRESSMSGBOXES");
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
                installCmd = String.Format(CultureInfo.InvariantCulture, "Start-Process -File msiexec.exe -ArgumentList /qn, /i, \"{0}\", \"{1}\" -Wait", loc, publicPropVal);
            }
            else
            {
                installCmd = String.Format(CultureInfo.InvariantCulture, "Start-Process -File msiexec.exe -ArgumentList /qn, /i, \"{0}\" -Wait", loc);
            }
            Utilities.ExecuteCommands.ExecuteCommand(installCmd, this.Host);
        }

        private string GetPublicPropsForMSI()
        {
            StringBuilder propStringBuilder = new StringBuilder();
            foreach (KeyValuePair<string, RuntimeDefinedParameter> eachParam in _runtimeParamsCollection)
            {
                //If value contain spaces we need to escape quotes with backtick.
                propStringBuilder.AppendFormat("{0}=`\"{1}`\"", eachParam.Value.Name.ToUpperInvariant(), eachParam.Value.Value);
            }
            return propStringBuilder.ToString();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // free managed resources
                if (threadBlocker != null)
                {
                    threadBlocker.Close();
                    threadBlocker = null;
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
