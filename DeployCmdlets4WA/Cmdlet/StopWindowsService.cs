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
using System.ServiceProcess;
using DeployCmdlets4WA.Properties;

namespace DeployCmdlets4WA.Cmdlet
{
    [Cmdlet(VerbsLifecycle.Stop, "WindowsService")]
    public class StopWindowsService : PSCmdlet
    {
        [Parameter(Mandatory = true, HelpMessage = "Service name.")]
        [ValidateNotNullOrEmpty]
        public string Service { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();

            using(ServiceController controller = new ServiceController(Service))
            {
                if (controller.Status == ServiceControllerStatus.Stopped)
                {
                    WriteObject(string.Format(Resources.WinServiceAlreadyStopped, Service));
                }
                else
                {
                    controller.Stop();
                    WriteObject(Resources.WinServiceStopping);
                    controller.WaitForStatus(ServiceControllerStatus.Stopped);
                    WriteObject(string.Format(Resources.WinServiceStopped, Service));
                }
            }
        }
    }
}
