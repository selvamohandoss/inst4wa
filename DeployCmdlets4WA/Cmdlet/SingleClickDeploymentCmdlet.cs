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
using System.Security.Permissions;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Management.Automation;
using System.Diagnostics;
using System.Runtime.InteropServices;
using DeployCmdlets4WA.Properties;
using System.Reflection;
using System.Management.Automation.Runspaces;
using System.Management;
using DeployCmdlets4WA.Utilities;
using System.Globalization;

namespace DeployCmdlets4WA.Cmdlet
{
    [Cmdlet(VerbsCommon.New, "DeployOnAzure")]
    public class DeployOnAzure : PSCmdlet, IDynamicParameters
    {
        private enum LogType
        {
            Begin,
            End,
            Error
        }

        private class StepType
        {
            public const string CmdLet = "Cmdlet";
            public const string ChangeWorkingDir = "ChangeWorkingDir";
            public const string PowershellScript = "Powershell";
            public const string PS1File = "PS1";

            private StepType()
            { }
        }

        private const string programFileNotation = "%%Program Files%%";
        private const string systemDriveNotation = "%%windir%%";
        private const string publishSettingExtn = ".publishsettings";

        private string _publishSettingsPath;
        private bool _isXmlPathValid = true;
        private RuntimeDefinedParameterDictionary _runtimeParamsCollection;
        private AutoResetEvent _threadBlocker;
        private DeploymentModelHelper _controller;

        // method to get Downloads folder path
        private static readonly Guid DownloadsFolderGUID = new Guid("374DE290-123F-4565-9164-39C4925E467B");

        [Parameter(Mandatory = true)]
        public String XmlConfigPath { get; set; }

