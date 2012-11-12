using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Management.Automation;
using DeployCmdlets4WA.ServiceProxy;
using System.Collections.ObjectModel;
using System.Xml.Linq;
using System.Security.Cryptography.X509Certificates;
using Microsoft.WindowsAzure.Management.XmlSchema;
using DeployCmdlets4WA.Utilities;
using System.IO;
using DeployCmdlets4WA.Properties;
using System.Globalization;

namespace DeployCmdlets4WA.Cmdlet
{
    [Cmdlet(VerbsCommon.New, "WinRMEnabledAzureVMImage")]
    public class NewWinRMEnabledAzureVMImage : PSCmdlet
    {
        private string _storageAccount;

        [Parameter(Mandatory = true)]
        public string Location { get; set; }

        //TODO: This need not be a param.
        [Parameter(Mandatory = true)]
        public string LocationXml { get; set; }

        [Parameter(Mandatory = true)]
        public string ImageName { get; set; }

        [Parameter(Mandatory = true, HelpMessage = "The name of subscription to be used for publishing to the cloud.")]
        [ValidateNotNullOrEmpty]
        public string Subscription { get; set; }

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
                _storageAccount = value.ToLowerInvariant();
            }
        }

        [Parameter(Mandatory = true, HelpMessage = "Path to the publish settings file.")]
        [ValidateNotNullOrEmpty]
        public string PublishSettingsFile { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Container within storage account for VHD file.")]
        public string Container { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();

            //Suppress progress messages..
            PSVariable progressPreference = base.SessionState.PSVariable.Get("ProgressPreference");
            base.SessionState.PSVariable.Set("ProgressPreference", "SilentlyContinue");

            Container = string.IsNullOrEmpty(Container) == true ? "vhd" : Container;

            Prevalidate(this.LocationXml, this.PublishSettingsFile);

            //Get Source Url using image location xml and location.
            string source = GetSourceVHD();

            //Check if image already exists.
            if (DoesImageExist() == true)
            {
                return;
            }

            //Get storage account keys.
            string primaryKey = GetStorageAccKey();

            //Create container for VHD if it does not exists.
            CreateContainer(primaryKey);

            //Copy VHD across blob.
            string vhdUrlInStorageAcc = CopyVHDBlob(source, primaryKey);

            //Create VM Image.
            CreateVMImage(vhdUrlInStorageAcc);

            //Restore the variable value back.
            base.SessionState.PSVariable.Set("ProgressPreference", progressPreference.Value);
        }

        private void Prevalidate(string locationXml, string publishSettingsFile)
        {
            if (File.Exists(locationXml) == false)
            {
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, Resources.LocationXmlNotFound, LocationXml), "locationXml");
            }
            if (File.Exists(publishSettingsFile) == false)
            {
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, Resources.FileDoesNotExist, publishSettingsFile), "publishSettingsFile");
            }
        }

        private void CreateVMImage(string vhdUrlInStorageAcc)
        {
            ExecutePSCmdlet executeCreateAzureImgCmd = new ExecutePSCmdlet();
           
            string createVMImageCmd = "Add-AzureVMImage -ImageName \"{0}\" -MediaLocation \"{1}\" -Label \"{0}\" -OS windows";
            executeCreateAzureImgCmd.Execute(string.Format(CultureInfo.InvariantCulture, Resources.CreatingVMImage, ImageName), string.Format(CultureInfo.InvariantCulture, createVMImageCmd, ImageName, vhdUrlInStorageAcc));
            if (executeCreateAzureImgCmd.ErrorOccurred == true)
            {
                throw new ApplicationFailedException(Resources.VMImageCreationFailed);
            }
        }

        private string GetSourceVHD()
        {
            XDocument imageLocationDoc = XDocument.Load(LocationXml);
            XElement urlelement = (from eachImageLocationNode in imageLocationDoc.Descendants("imagelocation")
                                   let locationNode = eachImageLocationNode.Descendants("location").FirstOrDefault()
                                   where locationNode.Value == Location
                                   select eachImageLocationNode).FirstOrDefault();
            if (urlelement == null)
            {
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, Resources.ImgUrlNotFoundForLocation, Location));
            }
            return urlelement.Descendants("imageurl").FirstOrDefault().Value;
        }

        private bool DoesImageExist()
        {
            ExecutePSCmdlet executeGetAzureImgCmd = new ExecutePSCmdlet();
            executeGetAzureImgCmd.Execute(string.Format(CultureInfo.InvariantCulture, Resources.VerifyingVMImageExists, ImageName), string.Format(CultureInfo.InvariantCulture, "Get-AzureVMImage -ImageName \"{0}\" ", ImageName));
            if (executeGetAzureImgCmd.OutputData == null || executeGetAzureImgCmd.OutputData.Count() == 0)
            {
                return false;
            }

            //Print Image Properties.
            WriteObject(Resources.VMImagePropHeader);
            foreach (PSPropertyInfo eachProperty in executeGetAzureImgCmd.OutputData.ElementAt(0).Properties)
            {
                WriteObject(string.Format(CultureInfo.InvariantCulture, "{0} : {1}", eachProperty.Name, eachProperty.Value));
            }
            return true;
        }

        private string GetStorageAccKey()
        {
            WriteObject(string.Format(CultureInfo.InvariantCulture, Resources.FetchingStorageAccKeys, StorageAccount));
            PublishDataPublishProfile publishProfile;
            X509Certificate2 cert;
            string subscriptionId;

            Utils.InitPublishSettings(PublishSettingsFile, Subscription, out publishProfile, out cert, out subscriptionId);

            ClientOutputMessageInspector messageInspector;
            IServiceManagement serviceProxy = ServiceInitializer.Get(cert, out messageInspector);
            StorageService response = serviceProxy.GetStorageAccountKeys(subscriptionId, this.StorageAccount);
            return response.StorageServiceKeys.Primary;
        }

        private string CopyVHDBlob(string source, string primaryKey)
        {
            string destination = string.Format(CultureInfo.InvariantCulture, "http://{0}.blob.core.windows.net/{1}/{2}.vhd", StorageAccount, Container, ImageName);
            WriteObject(string.Format(CultureInfo.InvariantCulture, Resources.CopyingVHD, source, destination));

            CopyBlob.Copy(source, destination, StorageAccount, primaryKey);

            return destination;
        }

        private void CreateContainer(string primaryKey)
        {
            WriteObject(string.Format(CultureInfo.InvariantCulture, Resources.CreatingContainerIfNotExists, Container));
            CreateContainerIfNotExists.CreateIfNotExists(Container, StorageAccount, primaryKey);
        }
    }
}
