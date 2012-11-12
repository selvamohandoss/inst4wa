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
using DeployCmdlets4WA.Utilities;
using DeployCmdlets4WA.Properties;
using DeployCmdlets4WA.Cmdlet.ServiceConfigurationSchema;
using DeployCmdlets4WA.Cmdlet.ServiceDefinitionSchema;
using System.Globalization;

namespace DeployCmdlets4WA.Cmdlet
{
    [Cmdlet(VerbsCommon.Add, "AzureWorkerRole")]
    public class AddAzureWorkerRole : PSCmdlet
    {
        [Parameter(Mandatory = true, HelpMessage = "Location of folder containing role binaries.")]
        [ValidateNotNullOrEmpty]
        public string RoleBinariesFolder { get; set; }

        [Parameter(Mandatory = true, HelpMessage = "Location of CSCFG file containing role configuration.")]
        [ValidateNotNullOrEmpty]
        public string CscfgFile { get; set; }

        [Parameter(Mandatory = true, HelpMessage = "Location of CSDEF file containing role definition.")]
        [ValidateNotNullOrEmpty]
        public string CsdefFile { get; set; }

        [Parameter(Mandatory = true, HelpMessage = "Name of the worker role.")]
        [ValidateNotNullOrEmpty]
        public string RoleName { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Size of VM. Possible values are ExtraSmall,Small,Medium,Large or ExtraLarge.")]
        [ValidateNotNullOrEmpty]
        public RoleSize? VMSize { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Number of instances of worker role.")]
        public int InstanceCount { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            
            //Step 1 - Validate Params.
            ValidateInputs(this.RoleBinariesFolder, this.CscfgFile, this.CsdefFile);

            //Step 2 - Copy CSCFG and CSDEF Settings.
            UpdateCSCFG();
            UpdateCSDEF();

            //Step 3 - Copy Role Binaries
            CopyBinaries();
        }

        private void ValidateInputs(string roleBinariesFolder, string cscfgFile, string csdefFile)
        {
            if (Directory.Exists(roleBinariesFolder) == false)
            {
                throw new ArgumentException(Resources.InvalidRoleBinaryFolder, "roleBinariesFolder");
            }
            if (File.Exists(cscfgFile) == false)
            {
                throw new ArgumentException(Resources.CSCFGFileDoesNotExist, "cscfgFile");
            }
            if (File.Exists(csdefFile) == false)
            {
                throw new ArgumentException(Resources.CSDEFFileDoesNotExist, "csdefFile");
            }
            if (File.Exists(LocalCSCFGFile) == false || File.Exists(CloudCSCFGFile) == false || File.Exists(ServiceCSDEFFile) == false)
            {
                throw new ArgumentException(Resources.ServiceRootInvalid);
            }
        }

        private void CopyBinaries()
        {
            string roleDir = Path.Combine(CurrentLocation, this.RoleName);
            if (Directory.Exists(roleDir) == true)
            {
                WriteObject(string.Format(CultureInfo.InvariantCulture, Resources.RoleDirAlreadyPresent, this.RoleName));
            }
            else
            {
                Directory.CreateDirectory(roleDir);
                ExecuteCommands.ExecuteCommand(string.Format(CultureInfo.InvariantCulture, "COPY-ITEM \"{0}\" \"{1}\" -recurse", Path.Combine(this.RoleBinariesFolder, "*"), roleDir), this.Host);
            }
        }

        private void UpdateCSDEF()
        {
            string normalizedRoleName = this.RoleName.ToUpperInvariant();

            ServiceDefinitionSchema.ServiceDefinition destCSDEF = SerializationUtils.DeserializeXmlFile<ServiceDefinitionSchema.ServiceDefinition>(ServiceCSDEFFile);

            if (destCSDEF.WorkerRole != null)
            {
                //Check if destination csdef already has a role.
                ServiceDefinitionSchema.WorkerRole roleToAddInDestCSDEF = destCSDEF.WorkerRole.Where(eachRole => eachRole.name.ToUpperInvariant() == normalizedRoleName).FirstOrDefault();
                //If found just update VMSize and exit.
                if (roleToAddInDestCSDEF != null)
                {
                    roleToAddInDestCSDEF.vmsize = this.VMSize == null ? roleToAddInDestCSDEF.vmsize : this.VMSize.Value;
                    SerializationUtils.SerializeXmlFile<ServiceDefinitionSchema.ServiceDefinition>(destCSDEF, ServiceCSDEFFile);
                    WriteObject(string.Format(CultureInfo.InvariantCulture, Resources.RoleAlreadyPresentInCSDEF, this.RoleName, roleToAddInDestCSDEF.vmsize.ToString()));
                    return;
                }
            }

            ServiceDefinitionSchema.ServiceDefinition sourceCSDEF = SerializationUtils.DeserializeXmlFile<ServiceDefinitionSchema.ServiceDefinition>(CsdefFile);
            if (sourceCSDEF.WorkerRole == null)
            {
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, Resources.RoleNotFoundInCSDEF, this.RoleName));
            }
            
            ServiceDefinitionSchema.WorkerRole roleToAdd = sourceCSDEF.WorkerRole.Where(eachRole => eachRole.name.ToUpperInvariant() == normalizedRoleName).FirstOrDefault();
            if (roleToAdd == null)
            {
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, Resources.RoleNotFoundInCSDEF, this.RoleName));
            }