        [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
        protected override void ProcessRecord()
        {
            if (_isXmlPathValid == false || !ValidateParameters())
                return;

            if (IsEmulator() == false)
            {
                string pubSettingsFilePath = string.Empty;
                if (TryParsePubSettingFilePathParam(out  pubSettingsFilePath) == false)
                {
                    if (!DownloadPublishSettings())
                        return;
                }
                else
                {
                    _controller.SetParameterByName("PublishSettingsFilePath", pubSettingsFilePath);
                }
            }

            for (int i = 0; ; i++)
            {
                DeploymentModelStepsStep step = _controller.GetStepAtIndex(i);
                Console.WriteLine("===============================");

                if (step == null)
                {
                    break;
                }

                if (!ProcessStep(step))
                    break;
            }
        }

        private bool ProcessStep(DeploymentModelStepsStep step)
        {
            bool bRet = true;

            switch (step.Type)
            {
                case StepType.CmdLet:
                    String command = GetCommandForStep(step);
                    Log(LogType.Begin, command);
                    bRet = ExecutePsCmdlet(step.Message, command);
                    Log(LogType.End, command);
                    break;

                case StepType.ChangeWorkingDir:
                    Log(LogType.Begin, StepType.ChangeWorkingDir);
                    bRet = ChangeWorkingDir(step);
                    Log(LogType.End, StepType.ChangeWorkingDir);
                    break;

                case StepType.PowershellScript:
                    Log(LogType.Begin, step.Command);
                    bRet = ExecuteCommand(step.Command);
                    Log(LogType.End, step.Command);
                    break;

                case StepType.PS1File:
                    Log(LogType.Begin, step.Command);
                    bRet = ExecutePS1File(step);
                    Log(LogType.End, step.Command);
                    break;

                default:
                    Log(LogType.Error, "Unrecognized step type inside deployment model xml: " + step.Type);
                    bRet = false;
                    break;
            }

            return bRet;
        }

        private bool ChangeWorkingDir(DeploymentModelStepsStep step)
        {
            try
            {
                String location = GetParamValue(step.CommandParam[0].ParameterName);
                SessionState.Path.SetLocation(location);
            }
            catch (Exception exc)
            {
                Log(LogType.Error, "Exception while changing working directory: " + exc.Message);
                return false;
            }

            return true;
        }

        private string GetCommandForStep(DeploymentModelStepsStep step)
        {
            StringBuilder command = new StringBuilder(step.Command + "  ");

            if (step.CommandParam != null)
            {
                for (int i = 0; i < step.CommandParam.Length; i++)
                {
                    String paramValue = GetParamValue(step.CommandParam[i].ParameterName);

                    //Handle the cases where user need to just add switch to command..
                    paramValue = String.IsNullOrEmpty(paramValue) == true ? String.Empty : paramValue;
                    if (String.IsNullOrEmpty(paramValue) == true)
                    {
                        command.AppendFormat(" -{0} ", step.CommandParam[i].Name);
                    }
                    //Handle the cases where param value begins with @..which indicates that value is hashtable..so dont wrap it in double quotes.
                    else if (paramValue.StartsWith("@", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        command.AppendFormat(" -{0} {1} ", step.CommandParam[i].Name, paramValue);
                    }
                    else
                    {
                        command.AppendFormat(" -{0} \"{1}\" ", step.CommandParam[i].Name, paramValue);
                    }
                }
            }

            return command.ToString();
        }

        private static bool ExecuteCommand(String command)
        {
            bool bRet = true;

            try
            {
                PowerShell ps = PowerShell.Create();
                ps.AddScript(command);

                // Create the output buffer for the results.
                Collection<PSObject> result = ps.Invoke();
                foreach (PSObject eachResult in result)
                {
                    Console.WriteLine(eachResult.ToString());
                }
            }
            catch (Exception exc)
            {
                Log(LogType.Error, "Exception while executing command: " + exc.Message);
                bRet = false;
            }

            return bRet;
        }

        private bool ExecutePS1File(DeploymentModelStepsStep step)
        {
            bool bRet = true;
            Console.WriteLine(step.Message);
            Console.WriteLine("File: " + step.Command);
            try
            {
                string filePath = step.Command;
                if (!Path.IsPathRooted(filePath))
                    filePath = Path.Combine(System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), step.Command);

                Runspace executePs1FileRunspace = Runspace.DefaultRunspace;
                using (Pipeline executePsCmdletPipeline = executePs1FileRunspace.CreateNestedPipeline())
                {
                    Command scriptCommand = new Command(filePath);
                    if (step.CommandParam != null)
                    {
                        for (int i = 0; i < step.CommandParam.Length; i++)
                        {
                            DeploymentModelStepsStepCommandParam param = step.CommandParam[i];
                            String paramValue = GetParamValue(param.ParameterName);
                            if (String.IsNullOrEmpty(param.Name) == true)
                            {
                                scriptCommand.Parameters.Add(null, paramValue);
                            }
                            else
                            {
                                scriptCommand.Parameters.Add(param.Name, paramValue);
                            }
                        }
                    }
                    executePsCmdletPipeline.Error.DataReady += new EventHandler(Error_DataReadyExecutePsCmdlet);
                    executePsCmdletPipeline.Output.DataReady += new EventHandler(Output_DataReadyExecutePsCmdlet);
                    executePsCmdletPipeline.Commands.Add(scriptCommand);
                    executePsCmdletPipeline.Invoke();
                }
            }
            catch (Exception exc)
            {
                Log(LogType.Error, "Exception while executing PS1 file: " + exc.Message);
                bRet = false;
            }
            return bRet;
        }

        private bool ExecutePsCmdlet(String beginMessage, String command)
        {
            Console.WriteLine(beginMessage);

            try
            {
                Runspace executePsCmdletRunspace = Runspace.DefaultRunspace;
                using (Pipeline executePsCmdletPipeline = executePsCmdletRunspace.CreateNestedPipeline(command, true))
                {
                    executePsCmdletPipeline.Error.DataReady += new EventHandler(Error_DataReadyExecutePsCmdlet);
                    executePsCmdletPipeline.Output.DataReady += new EventHandler(Output_DataReadyExecutePsCmdlet);
                    executePsCmdletPipeline.Invoke();
                }
            }
            catch (Exception exc)
            {
                Log(LogType.Error, "Exception while executing cmdlet: " + exc.Message);
                return false;
            }
            return true;
        }

        private void Output_DataReadyExecutePsCmdlet(object sender, EventArgs e)
        {
            PipelineReader<PSObject> reader = sender as PipelineReader<PSObject>;
            if (reader != null)
            {
                while (reader.Count > 0)
                {
                    Console.WriteLine(reader.Read().ToString());
                }
            }
        }

        private void Error_DataReadyExecutePsCmdlet(object sender, EventArgs e)
        {
            PipelineReader<Object> reader = sender as PipelineReader<Object>;
            if (reader != null)
            {
                while (reader.Count > 0)
                {
                    Console.WriteLine(reader.Read().ToString());
                }
            }
        }

        private bool DownloadPublishSettings()
        {
            bool bRet = true;

            // determine paas or iaas
            bool isIaaS = false;
            string serviceModel = GetParamValue("ServiceModel");
            if (!string.IsNullOrEmpty(serviceModel))
                isIaaS = (serviceModel.ToUpperInvariant() == "IAAS");

            Process.Start(isIaaS ? Resources.AzureIaaSPublishSettingsURL : Resources.AzurePaaSPublishSettingsURL);
            Console.WriteLine("Waiting for publish settings file.");

            _publishSettingsPath = GetParamValue("PublishSettingsPath");

            try
            {
                // if no path is specified, we need to watch the default downloads folder as well as the folder where the current assembly is running from
                if (string.IsNullOrEmpty(_publishSettingsPath))
                {
                    string downloadsFolderPath, currentFolderPath;

                    NativeMethods.SHGetKnownFolderPath(DownloadsFolderGUID, 0, IntPtr.Zero, out downloadsFolderPath);
                    currentFolderPath = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

                    // set up watchers for both paths and wait until one fires
                    using (_threadBlocker = new AutoResetEvent(false))
                    using (FileSystemWatcher downloadsFolderWatcher = SetupFolderWatcher(downloadsFolderPath))
                    using (FileSystemWatcher currentFolderWatcher = SetupFolderWatcher(currentFolderPath))
                    {
                        downloadsFolderWatcher.Changed += new FileSystemEventHandler(folderWatcher_EventHandler);
                        downloadsFolderWatcher.Created += new FileSystemEventHandler(folderWatcher_EventHandler);
                        downloadsFolderWatcher.Renamed += new RenamedEventHandler(folderWatcher_EventHandler);

                        currentFolderWatcher.Changed += new FileSystemEventHandler(folderWatcher_EventHandler);
                        currentFolderWatcher.Created += new FileSystemEventHandler(folderWatcher_EventHandler);
                        currentFolderWatcher.Renamed += new RenamedEventHandler(folderWatcher_EventHandler);

                        if (!_threadBlocker.WaitOne(1200000))
                        {
                            Log(LogType.Error, "Timed out waiting for publishsettings file.");
                            bRet = false;
                        }
                    }
                }
                else
                {
                    using (_threadBlocker = new AutoResetEvent(false))
                    using (FileSystemWatcher publishSettingsFolderWatcher = SetupFolderWatcher(_publishSettingsPath))
                    {
                        publishSettingsFolderWatcher.Changed += new FileSystemEventHandler(folderWatcher_EventHandler);
                        publishSettingsFolderWatcher.Created += new FileSystemEventHandler(folderWatcher_EventHandler);
                        publishSettingsFolderWatcher.Renamed += new RenamedEventHandler(folderWatcher_EventHandler);

                        if (!_threadBlocker.WaitOne(1200000))
                        {
                            Log(LogType.Error, "Timed out waiting for publishsettings file.");
                            bRet = false;
                        }
                    }
                }
            }
            catch (Exception exc)
            {
                Log(LogType.Error, "Exception while downloading publishsettings file: " + exc.Message);
                bRet = false;
            }

            return bRet;
        }

        private static FileSystemWatcher SetupFolderWatcher(string folderPath)
        {
            Console.WriteLine("Watching publish settings folder: " + folderPath);

            FileSystemWatcher publishSettingsLocationWatcher = new FileSystemWatcher(folderPath);
            publishSettingsLocationWatcher.EnableRaisingEvents = true;
            publishSettingsLocationWatcher.IncludeSubdirectories = true;
            publishSettingsLocationWatcher.Filter = String.Concat("*", publishSettingExtn);
            return publishSettingsLocationWatcher;
        }

        private void folderWatcher_EventHandler(object sender, FileSystemEventArgs e)
        {
            if (Path.GetExtension(e.Name) == publishSettingExtn)
            {
                string publishSettingsFilePath = e.FullPath;
                Console.WriteLine("Publish settings file: " + publishSettingsFilePath);
                _controller.SetParameterByName("PublishSettingsFilePath", publishSettingsFilePath);
                _threadBlocker.Set();
            }
        }

        public object GetDynamicParameters()
        {
            _runtimeParamsCollection = new RuntimeDefinedParameterDictionary();
            if (File.Exists(XmlConfigPath) == false)
            {
                Console.WriteLine(Resources.InvalidXmlConfigPath);
                _isXmlPathValid = false;
                return _runtimeParamsCollection;
            }
            _controller = new DeploymentModelHelper(XmlConfigPath);
            _controller.Init();

            IEnumerable<string> paramsForModel = _controller.GetAllParameterNames;

            foreach (string paramForModel in paramsForModel)
            {
                RuntimeDefinedParameter dynamicParam = new RuntimeDefinedParameter()
                {
                    Name = paramForModel,
                    ParameterType = typeof(string),
                };
                dynamicParam.Attributes.Add(new ParameterAttribute() { Mandatory = false });
                _runtimeParamsCollection.Add(paramForModel, dynamicParam);
            }

            return _runtimeParamsCollection;
        }

        // check for required parameters / invalid values etc.
        private bool ValidateParameters()
        {
            bool bRet = true;
            IEnumerable<string> paramsForModel = _controller.GetAllParameterNames;

            foreach (string paramForModel in paramsForModel)
            {
                //Console.WriteLine("param: " + paramForModel + ", reqd: " + _controller.IsParamValueRequired(paramForModel) +
                //    ", XML value: " + _controller.GetParameterByName(paramForModel) + ", cmdline value: " + GetDynamicParamValue(paramForModel));
                if (_controller.IsParamValueRequired(paramForModel))
                {
                    string value = GetParamValue(paramForModel);
                    if (string.IsNullOrEmpty(value))
                    {
                        Log(LogType.Error, "Missing required value for parameter: " + paramForModel);
                        bRet = false;
                    }
                }
            }

            return bRet;
        }

        // This is the main method using with parameter values should be obtained. It takes care of overrides etc.
        private string GetParamValue(string paramName)
        {
            String paramValueFromXml = _controller.GetParameterByName(paramName);
            String paramValueFromCmdline = GetDynamicParamValue(paramName);

            // Value of parameter set inside cmdline has higher precedence than the one inside xml.
            string value = String.IsNullOrEmpty(paramValueFromCmdline) ? paramValueFromXml : paramValueFromCmdline;

            // if no value given, see if ValuePrefixRef and ValueSuffix are available, and combine them to get the value
            if (string.IsNullOrEmpty(value))
            {
                string valuePrefixRef = _controller.GetParamValuePrefixRef(paramName);
                if (string.IsNullOrEmpty(valuePrefixRef))
                    return null;

                string valuePrefix = GetParamValue(valuePrefixRef);
                if (string.IsNullOrEmpty(valuePrefix))
                    return null;

                string valueSuffix = _controller.GetParamValueSuffix(paramName);

                value = valuePrefix + valueSuffix;
            }

            string foramttedValue = ReplaceNotations(value);
            return foramttedValue;
        }

        private string GetDynamicParamValue(string paramName)
        {
            RuntimeDefinedParameter paramDef;
            _runtimeParamsCollection.TryGetValue(paramName, out paramDef);

            return (paramDef == null || paramDef.Value == null) ? String.Empty : paramDef.Value.ToString();
        }

        private bool IsEmulator()
        {
            string paramValue = GetParamValue("Emulator");
            if (string.IsNullOrEmpty(paramValue) == true)
            {
                return true; //If model.xml does not specify the Emualator option then we assume that it is emulator.
            }
            return bool.Parse(paramValue);
        }

        /// <summary>
        /// Method to replace special notations inside param values with actual values.
        /// Currently %%Program Files%% will be replaced with -- [WINDIR]:\Progam Files(x86) for 64 Bit OS & [WINDIR]:\Progam Files for 32 Bit OS
        /// %%windir%% will be replaced with - [WINDIR]
        /// </summary>
        /// <param name="paramValue">Param value to be processed</param>
        /// <returns>Processed param value</returns>
        private static string ReplaceNotations(string paramValue)
        {
            string formattedValue = paramValue;
            if (paramValue.Contains(programFileNotation) == true)
            {
                string programFileLoc = GetProgramFileLocation();
                formattedValue = formattedValue.Replace(programFileNotation, programFileLoc);
            }
            else if (paramValue.Contains(systemDriveNotation) == true)
            {
                string winDir = GetWinDir();
                formattedValue = formattedValue.Replace(systemDriveNotation, winDir);
            }
            return formattedValue;
        }

        private static string GetProgramFileLocation()
        {
            string windir = GetWinDir();
            string programFileLocation = string.Empty;

            using (ManagementObjectSearcher osDetailsFetcher = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem"))
            using (ManagementObjectCollection results = osDetailsFetcher.Get())
            {
                foreach (ManagementBaseObject eachResult in results)
                {
                    using (eachResult)
                    {
                        string osArchitecture = eachResult["OSArchitecture"].ToString();
                        programFileLocation = osArchitecture == "64-bit" ? Path.Combine(windir, "Program Files (x86)") : Path.Combine(windir, "Program Files");
                        break;
                    }
                }
            }
            return programFileLocation;
        }

        private static string GetWinDir()
        {
            string windirEnvVar = Environment.GetEnvironmentVariable("windir");
            return Path.GetPathRoot(windirEnvVar);
        }

        private bool TryParsePubSettingFilePathParam(out string pubSettingsFilePath)
        {
            string parmaValue = GetParamValue("PublishSettingsFilePath");
            if (File.Exists(parmaValue) == true)
            {
                pubSettingsFilePath = parmaValue;
                return true;
            }
            pubSettingsFilePath = null;
            return false;
        }

        private static void Log(LogType type, string message)
        {
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0}: {1}", type.ToString(), string.IsNullOrEmpty(message) ? "<empty>" : message));
        }
    }

    internal class NativeMethods
    {
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        internal static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint dwFlags, IntPtr hToken, out string pszPath);

        private NativeMethods()
        {
        }
    }
}
