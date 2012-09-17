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
using Microsoft.WindowsAzure.Management.XmlSchema;
using System.Security.Cryptography.X509Certificates;
using DeployCmdlets4WA.Properties;

namespace DeployCmdlets4WA.Utilities
{
    public static class Utils
    {
        public static void InitPublishSettings(string publishSettingsFile, string subscriptionName, out PublishDataPublishProfile publishProfile, out X509Certificate2 cert, out string subscriptionId)
        {
            publishProfile = null;
            cert = null;
            subscriptionId = null;

            PublishData publishData = SerializationUtils.DeserializeXmlFile<PublishData>(publishSettingsFile);

            for (int iItem = 0; iItem < publishData.Items.Length; iItem++)
            {
                publishProfile = publishData.Items[iItem];

                for (int iSubscription = 0; iSubscription < publishProfile.Subscription.Length; iSubscription++)
                {
                    PublishDataPublishProfileSubscription subscription = publishProfile.Subscription[iSubscription];

                    if (subscription.Name == subscriptionName)
                    {
                        cert = new X509Certificate2(Convert.FromBase64String(publishProfile.ManagementCertificate), String.Empty);
                        subscriptionId = subscription.Id;
                        return;
                    }
                }
            }

            throw new ArgumentException(string.Format(Resources.SubscriptionNotFound, subscriptionName), "Subscription");
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
