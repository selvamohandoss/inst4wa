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
using System.IO;
using System.Reflection;
using System.Collections.ObjectModel;
using DeployCmdlets4WA.Properties;
using System.Globalization;

namespace DeployCmdlets4WA.Cmdlet
{
    [Cmdlet(VerbsLifecycle.Install, "NodeJsModules")]
    public class AddNodeJSModules : PSCmdlet
    {
        [Parameter(Mandatory = true, HelpMessage = "Location to install node modules.")]
        [ValidateNotNullOrEmpty]
        public string InstallLoc { get; set; }

        [Parameter(Mandatory = true, HelpMessage = "Space separated list of modules to be installed.")]
        [ValidateNotNullOrEmpty]
        public string Modules { get; set; }

        protected override void ProcessRecord()
        {
            PreValidate(this.InstallLoc);

            base.ProcessRecord();

            string script = GetInstallScript();
            InstallNodeJsModules(script);

            PostValidate();
        }

        private static void PreValidate(string installLoc)
        {
            if (Directory.Exists(installLoc) == false)
            {
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, Resources.InvalidInstallLoc, installLoc), "installLoc");
            }
        }

        private void PostValidate()
        {
            //Check if the directories are created for each module.
            string[] modules = Modules.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            string nodeModulesLoc = Path.Combine(InstallLoc, "node_modules");
            string errorModules = string.Empty;
            foreach (string eachModule in modules)
            {
                if (Directory.Exists(Path.Combine(nodeModulesLoc, eachModule)) == false)
                {
                    errorModules = String.Concat(errorModules, eachModule, " ");
                }
            }
            if (string.IsNullOrEmpty(errorModules) == false)
            {
                throw new ApplicationFailedException(string.Format(CultureInfo.InvariantCulture, Resources.ErrorInstallingModules, errorModules));
            }
        }

        private static string GetInstallScript()
        {
            string installScript = null;
            using (Stream ps1Stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("DeployCmdlets4WA.Resources.InstallNodeJSModules.ps1"))
            {
                using(StreamReader ps1Reader = new StreamReader(ps1Stream))
                {
                     installScript = ps1Reader.ReadToEnd();
                }
            }
            return installScript;
        }

        private void InstallNodeJsModules(string installScript)
        {
            using (PowerShell scriptExecuter = PowerShell.Create())
            {
                scriptExecuter.AddScript(installScript);
                scriptExecuter.AddArgument(InstallLoc);
                scriptExecuter.AddArgument(Modules);
                scriptExecuter.AddParameter("Verb", "runas");
                Collection<PSObject> result = scriptExecuter.Invoke();
                foreach (PSObject eachResult in result)
                {
                    Console.WriteLine(eachResult.ToString());
                }
            }
        }
    }
}
