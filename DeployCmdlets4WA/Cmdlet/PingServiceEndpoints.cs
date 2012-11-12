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
using DeployCmdlets4WA.Properties;
using System.Net;
using Microsoft.WindowsAzure.Management.XmlSchema;
using DeployCmdlets4WA.ServiceProxy;
using System.Security.Cryptography.X509Certificates;
using DeployCmdlets4WA.Utilities;
using System.Net.Sockets;
using System.Globalization;

namespace DeployCmdlets4WA.Cmdlet
{
    [Cmdlet(VerbsDiagnostic.Ping, "ServiceEndpoints")]
    public class PingServiceEndpoints : PSCmdlet
    {
        private PublishDataPublishProfile _publishProfile;
        private X509Certificate2 _cert;
        private string _subscriptionId;

        [Parameter(Mandatory = true, HelpMessage = "Path to the publish settings file.")]
        [ValidateNotNullOrEmpty]
        public string PublishSettingsFile { get; set; }

        [Parameter(Mandatory = true, HelpMessage = "The name to be used for the service published to the cloud.")]
        [ValidateNotNullOrEmpty]
        public string ServiceName { get; set; }

        [Parameter(Mandatory = true, HelpMessage = "The name of subscription to be used for publishing to the cloud.")]
        [ValidateNotNullOrEmpty]
        public string Subscription { get; set; }

        protected override void ProcessRecord()
        {
            PreValidate(this.PublishSettingsFile);

            base.ProcessRecord();

            Utils.InitPublishSettings(PublishSettingsFile, Subscription, out _publishProfile, out _cert, out _subscriptionId);

            List<IPEndPoint> serviceEndpoints = GetServiceEndpoints();
            PingEndPoints(serviceEndpoints);
        }

        private static void PreValidate(string publishSettingsFile)
        {
            if (File.Exists(publishSettingsFile) == false)
            {
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, Resources.FileDoesNotExist, publishSettingsFile), "publishSettingsFile");
            }
        }

        private List<IPEndPoint> GetServiceEndpoints()
        {
            ClientOutputMessageInspector messageInspector;
            IServiceManagement serviceProxy = ServiceInitializer.Get(this._cert, out messageInspector);

            WriteObject(String.Format(CultureInfo.InvariantCulture, Resources.FetchingEndpoints, ServiceName));
            HostedService hostedService = serviceProxy.GetHostedServiceProperties(this._subscriptionId, this.ServiceName);

            List<IPEndPoint> endPointsToTest = new List<IPEndPoint>();
            if (hostedService.Deployments != null && hostedService.Deployments.Length != 0)
            {
                foreach (Deployment eachDeployment in hostedService.Deployments)
                {
                    if (eachDeployment.RoleList != null && eachDeployment.RoleList.Length != 0)
                    {
                        foreach (Role eachRole in eachDeployment.RoleList)
                        {
                            if (eachRole.ConfigurationSets != null && eachRole.ConfigurationSets.Length != 0)
                            {
                                foreach (ConfigurationSet eachConfigSet in eachRole.ConfigurationSets)
                                {
                                    NetworkConfigurationSet networkConfigset = eachConfigSet as NetworkConfigurationSet;
                                    if (networkConfigset != null && networkConfigset.InputEndpoints != null && networkConfigset.InputEndpoints.Length != 0)
                                    {
                                        endPointsToTest.AddRange(networkConfigset.InputEndpoints.Select(eachEndpoint => new IPEndPoint(IPAddress.Parse(eachEndpoint.Vip), eachEndpoint.Port)));
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return endPointsToTest;
        }

        private void PingEndPoints(List<IPEndPoint> serviceEndpoints)
        {
            int maxRetryCount = 36; //Try after every 5 secs for 3 minutes.
            int retryCountForEndpoint = 0;
            foreach (IPEndPoint eachEndpoint in serviceEndpoints)
            {
                retryCountForEndpoint = 0;
                while (retryCountForEndpoint <= maxRetryCount)
                {
                    WriteObject(string.Format(CultureInfo.InvariantCulture, Resources.VerifyingEndpoint, eachEndpoint.Address.ToString(), eachEndpoint.Port.ToString(CultureInfo.InvariantCulture)));
                    try
                    {
                        using (Socket s = new Socket(eachEndpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp))
                        {
                            s.Connect(eachEndpoint);
                            s.Disconnect(false);
                        }
                        WriteObject(Resources.VerificationSuccess);
                        break;
                    }
                    catch (Exception ex)
                    {
                        if (retryCountForEndpoint == maxRetryCount)
                        {
                            WriteObject(Resources.VerificationFailedRetry);
                            System.Threading.Thread.Sleep(5000); //Wait for 5 Seconds before trying again.
                        }
                        else
                        {
                            WriteObject(string.Format(CultureInfo.InvariantCulture, Resources.VerificationFailedRetry, ex.Message));
                            throw;
                        }
                    }
                    finally
                    {
                        retryCountForEndpoint++;
                    }
                }
            }
        }

    }
}
