using System;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using AzureDistributedServiceTests;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.ServiceRuntime;

namespace TestRequestProcessor
{
    public class WorkerRole : RoleEntryPoint
    {
        public override void Run()
        {
            // This is a sample worker implementation. Replace with your logic.
            Trace.TraceInformation("TestRequestProcessor entry point called", "Information");

            string requestQueueName = CloudConfigurationManager.GetSetting("ServiceRequestQueue");
            string storageConnectionString = CloudConfigurationManager.GetSetting("StorageConnectionString");
            int messagesPerRequest = Int32.Parse(CloudConfigurationManager.GetSetting("MessagesPerRequest"));
            var requestTimeout = TimeSpan.FromSeconds(15); // if the request cannot be completed after this long, let someone else try
            var testWorker = new TestWorker(storageConnectionString, requestQueueName, requestTimeout, messagesPerRequest);
            try
            {
                Task processRequests = testWorker.ProcessRequestsAsync();
                processRequests.Wait(); // should never return
            }
            catch (Exception ex)
            {
                TestRecorder.LogException(storageConnectionString, RoleEnvironment.CurrentRoleInstance.Id, ex);
                throw;
            }
        }

        public override bool OnStart()
        {
            // Set the maximum number of concurrent connections 
            ServicePointManager.DefaultConnectionLimit = 64;

            // Turning Nagle off improves latency and throughput for Queue and Table.
            // Since we do not know the queue and table URI we are setting it off for all ServicePoints.
            // For blobs it will not matter since we always will deal with full segments.
            ServicePointManager.UseNagleAlgorithm = false;
            ServicePointManager.Expect100Continue = false;

            // For information on handling configuration changes
            // see the MSDN topic at http://go.microsoft.com/fwlink/?LinkId=166357.

            return base.OnStart();
        }
    }
}