            if (roleToAdd.Runtime == null || roleToAdd.Runtime.EntryPoint == null)
            {
                throw new ApplicationFailedException(string.Format(CultureInfo.InvariantCulture, Resources.EntryPointConfigMissing, this.RoleName));
            }

            List<ServiceDefinitionSchema.WorkerRole> workerRoles = new List<ServiceDefinitionSchema.WorkerRole>();
            //Retain old roles if any.
            if (destCSDEF.WorkerRole != null)
            {
                workerRoles.AddRange(destCSDEF.WorkerRole);
            }
            workerRoles.Add(roleToAdd);
            destCSDEF.WorkerRole = workerRoles.ToArray();

            SerializationUtils.SerializeXmlFile<ServiceDefinitionSchema.ServiceDefinition>(destCSDEF, ServiceCSDEFFile);
        }

        private void UpdateCSCFG()
        {
            string normalizedRoleName = this.RoleName.ToUpperInvariant();

            ServiceConfigurationSchema.ServiceConfiguration sourceCSCFG = SerializationUtils.DeserializeXmlFile<ServiceConfigurationSchema.ServiceConfiguration>(CscfgFile);
            ServiceConfigurationSchema.RoleSettings roleToAdd = sourceCSCFG.Role.Where(eachRole => eachRole.name.ToUpperInvariant() == normalizedRoleName).FirstOrDefault();

            if (roleToAdd == null)
            {
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, Resources.RoleNotFoundInCSCFG, this.RoleName));
            }

            ConfigureInstCount(roleToAdd);

            AddRole(roleToAdd, CloudCSCFGFile);
            AddRole(roleToAdd, LocalCSCFGFile);
        }

        private void ConfigureInstCount(RoleSettings roleToAdd)
        {
            if (roleToAdd.Instances == null)
            {
                roleToAdd.Instances = new TargetSetting();
            }
            roleToAdd.Instances.count = this.InstanceCount == 0 ? 1 : this.InstanceCount;
        }

        private void AddRole(RoleSettings roleToAdd, string cscfgFileLoc)
        {
            ServiceConfigurationSchema.ServiceConfiguration config = SerializationUtils.DeserializeXmlFile<ServiceConfigurationSchema.ServiceConfiguration>(cscfgFileLoc);
            
            string roleNameToAdd = roleToAdd.name.ToUpperInvariant();
            if(config.Role != null)
            {
                //Check if role to be added is already present inside the cscfg. If so just update instance count.
                RoleSettings matchingRoleInConfig = config.Role.Where(eachRole => eachRole.name.ToUpperInvariant() == roleNameToAdd).FirstOrDefault();
                if (matchingRoleInConfig != null)
                {
                    ConfigureInstCount(matchingRoleInConfig);
                    SerializationUtils.SerializeXmlFile<ServiceConfigurationSchema.ServiceConfiguration>(config, cscfgFileLoc);
                    WriteObject(string.Format(CultureInfo.InvariantCulture, Resources.RoleAlreadyPresentInCSCFG, roleNameToAdd, matchingRoleInConfig.Instances.count.ToString(CultureInfo.InvariantCulture)));
                    return;
                }
            }
            
            List<ServiceConfigurationSchema.RoleSettings> roles = new List<ServiceConfigurationSchema.RoleSettings>();
            //Retain existing roles inside the cscfg.
            if (config.Role != null)
            {
                roles.AddRange(config.Role);
            }
            roles.Add(roleToAdd);
            config.Role = roles.ToArray();
            SerializationUtils.SerializeXmlFile<ServiceConfigurationSchema.ServiceConfiguration>(config, cscfgFileLoc);
        }

        private string CloudCSCFGFile
        {
            get { return Path.Combine(CurrentLocation, Resources.CSCFGCloudFile); }
        }

        private string ServiceCSDEFFile
        {
            get { return Path.Combine(CurrentLocation, Resources.CSDEFFile); }
        }

        private string LocalCSCFGFile
        {
            get { return Path.Combine(CurrentLocation, Resources.CSCFGLocalFile); }
        }

        private string CurrentLocation
        {
            get { return this.SessionState.Path.CurrentLocation.Path; }
        }
    }
}
