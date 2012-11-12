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
using System.Text.RegularExpressions;
using DeployCmdlets4WA.Properties;
using Microsoft.WindowsAzure.Management.XmlSchema;
using DeployCmdlets4WA.Utilities;
using System.Security.Cryptography.X509Certificates;
using DeployCmdlets4WA.ServiceProxy;
using System.Timers;
using System.Threading;
using System.IO;
using DeployCmdlets4WA.Cmdlet.ServiceConfigurationSchema;
using System.ServiceModel;
using System.Globalization;

namespace DeployCmdlets4WA.Cmdlet
{
    [Cmdlet(VerbsCommon.Set, "AzureStorageAccountEx")]
    public class SetAzureStorageAccountEx : PSCmdlet
    {
        private string _storageAccount;

        private PublishDataPublishProfile _publishProfile;
        private X509Certificate2 _cert;
        private string _subscriptionId;

        private string _createRequestToken;
        private string _createStorageAccStatus;

        [Parameter(Mandatory = true, HelpMessage = "Path to the publish settings file.")]
        [ValidateNotNullOrEmpty]
        public string PublishSettingsFile { get; set; }

        [Parameter(Mandatory = true, HelpMessage = "Name of storage account.")]
        [ValidateNotNullOrEmpty]
        public string StorageAccount
        {
            get
            {
                return _storageAccount;
            }
            set
            {
                _storageAccount = value.ToLowerInvariant(); //Storage account name can only have lowercase letters.
            }
        }

