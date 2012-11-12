using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Management.Automation;
using DeployCmdlets4WA.Utilities;
using DeployCmdlets4WA.Properties;
using System.IO;
using System.Globalization;

namespace DeployCmdlets4WA.Cmdlet
{
    [Cmdlet(VerbsCommon.New, "ConfigJson")]
    public class NewConfigJson : PSCmdlet
    {
        private string _ipAddress;
        private string _ports;

        [Parameter(Mandatory = true)]
        public int Count { get; set; }

        [Parameter(Mandatory = true)]
        public string Service { get; set; }

        [Parameter(Mandatory = true)]
        public string AdminPassword { get; set; }

        [Parameter(Mandatory = true)]
        public string EndpointPrefix { get; set; }

        [Parameter(Mandatory = false)]
        public string VMNamePrefix { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();

            //System.Diagnostics.Debugger.Launch();

            //Get VM Names.
            string[] vmNames = GetVMNames();

            //Fetch VM Details.
            FetchVMConfig(vmNames);

            //Write Config.json file.
            WriteConfigJson();
        }

        private void WriteConfigJson()
        {
            string[] jsonVals = new string[] { string.Format(CultureInfo.InvariantCulture, "{0}.cloudapp.net", Service), "Administrator", AdminPassword, _ports, _ipAddress, Service };
            string contents = string.Format(CultureInfo.InvariantCulture, Resources.ConfigJson, jsonVals);

            File.WriteAllText("config.json", contents);
        }

        private void FetchVMConfig(string[] vmNames)
        {
            foreach (string vm in vmNames)
            {
                string ip = GetIPAddress(vm);
                string port = GetPort(vm);

                _ipAddress += WrapInQuotes(ip) + ",";
                _ports += WrapInQuotes(port) + ",";
            }
            //Remove extra comma.
            _ipAddress = _ipAddress.Substring(0, _ipAddress.Length - 1);
            _ports = _ports.Substring(0, _ports.Length - 1);
        }

        private string GetIPAddress(string vmName)
        {
            string getAzureVMCmd = string.Format(CultureInfo.InvariantCulture, "Get-AzureVM -ServiceName \"{0}\" -Name \"{1}\" ", Service, vmName);
            string message = string.Format(CultureInfo.InvariantCulture, "Fetching details for VM {0}", vmName);

            ExecutePSCmdlet executeGetAzureVMCmd = new ExecutePSCmdlet();
            executeGetAzureVMCmd.Execute(message, getAzureVMCmd);
            if (executeGetAzureVMCmd.ErrorOccurred == true)
            {
                throw new ApplicationFailedException(string.Format(CultureInfo.InvariantCulture, "Error fetching details for VM '{0}'", vmName));
            }

            return executeGetAzureVMCmd.OutputData.ElementAt(0).Properties["IpAddress"].Value.ToString();
        }

        private string GetPort(string vmName)
        {
            string getAzureEndPointCmd = string.Format(CultureInfo.InvariantCulture, "Get-AzureVM -ServiceName \"{0}\" -Name \"{1}\" | Get-AzureEndPoint", Service, vmName);
            string message = string.Format(CultureInfo.InvariantCulture, "Fetching Ports for VM '{0}'", vmName);

            ExecutePSCmdlet executeGetAzureEndPointsCmd = new ExecutePSCmdlet();
            executeGetAzureEndPointsCmd.Execute(message, getAzureEndPointCmd);
            if (executeGetAzureEndPointsCmd.ErrorOccurred == true)
            {
                throw new ApplicationFailedException(string.Format(CultureInfo.InvariantCulture, "Error fetching Ports for VM '{0}'", vmName));
            }

            string port = string.Empty;
            foreach (PSObject eachEndPoint in executeGetAzureEndPointsCmd.OutputData)
            {
                string endPointName = eachEndPoint.Properties["Name"].Value.ToString();
                if (endPointName.Contains(EndpointPrefix) == true)
                {
                    port = eachEndPoint.Properties["Port"].Value.ToString();
                    break;
                }
            }
            return port;
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

        private static string WrapInQuotes(string stringToWrap)
        {
            return string.Concat("\"", stringToWrap, "\"");
        }
    }
}
