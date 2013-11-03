using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Management.Automation;
using DeployCmdlets4WA.Utilities;
using System.Globalization;

namespace DeployCmdlets4WA.Cmdlet
{
    [Cmdlet(VerbsCommon.Add, "Endpoints")]
    public class AddEndpoints : PSCmdlet
    {
        [Parameter(Mandatory = true)]
        public int Count { get; set; }

        [Parameter(Mandatory = true)]
        public string Service { get; set; }

        [Parameter(Mandatory = false)]
        public string VMNamePrefix { get; set; }

        [Parameter(Mandatory = true)]
        public int PrivatePort { get; set; }

        [Parameter(Mandatory = true)]
        public int PublicPort { get; set; }

        [Parameter(Mandatory = true)]
        public string Protocol { get; set; }

        [Parameter(Mandatory = true, HelpMessage = "This parameter is used as prefix for endpoint if endpoint is not Load balanced. If endpoint is load balanced then this name is used as a group name of load balanced endpoints.")]
        public string EndpointName { get; set; }

        [Parameter(Mandatory = false)]
        public SwitchParameter LoadBalance { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();

            //Suppress progress messages..
            PSVariable progressPreference = base.SessionState.PSVariable.Get("ProgressPreference");
            base.SessionState.PSVariable.Set("ProgressPreference", "SilentlyContinue");

            //Get VM Names.
            string[] vmNames = GetVMNames();

            //Add Endpoints.
            if (LoadBalance.IsPresent == false)
            {
                AddEndPointsToVMs(vmNames);
            }
            else
            {
                AddLBEndPointsToVMs(vmNames);
            }

            //Restore the variable value back.
            base.SessionState.PSVariable.Set("ProgressPreference", progressPreference.Value);
        }

        private void AddLBEndPointsToVMs(string[] vmNames)
        {
            string cmdTemplate = "Get-AzureVM -ServiceName \"{0}\" -Name \"{1}\" | Add-AzureEndpoint -LBSetName \"{2}\" -Name \"{2}\" -ProbeProtocol \"{3}\"  -Protocol \"{3}\" -PublicPort {4} -ProbePort {4} -LocalPort {5} | Update-AzureVM -verbose";
            //DEBUG ENABLED - string cmdTemplate = "Get-AzureVM -ServiceName \"{0}\" -Name \"{1}\" | Add-AzureEndpoint -LBSetName \"{2}\" -Name \"{2}\" -ProbeProtocol \"{3}\"  -Protocol \"{3}\" -PublicPort {4} -ProbePort {4} -LocalPort {5} | Update-AzureVM -verbose -debug";

            string messageTemplate = "Adding load balanced endpoint for the VM '{0}'";
            string failedMessageTemplate = "Failed to add load balanced endpoint VM '{0}";
            string successMessageTemplate = "Added load balanced endpoint for the VM '{0}";

            for (int i = 0; i < vmNames.Length; i++)
            {
                string[] cmdParams = new string[] { Service, vmNames[i], EndpointName, Protocol, PublicPort.ToString(CultureInfo.InvariantCulture), PrivatePort.ToString(CultureInfo.InvariantCulture) };
                string cmd = string.Format(CultureInfo.InvariantCulture, cmdTemplate, cmdParams);

                ExecutePSCmdlet executeAddAzureEndPointCmd = new ExecutePSCmdlet();
                string message = string.Format(CultureInfo.InvariantCulture, messageTemplate, vmNames[i]);
                executeAddAzureEndPointCmd.Execute(message, cmd);
                if (executeAddAzureEndPointCmd.ErrorOccurred == true)
                {
                    throw new ApplicationFailedException(string.Format(CultureInfo.InvariantCulture, failedMessageTemplate, vmNames[i]));
                }
                WriteObject(string.Format(CultureInfo.InvariantCulture, successMessageTemplate, vmNames[i]));
            }
        }

        private void AddEndPointsToVMs(string[] vmNames)
        {
            string endpointNameTemplate = EndpointName + "-{0}-{1}";
            //DEBUG ENABLED string cmdTemplate = "Get-AzureVM -ServiceName \"{0}\" -Name \"{1}\" | Add-AzureEndpoint -Name \"{2}\" -Protocol \"{3}\" -PublicPort {4} -LocalPort {5} | Update-AzureVM -verbose -debug";
            string cmdTemplate = "Get-AzureVM -ServiceName \"{0}\" -Name \"{1}\" | Add-AzureEndpoint -Name \"{2}\" -Protocol \"{3}\" -PublicPort {4} -LocalPort {5} | Update-AzureVM -verbose";

            string messageTemplate = "Adding endpoint mapping [{0}, {1}] for the VM '{2}'";
            string failedMessageTemplate = "Failed to add endpoint VM '{0}";
            string successMessageTemplate = "Endpoint mapping [{0}, {1}] added for the VM '{2}";

            for (int i = 0; i < vmNames.Length; i++)
            {
                int publicPort = PublicPort + i;
                string[] cmdParams = new string[] { Service, vmNames[i], string.Format(CultureInfo.InvariantCulture, endpointNameTemplate, PrivatePort, publicPort), Protocol, publicPort.ToString(CultureInfo.InvariantCulture), PrivatePort.ToString(CultureInfo.InvariantCulture) };
                string cmd = string.Format(CultureInfo.InvariantCulture, cmdTemplate, cmdParams);

                ExecutePSCmdlet executeAddAzureEndPointCmd = new ExecutePSCmdlet();
                string message = string.Format(CultureInfo.InvariantCulture, messageTemplate, publicPort, PrivatePort, vmNames[i]);
                executeAddAzureEndPointCmd.Execute(message, cmd);
                if (executeAddAzureEndPointCmd.ErrorOccurred == true)
                {
                    throw new ApplicationFailedException(string.Format(CultureInfo.InvariantCulture, failedMessageTemplate, vmNames[i]));
                }
                WriteObject(string.Format(CultureInfo.InvariantCulture, successMessageTemplate, publicPort, PrivatePort, vmNames[i]));
            }
        }

        private string[] GetVMNames()
        {
            int i = 1;
            string[] vmNames = new string[Count];
            for (int j = 0; j < Count; j++)
            {
                vmNames[j] = string.Concat(VMNamePrefix, "-", i++);
            }
            return vmNames;
        }
    }
}
