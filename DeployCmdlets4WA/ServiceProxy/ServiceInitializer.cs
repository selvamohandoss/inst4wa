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
using System.Security.Cryptography.X509Certificates;
using System.ServiceModel.Web;

namespace DeployCmdlets4WA.ServiceProxy
{
    public static class ServiceInitializer
    {
        public static IServiceManagement Get(X509Certificate2 authCert, out ClientOutputMessageInspector messageInspector)
        {
            messageInspector = new ClientOutputMessageInspector();
            WebChannelFactory<IServiceManagement> factory = new WebChannelFactory<IServiceManagement>
                (
                    ConfigurationConstants.WebHttpBinding(0),
                    new Uri(ConfigurationConstants.ServiceManagementEndpoint)
                );
            factory.Endpoint.Behaviors.Add(messageInspector);
            factory.Credentials.ClientCertificate.Certificate = authCert;
            return factory.CreateChannel();
        }
    }
}
