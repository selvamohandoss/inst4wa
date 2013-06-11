using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Management.Automation;
using DeployCmdlets4WA.Utilities;
using System.IO;
using DeployCmdlets4WA.Properties;
using System.Globalization;

namespace DeployCmdlets4WA.Cmdlet
{
    //TODO Fixed: Change name to New-AzureVMsFromImage to keep consistent with Azure nomenclature
    [Cmdlet(VerbsCommon.New, "AzureVMsFromImage")]
    public class NewAzureVMsFromImage : PSCmdlet
    {
        private const int _retryCount = 5;
        private const int _waitPeriod = 10000;
        private bool _force;

        [Parameter(Mandatory = true)]
        public int Count { get; set; }

        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public string ImageName { get; set; }

        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public string Service { get; set; }

        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public string Location { get; set; }

        [Parameter(Mandatory = false)]
        [ValidateNotNullOrEmpty]
        public string VMNamePrefix { get; set; }

        [Parameter(Mandatory = false)]
        [ValidateNotNullOrEmpty]
        public string InstanceSize { get; set; }

        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public string AdminPassword { get; set; }

        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public string AdminUsername { get; set; }

        [Parameter(Mandatory = true)]
        public string Force { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();

            //Suppress progress messages..
            PSVariable progressPreference = base.SessionState.PSVariable.Get("ProgressPreference");
            base.SessionState.PSVariable.Set("ProgressPreference", "SilentlyContinue");

            Prevalidate(this.Count, this.Force);

            Service = Service.Trim();
            VMNamePrefix = VMNamePrefix.Trim();

            //Set default value of optional params.
            InstanceSize = String.IsNullOrEmpty(InstanceSize) == true ? "Small" : InstanceSize;
            VMNamePrefix = String.IsNullOrEmpty(VMNamePrefix) == true ? "Win2k8VM" : VMNamePrefix;

            //Create service if it does not exist.
            CreateServiceIfNotExist();

            //Create new VMs.
            CreateVMs();

            //Wait till ready.
            WaitTillReady();

            //Restore the variable value back.
            base.SessionState.PSVariable.Set("ProgressPreference", progressPreference.Value);
        }

        private void Prevalidate(int count, string force)
        {
            if (count <= 0)
            {
                throw new ArgumentException(Resources.VMCountInvalid, "count");
            }
            if (bool.TryParse(force.Trim().ToUpperInvariant(), out _force) == false)
            {
                throw new ArgumentException(Resources.ForceParamInvalid, "force");
            }
        }

        private void CreateServiceIfNotExist()
        {
            bool exists = ServiceExists();

            if (exists == true)
            {
                WriteObject(string.Format(CultureInfo.InvariantCulture, Resources.ServiceExists, Service));
                return;
            }

            CreateService();
        }

        private bool ServiceExists()
        {
            string command = string.Format("@(Get-AzureService | % {{ $_ }} | WHERE-Object {{$_.ServiceName -eq \"{0}\" }}).Count", Service.Trim());

            ExecutePSCmdlet executeGetServiceCmd = new ExecutePSCmdlet();
            executeGetServiceCmd.Execute(string.Format(CultureInfo.InvariantCulture, Resources.VerifyIfServiceExist, Service), command);

            if (executeGetServiceCmd.OutputData == null || executeGetServiceCmd.OutputData.Count() == 0)
            {
                return false;
            }
            return int.Parse(executeGetServiceCmd.OutputData.ElementAt(0).ToString()) == 1;
        }

        private void CreateService()
        {
            string createServiceCmd = string.Format(CultureInfo.InvariantCulture, "New-AzureService -ServiceName \"{0}\" -Location \"{1}\" ", Service, Location);

            ExecutePSCmdlet executeCreateServiceCmd = new ExecutePSCmdlet();
            executeCreateServiceCmd.Execute(string.Format(CultureInfo.InvariantCulture, Resources.CreatingService, Service), createServiceCmd);

            if (executeCreateServiceCmd.ErrorOccurred == true)
            {
                throw new ApplicationFailedException(string.Format(CultureInfo.InvariantCulture, Resources.ErrorCreatingService, Service));
            }
        }

