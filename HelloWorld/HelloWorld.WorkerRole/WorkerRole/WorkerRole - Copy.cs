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
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.StorageClient;
using System.Net.Sockets;
using System.IO;

namespace WorkerRole
{
    public class WorkerRole : RoleEntryPoint
    {
        private CloudTableClient tableClient;

        public override void Run()
        {
            Trace.WriteLine("WorkerRole entry point called", "Information");

            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(CloudConfigurationManager.GetSetting("StorageConnectionString"));
            tableClient = storageAccount.CreateCloudTableClient();
            tableClient.CreateTableIfNotExist(Constants.LogTable);

            HttpListener listener = new HttpListener();
            string listenerPrefix = string.Format("http://{0}:{1}/",
                RoleEnvironment.CurrentRoleInstance.InstanceEndpoints["HelloWorldEndpoint"].IPEndpoint.Address,
                RoleEnvironment.CurrentRoleInstance.InstanceEndpoints["HelloWorldEndpoint"].IPEndpoint.Port);
            Trace.WriteLine("Listening to -" + listenerPrefix);
            StoreInTable("Listening to -" + listenerPrefix);
            listener.Prefixes.Add(listenerPrefix);
            listener.Start();

            while (true)
            {
                // Create a listener.
                try
                {
                    StoreInTable("Listening...");
                    // Note: The GetContext method blocks while waiting for a request. 
                    HttpListenerContext context = listener.GetContext();
                    HttpListenerRequest request = context.Request;
                    // Obtain a response object.
                    HttpListenerResponse response = context.Response;
                    // Construct a response.
                    string responseString = "<HTML><BODY> Hello world!</BODY></HTML>";
                    byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
                    // Get a response stream and write the response to it.
                    response.ContentLength64 = buffer.Length;
                    System.IO.Stream output = response.OutputStream;
                    output.Write(buffer, 0, buffer.Length);
                    // You must close the output stream.
                    output.Close();
                }
                catch (Exception ex)
                {
                    Trace.WriteLine("Error - " + ex.Message);
                    StoreInTable("Error - " + ex.Message);
                }
            }
        }

        public override bool OnStart()
        {
            // Set the maximum number of concurrent connections 
            ServicePointManager.DefaultConnectionLimit = 12;

            // For information on handling configuration changes
            // see the MSDN topic at http://go.microsoft.com/fwlink/?LinkId=166357.

            return base.OnStart();
        }

        private void StoreInTable(string logmessage)
        {
            TableServiceContext context = tableClient.GetDataServiceContext();
            LogEntity newVisitor = new LogEntity(DateTime.Now.ToString("MM-dd-yyyy")) { logMessage = logmessage };
            context.AddObject(Constants.LogTable, newVisitor);
            context.SaveChanges();
        }
    }
}
