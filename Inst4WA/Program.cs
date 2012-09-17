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
using System.Security.Principal;
using System.Diagnostics;
using System.Reflection;
using System.Xml;
using System.IO;

namespace Inst4WA
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine();
            Console.WriteLine("============================== Starting Inst4WA ==============================");

            if (!IsElevated || (args == null) || (args.Count() == 0) || ! args.Contains("-XmlConfigPath"))
            {
                ShowUsage();
            }
            else
            {
                RunInstaller(args);
            }

            Console.WriteLine("============================== Inst4WA shut down ==============================");
            Console.WriteLine();
        }

        private static bool IsElevated
        {
            get
            {
                return new WindowsPrincipal
                    (WindowsIdentity.GetCurrent()).IsInRole
                    (WindowsBuiltInRole.Administrator);
            }
        }

        private static void ShowUsage()
        {
            string msg = "Inst4WA:\r\n\r\n" +
                         "Installs open source packages to Windows Azure using settings specified in a configuration file.\r\n\r\n" +
                         "Usage:\r\n\r\n" +
                         "Start a command window in Administrator mode and then type:\r\n\r\n" +
                         "Inst4WA -XmlConfigPath <config file path> -Subscription <subscription name> -DomainName <domain name>\r\n\r\n" +
                         "Where:\r\n\r\n" +
                         "<config file path>: path to an XML config file containing settings for the service to be deployed\r\n" +
                         "<subscription name>: Windows Azure subscription name\r\n" +
                         "<domain name>: Unique name to be used to create the service to be deployed. The domain name will be used to generate other unique names such as storage account name etc.\r\n\r\n" +
                         "For example:\r\n\r\n" +
                         "Inst4WA.exe -XmlConfigPath \"DeploymentModelSolr.xml\" -DomainName \"foo\" -Subscription \"bar\"\r\n\r\n" +
                         "Please also refer to the sample config XML files included along with this tool.";

            Console.WriteLine(msg);
        }

        private static void RunInstaller(string[] args)
        {
            //Launch Powershell Window with Cmdlets loaded.
            String argsForCmdlet = GetArgsStringForCmdlet(args);

            string fmt = Inst4WA.Properties.Resources.CommandArgs;
            
            EnsureCorrectPowershellConfig();

            // create process info to run inst4wa
            Process p = new Process();

            p.StartInfo.FileName = "Powershell.exe";
            p.StartInfo.Arguments = String.Format(fmt, System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + @"\", argsForCmdlet);
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.WorkingDirectory = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            p.StartInfo.Verb = "runas";

            Console.WriteLine(p.StartInfo.FileName + " " + p.StartInfo.Arguments);
            p.Start();
            
            p.WaitForExit();
            p.Close();
        }

        private static void EnsureCorrectPowershellConfig()
        {
            List<string> locationOfPowershellExe = GetPowershellLocation();
            if (locationOfPowershellExe == null || locationOfPowershellExe.Count == 0)
            {
                throw new Exception("Unable to locate powershell exe.");
            }

            //Create config file if not exists.
            foreach (string eachLocation in locationOfPowershellExe)
            {
                string configFileLoc = System.IO.Path.Combine(eachLocation, "powershell.exe.config");
                if (File.Exists(configFileLoc) == false)
                {
                    FileStream configStream = File.Create(configFileLoc);
                    using (XmlTextWriter textWriter = new XmlTextWriter(configStream, null))
                    {
                        textWriter.WriteStartElement("configuration"); //Append Root node otherwise xmldocument cannot load the config file.
                        textWriter.WriteEndElement();
                    }
                    configStream.Close();
                    configStream.Dispose();
                }

                //Prepate config file.
                PrepareConfigFile(configFileLoc);

            }
        }

        private static void PrepareConfigFile(string configFileLoc)
        {
            //Load config file.
            XmlDocument configFile = new XmlDocument();

            string configFileContext = File.ReadAllText(configFileLoc);
            configFile.LoadXml(configFileContext);

            XmlNode configurationNode = configFile.SelectSingleNode("configuration");

            //Check if start up node is present.
            XmlNode startupNode = configurationNode.SelectSingleNode("startup");
            if (startupNode == null)
            {
                startupNode = configFile.CreateNode(XmlNodeType.Element, "startup", string.Empty);
                configurationNode.AppendChild(startupNode);
            }

            //Check runtime policy attribute if it does not exist.
            XmlAttribute runtimePolicyAttr = startupNode.Attributes["useLegacyV2RuntimeActivationPolicy"];
            if (runtimePolicyAttr == null)
            {
                runtimePolicyAttr = configFile.CreateAttribute("useLegacyV2RuntimeActivationPolicy");
                runtimePolicyAttr.Value = "true";
                startupNode.Attributes.Append(runtimePolicyAttr);
            }

            //Add supported runtime version node.
            string[] requiredVersions = new string[4] { "v3.5", "v3.0", "v2.0.50727", "v4.0.30319" };

            //Add supported runtime version node.
            foreach (string version in requiredVersions)
            {
                XmlNode supportedRunTimeVersionNode = startupNode.SelectSingleNode(string.Format("supportedRuntime[@version='{0}']", version));
                if (supportedRunTimeVersionNode == null)
                {
                    supportedRunTimeVersionNode = configFile.CreateElement("supportedRuntime");

                    XmlAttribute supportedRuntimeVer = configFile.CreateAttribute("version");
                    supportedRuntimeVer.Value = version;
                    supportedRunTimeVersionNode.Attributes.Append(supportedRuntimeVer);

                    startupNode.AppendChild(supportedRunTimeVersionNode);
                }
            }

            configFile.Save(configFileLoc);
        }

        private static List<string> GetPowershellLocation()
        {
            List<String> locationOfPowersehllExe = new List<String>();
            String windowsDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

            String locationOf64BitPowersehllExe = System.IO.Path.Combine(windowsDir, @"SysWOW64\WindowsPowerShell\v1.0");
            if (Directory.Exists(locationOf64BitPowersehllExe) == true)
            {
                locationOfPowersehllExe.Add(locationOf64BitPowersehllExe);
            }

            String locationOf32BitPowershellExe = System.IO.Path.Combine(windowsDir, @"System32\WindowsPowerShell\v1.0");
            if (Directory.Exists(locationOf32BitPowershellExe) == true)
            {
                locationOfPowersehllExe.Add(locationOf32BitPowershellExe);
            }

            return locationOfPowersehllExe;
        }

        private static string GetArgsStringForCmdlet(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                return string.Empty;
            }
            //Wrap value of args inside double quote.
            string[] argsWithValsInsideDoubleQuote = new string[args.Length];
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];

                if (arg.StartsWith("-"))
                    argsWithValsInsideDoubleQuote[i] = arg;
                else if (arg.Contains(' '))
                    argsWithValsInsideDoubleQuote[i] = String.Concat("\\\"", arg, "\\\"");
                else
                    argsWithValsInsideDoubleQuote[i] = String.Concat("\"", arg, "\"");
            }
            return string.Join(" ", argsWithValsInsideDoubleQuote);
        }
    }
}