        private void CreateVMs()
        {
            int i = 1;
            int waitPeriodInSeconds = _waitPeriod / 1000;
            string createVMCmdTemplate = "New-AzureVMConfig -Name \"{0}\" -InstanceSize {1} -ImageName \"{2}\" |  Add-AzureProvisioningConfig -Windows -Password {3} -AdminUserName \"{4}\" -EnableWinRMHttp | New-AzureVM –ServiceName \"{5}\"";

            List<string> existingVMs = GetExistingVMs();

            for (int j = 0; j < Count; j++)
            {
                string vmname = string.Concat(VMNamePrefix, "-", i++);
                ValidateVMName(vmname);
                
                //If VM with same name already exists and we are not supposed to delete it..Then give message and continue.
                if (existingVMs.Contains(vmname.ToUpperInvariant()) == true)
                {
                    if (_force == false)
                    {
                        WriteObject(string.Format(CultureInfo.InvariantCulture, Resources.VMAlreadyExists, vmname));
                        continue;
                    }
                    else
                    {
                        DeleteVMWithOSDisk(vmname);
                    }
                }

                int currentRetryCount = 1;
                string createVMCmd = string.Format(CultureInfo.InvariantCulture, createVMCmdTemplate, new string[] { vmname, InstanceSize, ImageName, AdminPassword, AdminUsername, Service });

                while (true)
                {
                    ExecutePSCmdlet executeCreateAzureVMCmd = new ExecutePSCmdlet();
                    executeCreateAzureVMCmd.Execute(string.Format(CultureInfo.InvariantCulture, "Creating VM {0}", vmname), createVMCmd);
                    if (executeCreateAzureVMCmd.ErrorOccurred == true)
                    {
                        if (currentRetryCount == _retryCount)
                        {
                            throw new ApplicationFailedException(string.Format(CultureInfo.InvariantCulture, "Attempt to create VM {0} failed.", vmname));
                        }
                        else
                        {
                            WriteWarning(string.Format(CultureInfo.InvariantCulture, Resources.WarnAzureVMCreationFailed, vmname, waitPeriodInSeconds));
                            System.Threading.Thread.Sleep(waitPeriodInSeconds);
                        }
                    }
                    else
                    {
                        WriteObject(string.Format(CultureInfo.InvariantCulture, "VM {0} created.", vmname));
                        break;
                    }
                    currentRetryCount++;
                }
            }
        }

        private List<string> GetExistingVMs()
        {
            string getAzureVMCmd = string.Format(CultureInfo.InvariantCulture, "Get-AzureVM -ServiceName \"{0}\"", Service);

            ExecutePSCmdlet executeGetAzureVMCmd = new ExecutePSCmdlet();
            executeGetAzureVMCmd.Execute(string.Format(CultureInfo.InvariantCulture, Resources.FetchingExistingVMs, Service), getAzureVMCmd);

            List<string> vms = new List<string>();

            if (executeGetAzureVMCmd.ErrorOccurred == true)
            {
                return vms;
            }
            
            if (executeGetAzureVMCmd.OutputData != null)
            {
                foreach (PSObject eachVMDetails in executeGetAzureVMCmd.OutputData)
                {
                    PSPropertyInfo nameProperty = eachVMDetails.Properties["Name"];
                    if (nameProperty != null)
                    {
                        vms.Add(eachVMDetails.Properties["Name"].Value.ToString().ToUpperInvariant());
                    }
                    else
                    {
                        //We don't have name property in the received output object...so just print the output.
                        eachVMDetails.ToString();
                    }
                }
            }
            return vms;
        }

        private void WaitTillReady()
        {
            string getAzureRoleCmd = string.Format(CultureInfo.InvariantCulture, "Get-AzureRole -ServiceName \"{0}\" -Slot Production  -InstanceDetails", Service);

            while (true)
            {
                System.Threading.Thread.Sleep(5000);
                ExecutePSCmdlet executeGetAzureRoleStatus = new ExecutePSCmdlet();
                executeGetAzureRoleStatus.Execute("Fetching the VM status.", getAzureRoleCmd);
                if (executeGetAzureRoleStatus.ErrorOccurred == true)
                {
                    throw new ApplicationFailedException("Error fetching VM status.");
                }

                bool areAllReady = true;
                foreach (PSObject eachResult in executeGetAzureRoleStatus.OutputData)
                {
                    string vmstatus = eachResult.Properties["InstanceStatus"].Value.ToString();
                    string vmName = eachResult.Properties["InstanceName"].Value.ToString();

                    if (vmstatus != "ReadyRole")
                    {
                        areAllReady = false;
                    }

                    if (vmstatus == "ProvisioningFailed")
                    {
                        throw new ApplicationFailedException(string.Format(CultureInfo.InvariantCulture, "Failed to initialize VM {0}.", vmName));
                    }

                    WriteObject(string.Format(CultureInfo.InvariantCulture, "Status of VM {0} is {1}", vmName, vmstatus));
                }

                if (areAllReady == true)
                {
                    break;
                }
            }
        }

