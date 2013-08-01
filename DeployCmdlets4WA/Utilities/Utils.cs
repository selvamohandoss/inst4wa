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
using System.Xml.Serialization;
using System.IO;
using V1 = Microsoft.WindowsAzure.Management.XmlSchema.V1;
using V2 = Microsoft.WindowsAzure.Management.XmlSchema.V2;
using System.Security.Cryptography.X509Certificates;
using DeployCmdlets4WA.Properties;
using System.Globalization;
using System.Xml.Linq;
using DeployCmdlets4WA.ServiceProxy;

namespace DeployCmdlets4WA.Utilities
{
    public static class Utils
    {
        private static string ServiceEnvironment;
        private static bool IsIaaS;
        public static string CloudAppURLFormat, VHDURLFormat, PublishSettingsURL, ContainerURLFormat, ServiceManagementEndpoint;

        public static void Init(string serviceEnvironment, string serviceModel)
        {
            // determine Azure service environment
            if (string.IsNullOrEmpty(serviceEnvironment))
                ServiceEnvironment = "AzureCloud";
            else
                ServiceEnvironment = serviceEnvironment;

            ServiceEnvironment = ServiceEnvironment.ToUpperInvariant();

            // determine service model
            IsIaaS = false;
            if (!string.IsNullOrEmpty(serviceModel))
                IsIaaS = (serviceModel.ToUpperInvariant() == "IAAS");
            
            // adjust various global params accordingly
            // TODO: Get these from Get-Environment?
            CloudAppURLFormat = (ServiceEnvironment == "AZURECHINACLOUD") ? "{0}.chinacloudapp.cn" : "{0}.cloudapp.net";

            VHDURLFormat = (ServiceEnvironment == "AZURECHINACLOUD") ? 
                "http://{0}.blob.core.chinacloudapi.cn/{1}/{2}.vhd" : "http://{0}.blob.core.windows.net/{1}/{2}.vhd";
            
            PublishSettingsURL = IsIaaS ?
                ((ServiceEnvironment == "AZURECHINACLOUD") ? Resources.AzureIaaSPublishSettingsURLCN : Resources.AzureIaaSPublishSettingsURL) :
                ((ServiceEnvironment == "AZURECHINACLOUD") ? Resources.AzurePaaSPublishSettingsURLCN : Resources.AzurePaaSPublishSettingsURL);

            ContainerURLFormat = (ServiceEnvironment == "AZURECHINACLOUD") ? 
                "http://{0}.blob.core.chinaclouapi.cn/{1}?restype=container" : "http://{0}.blob.core.windows.net/{1}?restype=container";

            ServiceManagementEndpoint = (ServiceEnvironment == "AZURECHINACLOUD") ?
                        "https://management.core.chinacloudapi.cn" : "https://management.core.windows.net";
        }

        public static void InitPublishSettings(string publishSettingsFile, string subscriptionName, out X509Certificate2 cert, out string subscriptionId, out string serviceMgmtUrl)
        {
            //Infer the schema version.
            XDocument settingsDoc = XDocument.Load(publishSettingsFile);
            XAttribute version = (from eachNode in settingsDoc.Descendants("PublishData").Single().Descendants("PublishProfile")
                                  select eachNode.Attribute("SchemaVersion")).FirstOrDefault();

            //For 1.0 version attribute was not present..so null indicates that version is below 2.0
            if (version == null)
            {
                ParseV1File(publishSettingsFile, subscriptionName, out cert, out subscriptionId, out serviceMgmtUrl);
                return;
            }
            else if(version.Value == "2.0")
            {
                ParseV2File(publishSettingsFile, subscriptionName, out cert, out subscriptionId, out serviceMgmtUrl);
                return;
            }

            throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, Resources.SubscriptionNotFound, subscriptionName), "subscriptionName");
        }

        /// <summary>
        /// Method that can be used to make sure that management service url present in publishsetting file for selected subscription is same as that of the Environment specified by the user.
        /// </summary>
        /// <param name="serviceMgmtUrl1">Url1</param>
        /// <param name="serviceMgmtUrl2">Url2. If NULL then ServiceManagementEndpoint is used for comparison</param>
        public static void EnsureSameEnvironment(string serviceMgmtUrl1, string serviceMgmtUrl2 = null)
        {
            Uri urlFromPubSetting = new Uri(serviceMgmtUrl1);
            Uri urlFromUtils = new Uri(String.IsNullOrEmpty(serviceMgmtUrl2) == true ? Utils.ServiceManagementEndpoint : serviceMgmtUrl2);
            if (urlFromPubSetting.Equals(urlFromUtils) == false)
            {
                throw new ArgumentException(Resources.MgmtUrlMismatch, "publishSettingsFile");
            }
        }

        private static void ParseV1File(string publishSettingsFile, string subscriptionName, out X509Certificate2 cert, out string subscriptionId, out string serviceMgmtUrl)
        {
            V1.PublishDataPublishProfile publishProfile = null;
            cert = null;
            subscriptionId = null;
            serviceMgmtUrl = null;

            V1.PublishData publishData = SerializationUtils.DeserializeXmlFile<V1.PublishData>(publishSettingsFile);

            for (int iItem = 0; iItem < publishData.Items.Length; iItem++)
            {
                publishProfile = publishData.Items[iItem];

                for (int iSubscription = 0; iSubscription < publishProfile.Subscription.Length; iSubscription++)
                {
                    V1.PublishDataPublishProfileSubscription subscription = publishProfile.Subscription[iSubscription];

                    if (subscription.Name == subscriptionName)
                    {
                        serviceMgmtUrl = publishProfile.Url;
                        cert = new X509Certificate2(Convert.FromBase64String(publishProfile.ManagementCertificate), String.Empty);
                        subscriptionId = subscription.Id;
                        return;
                    }
                }
            }
        }

        private static void ParseV2File(string publishSettingsFile, string subscriptionName, out X509Certificate2 cert, out string subscriptionId, out string serviceMgmtUrl)
        {
            V2.PublishDataPublishProfile publishProfile = null;
            cert = null;
            subscriptionId = null;
            serviceMgmtUrl = null;

            V2.PublishData publishData = SerializationUtils.DeserializeXmlFile<V2.PublishData>(publishSettingsFile);

            for (int iItem = 0; iItem < publishData.Items.Length; iItem++)
            {
                publishProfile = publishData.Items[iItem];

                for (int iSubscription = 0; iSubscription < publishProfile.Subscription.Length; iSubscription++)
                {
                    V2.PublishDataPublishProfileSubscription subscription = publishProfile.Subscription[iSubscription];

                    if (subscription.Name == subscriptionName)
                    {
                        serviceMgmtUrl = subscription.ServiceManagementUrl;
                        cert = new X509Certificate2(Convert.FromBase64String(subscription.ManagementCertificate), String.Empty);
                        subscriptionId = subscription.Id;
                        return;
                    }
                }
            }
        }
    }

    public static class SerializationUtils
    {
        public static T DeserializeXmlFile<T>(string fileName)
        {
            T item = default(T);

            XmlSerializer xmlSerializer = new XmlSerializer(typeof(T));
            using (Stream s = new FileStream(fileName, FileMode.Open))
            {
                item = (T)xmlSerializer.Deserialize(s);
            }
            return item;
        }

        public static void SerializeXmlFile<T>(T obj, string fileName)
        {
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(T));
            using (Stream stream = new FileStream(fileName, FileMode.Create))
            {
                xmlSerializer.Serialize(stream, obj);
            }
        }
    }
}