        [Parameter(Mandatory = false, HelpMessage = "Affinity group for storage account")]
        [ValidateNotNullOrEmpty]
        public string AffinityGroup { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Windows Azure Storage Account Location")]
        [ValidateNotNullOrEmpty]
        public string Location { get; set; }

        [Parameter(Mandatory = true, HelpMessage = "The name of subscription to be used for publishing to the cloud.")]
        [ValidateNotNullOrEmpty]
        public string Subscription { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Set this flag to just create storage account.")]
        public SwitchParameter CreateOnly { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Set this flag to set the storage account as default storage account.")]
        public SwitchParameter SetDefault { get; set; }

        protected override void ProcessRecord()
        {
            PreValidate(this.PublishSettingsFile);

            base.ProcessRecord();

            Utils.InitPublishSettings(PublishSettingsFile, Subscription, out _publishProfile, out _cert, out _subscriptionId);
            
            SetStorageAccount();
        }

        private void PreValidate(string publishSettingsFile)
        {
            if (File.Exists(publishSettingsFile) == false)
            {
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, Resources.FileDoesNotExist, this.PublishSettingsFile), "publishSettingsFile");
            }

            //Validate Storage account name.
            Regex validatePattern = new Regex("^([a-zA-z0-9]){3,24}$");
            if (validatePattern.IsMatch(this.StorageAccount) == false)
            {
                throw new ValidationMetadataException(Resources.InvalidStorageAccountNameMessage);
            }
        }

        private void SetStorageAccount()
        {
            if (!StorageAccountExists())
            {
                CreateStorageAccount();
                WriteObject(string.Format(CultureInfo.InvariantCulture, Resources.StorageAccCreatedMessage, this.StorageAccount));
            }
            else
            {
                WriteObject(string.Format(CultureInfo.InvariantCulture, Resources.StorageAccAlreadyExistsMessage, this.StorageAccount));
            }

            if (SetDefault.IsPresent == true)
            {
                SetAccountAsDefault();
            }

            if (CreateOnly.IsPresent == true)
            {
                return;
            }

            ServiceConfigurationSchema.ServiceConfiguration config = SerializationUtils.DeserializeXmlFile<ServiceConfigurationSchema.ServiceConfiguration>(CloudCSCFGFile);
            string primaryKey = GetStorageAccKey();
            ConfigureRoleStorageAccountKeys(config, primaryKey);
            WriteObject(Resources.StorageAccConfigSuccess);
        }

        private void SetAccountAsDefault()
        {
            ExecutePSCmdlet executeSetAzureSubscriptionCmd = new ExecutePSCmdlet();
            string enableWinRmCmd = "Set-AzureSubscription -CurrentStorageAccount \"{0}\" -SubscriptionName \"{1}\" ";
            executeSetAzureSubscriptionCmd.Execute(string.Format(CultureInfo.InvariantCulture, Resources.SetDefaultStorageAcc, StorageAccount, Subscription), string.Format(CultureInfo.InvariantCulture, enableWinRmCmd, StorageAccount, Subscription));
            if (executeSetAzureSubscriptionCmd.ErrorOccurred == true)
            {
                throw new ApplicationFailedException(Resources.ErrorSettingDefaultStorageAcc);
            }
        }

        private void ConfigureRoleStorageAccountKeys(ServiceConfigurationSchema.ServiceConfiguration config, string primaryKey)
        {
            string cloudStorageFormat = "DefaultEndpointsProtocol={0};AccountName={1};AccountKey={2}";
            string storageHttpKey = string.Format(CultureInfo.InvariantCulture, cloudStorageFormat, "http", this.StorageAccount, primaryKey);
            string storageHttpsKey = string.Format(CultureInfo.InvariantCulture, cloudStorageFormat, "https", this.StorageAccount, primaryKey);

            for (int i = 0; i < config.Role.Length; i++)
            {
                ServiceConfigurationSchema.ConfigurationSetting newSetting;
                newSetting = new ServiceConfigurationSchema.ConfigurationSetting() { name = Resources.DataConnectionString, value = storageHttpKey };
                UpdateSetting(ref config.Role[i], newSetting);

                newSetting = new ServiceConfigurationSchema.ConfigurationSetting() { name = Resources.DiagnosticsConnectionString, value = storageHttpsKey };
                UpdateSetting(ref config.Role[i], newSetting);
            }
            SerializationUtils.SerializeXmlFile<ServiceConfigurationSchema.ServiceConfiguration>(config, CloudCSCFGFile);
        }

        private static void UpdateSetting(ref RoleSettings rs, ServiceConfigurationSchema.ConfigurationSetting cs)
        {
            if (rs.ConfigurationSettings == null)
            {
                return;
            }
            for (int i = 0; i < rs.ConfigurationSettings.Length; i++)
            {
                ServiceConfigurationSchema.ConfigurationSetting setting = rs.ConfigurationSettings[i];
                if (setting.name == cs.name)
                {
                    setting.value = cs.value;
                    break;
                }
            }
        }

        private string GetStorageAccKey()
        {
            ClientOutputMessageInspector messageInspector;
            IServiceManagement serviceProxy = ServiceInitializer.Get(this._cert, out messageInspector);
            StorageService response = serviceProxy.GetStorageAccountKeys(this._subscriptionId, this.StorageAccount);
            return response.StorageServiceKeys.Primary;
        }

        private bool StorageAccountExists()
        {
            ClientOutputMessageInspector messageInspector;
            IServiceManagement serviceProxy = ServiceInitializer.Get(this._cert, out messageInspector);
            AvailabilityResponse response = serviceProxy.CheckStorageAccountNameAvailability(this._subscriptionId, this.StorageAccount);

            if (response.Result == false) //If storage account already exists..verify if it is located in same location supplied in input parameter.
            {
                StorageService storageAcctProperties;
                try
                {
                    storageAcctProperties = serviceProxy.GetStorageAccountProperties(this._subscriptionId, this.StorageAccount);
                }
                catch (EndpointNotFoundException)
                {
                    throw new ApplicationFailedException(string.Format(CultureInfo.InvariantCulture, Resources.StorageAccExistsForDiffSubscription, this.StorageAccount));
                }

                if (string.IsNullOrEmpty(Location) == false &&
                    Location.Trim().ToUpperInvariant() != storageAcctProperties.StorageServiceProperties.Location.ToUpperInvariant())
                {
                    WriteWarning(Resources.StorageAccDiffLocationWarning);
                }
            }
            return !response.Result;
        }

        private void CreateStorageAccount()
        {
            WriteObject("Creating storage account: " + _storageAccount);
            WriteObject("This may take a few minutes.");

            CreateStorageServiceInput input = new CreateStorageServiceInput();
            input.ServiceName = this.StorageAccount;
            input.Label = Convert.ToBase64String(UTF8Encoding.UTF8.GetBytes(this.StorageAccount));
            if (string.IsNullOrEmpty(this.Location) == false)
            {
                input.Location = this.Location;
            }
            else if (string.IsNullOrEmpty(this.AffinityGroup) == false)
            {
                input.AffinityGroup = this.AffinityGroup;
            }
            else  // Randomly use "North Central US" or "South Central US"
            {
                int randomLocation = new Random().Next(0, 1);
                input.Location = randomLocation == 0 ? "North Central US" : "South Central US";
            }

            try
            {
                _createRequestToken = ServiceManagementHelpers.CreateStorageAcc(this._subscriptionId, input, this._cert);
            }
            catch (Exception)
            {
                if (IsStorageAccountLimitExceeded() == true)
                {
                    throw new ApplicationFailedException(Resources.StorageAccLimitReached);
                }
                else
                {
                    throw;
                }
            }
            WaitTillCreationComplete();
        }

        private void WaitTillCreationComplete()
        {
            bool isDone = false;
            while (isDone == false)
            {
                System.Threading.Thread.Sleep(5000);

                ClientOutputMessageInspector messageInspector;
                IServiceManagement serviceProxy = ServiceInitializer.Get(this._cert, out messageInspector);
                Operation status = serviceProxy.GetOperationStatus(this._subscriptionId, this._createRequestToken);

                _createStorageAccStatus = status.Status;
                WriteObject("Storage account creation status: " + _createStorageAccStatus);

                if (status.Status == OperationStatus.Succeeded)
                {
                    break;
                }
                if (status.Status != OperationStatus.InProgress)
                {
                    if (IsStorageAccountLimitExceeded() == true)
                    {
                        throw new ApplicationFailedException(Resources.StorageAccLimitReached);
                    }
                    else
                    {
                        throw new ApplicationFailedException(string.Format(CultureInfo.InvariantCulture, Resources.StorageAccCreationFailed, _createStorageAccStatus));
                    }
                }
            }
        }

        private bool IsStorageAccountLimitExceeded()
        {
            WriteObject(Resources.VerifyStorageAccCount);

            ClientOutputMessageInspector messageInspector;
            IServiceManagement serviceProxy = ServiceInitializer.Get(this._cert, out messageInspector);
            ServiceProxy.Subscription subscriptionDetails = serviceProxy.GetSubscription(this._subscriptionId);

            if (subscriptionDetails.MaxStorageAccounts == subscriptionDetails.CurrentStorageAccounts)
            {
                return true;
            }
            return false;
        }

        private string CurrentLocation
        {
            get { return this.SessionState.Path.CurrentLocation.Path; }
        }

        private string CloudCSCFGFile
        {
            get { return Path.Combine(CurrentLocation, Resources.CSCFGCloudFile); }
        }
    }
}