        private void DeleteVMWithOSDisk(string vm)
        {
            //Get VM Disk Name.
            string disk = GetOSDiskForVM(vm);

            //Delete VM.
            DeleteVM(vm);

            //Delete OS Disk.
            DeleteOSDisk(disk);
        }

        private string GetOSDiskForVM(string vm)
        {
            string getAzureOSDiskCmd = string.Format(CultureInfo.InvariantCulture, "Get-AzureVM -ServiceName \"{0}\" -Name \"{1}\" | Get-AzureOSDisk ", Service, vm);

            ExecutePSCmdlet executeGetAzureOSDiskCmd = new ExecutePSCmdlet();
            executeGetAzureOSDiskCmd.Execute(string.Format(CultureInfo.InvariantCulture, Resources.FetchigOSDiskForVM, vm), getAzureOSDiskCmd);

            if (executeGetAzureOSDiskCmd.ErrorOccurred == true)
            {
                throw new ApplicationFailedException(string.Format(CultureInfo.InvariantCulture, Resources.ErrorFetchingOSDiskForVM, vm));
            }

            string osDiskName = executeGetAzureOSDiskCmd.OutputData.ElementAt(0).Properties["DiskName"].Value.ToString();
            WriteObject(string.Format(CultureInfo.InvariantCulture, Resources.OSDiskNameForVM, vm, osDiskName));
            return osDiskName;
        }

        private void DeleteVM(string vm)
        {
            int currentRetryCount = 1;
            int waitPeriodInSeconds = _waitPeriod / 1000;

            while (true)
            {
                string removeVMCmd = string.Format(CultureInfo.InvariantCulture, "Remove-AzureVM -ServiceName \"{0}\" -Name \"{1}\" ", Service, vm);

                ExecutePSCmdlet executeRemoveVMCmd = new ExecutePSCmdlet();
                executeRemoveVMCmd.Execute(string.Format(CultureInfo.InvariantCulture, Resources.DeletingVM, vm), removeVMCmd);

                if (executeRemoveVMCmd.ErrorOccurred == true)
                {
                    if (currentRetryCount == _retryCount)
                    {
                        throw new ApplicationFailedException(string.Format(CultureInfo.InvariantCulture, Resources.ErrorDeletingVM, vm));
                    }
                    else
                    {
                        WriteWarning(string.Format(CultureInfo.InvariantCulture, Resources.WarnAzureVMDeleteError, vm, waitPeriodInSeconds));
                        System.Threading.Thread.Sleep(_waitPeriod); //Wait before retry.
                    }
                }
                else
                {
                    WriteObject(string.Format(CultureInfo.InvariantCulture, Resources.VMDeletionSuccess, vm));
                    break;
                }
                currentRetryCount++;
            }
        }

        private void DeleteOSDisk(string disk)
        {
            string removeAzureDiskCmd = string.Format(CultureInfo.InvariantCulture, "Remove-AzureDisk -DiskName \"{0}\" -DeleteVHD", disk);

            ExecutePSCmdlet executeRemoveAzureDiskCmd = new ExecutePSCmdlet();
            executeRemoveAzureDiskCmd.Execute(string.Format(CultureInfo.InvariantCulture, Resources.DeletingAzureDisk, disk), removeAzureDiskCmd);

            if (executeRemoveAzureDiskCmd.ErrorOccurred == true)
            {
                throw new ApplicationFailedException(string.Format(CultureInfo.InvariantCulture, Resources.ErrorDeletingAzureDisk, disk));
            }

            WriteObject(string.Format(CultureInfo.InvariantCulture, Resources.AzureDiskDeleted, disk));
        }

        private void ValidateVMName(string vmName)
        {
            char[] invalidChars = new char[]{'~', '!','@','#', '$', '%', '^', '&', '*','(',')', '=', '+','_', '[', ']', '{', '}', '\\', '|', ';', ':','.', '\'', '"', ',', '<','/', '?','.'};
            if(
                vmName.Length > 15 || //Lenght should not be greater than 15 characters
                vmName.Where(e => char.IsLetter(e) == true).Count() == 0 || //VM Name cannot contain numbers only.
                vmName.Where(e => invalidChars.Contains(e) == true).Count() > 0 //Make sure that VM name does not have invalid characters.
              )
            {
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, Resources.InvalidVMName, vmName), "vmName");
            }
        }
    }
}
