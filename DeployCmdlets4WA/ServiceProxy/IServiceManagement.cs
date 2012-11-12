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
using System.ServiceModel;
using System.ServiceModel.Web;

namespace DeployCmdlets4WA.ServiceProxy
{
    public static class OperationStatus
    {
        public const string InProgress = "InProgress";
        public const string Failed = "Failed";
        public const string Succeeded = "Succeeded";
        public const string TimedOut = "TimedOut";
    }

    [ServiceContract(Namespace = ConfigurationConstants.ServiceManagementNamespace)]
    public partial interface IServiceManagement
    {
        [OperationContract]
        [WebInvoke(Method = "POST", UriTemplate = @"{subscriptionId}/services/storageservices")]
        void CreateStorageAccount(string subscriptionId, CreateStorageServiceInput createStorageServiceInput);

        [OperationContract]
        [WebGet(UriTemplate = @"{subscriptionId}/operations/{requestId}")]
        Operation GetOperationStatus(string subscriptionId, string requestId);

        [OperationContract]
        [WebGet(UriTemplate = @"{subscriptionId}/services/storageservices/{storageAccName}/keys")]
        StorageService GetStorageAccountKeys(string subscriptionId, string storageAccName);

        [OperationContract]
        [WebGet(UriTemplate = @"{subscriptionId}/services/storageservices/{storageAccName}")]
        StorageService GetStorageAccountProperties(string subscriptionId, string storageAccName);

        [OperationContract]
        [WebGet(UriTemplate = @"{subscriptionId}/services/storageservices/operations/isavailable/{storageAccName}")]
        AvailabilityResponse CheckStorageAccountNameAvailability(string subscriptionId, string storageAccName);

        [OperationContract]
        [WebGet(UriTemplate = @"{subscriptionId}/services/hostedservices/{serviceName}?embed-detail=true")]
        HostedService GetHostedServiceProperties(string subscriptionId, string serviceName);

        [OperationContract]
        [WebGet(UriTemplate = @"{subscriptionId}")]
        Subscription GetSubscription(string subscriptionId);
    }
}
