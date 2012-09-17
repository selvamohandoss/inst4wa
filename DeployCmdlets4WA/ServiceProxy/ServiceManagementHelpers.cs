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

namespace DeployCmdlets4WA.ServiceProxy
{
    public class ServiceManagementHelpers
    {
        /// <summary>
        /// Method to submit the request to create new storage account and return request token.
        /// </summary>
        /// <param name="subscriptionId">Subscription id</param>
        /// <param name="cert">Auth certificate</param>
        /// <param name="input">Input required to create new storage acc</param>
        /// <returns>Token to track the progress of storage account creation</returns>
        public static string CreateStorageAcc(string subscriptionId, CreateStorageServiceInput input, X509Certificate2 cert)
        {
            ClientOutputMessageInspector messageInspector;
            IServiceManagement serviceManager = ServiceInitializer.Get(cert, out messageInspector);
            serviceManager.CreateStorageAccount(subscriptionId, input);
            return messageInspector.ResponseMessage.Headers["x-ms-request-id"];
        }
    }
}
