using System;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using AzureDistributedServiceTests;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.ServiceRuntime;

namespace TestRequestSubmitter
{
    public class WorkerRole : RoleEntryPoint
    {
        public override void Run()
        {
            // This is a sample worker implementation. Replace with your logic.
            Trace.TraceInformation("TestRequestSubmitter entry point called", "Information");

            var tps = int.Parse(CloudConfigurationManager.GetSetting("TPS"));
            var totalTransactions = int.Parse(CloudConfigurationManager.GetSetting("TotalTransactions"));
            string instanceId = RoleEnvironment.CurrentRoleInstance.Id;
            string storageConnectionString = CloudConfigurationManager.GetSetting("StorageConnectionString");
            var serviceUri = new Uri(CloudConfigurationManager.GetSetting("ServiceUri"));

            Task webApiSubmitterTask = TestWebApiSubmitter.StartRequestSubmitterAsync(serviceUri, storageConnectionString, instanceId + "-WebApi", tps,
                totalTransactions);

            string requestQueueName = CloudConfigurationManager.GetSetting("ServiceRequestQueue");
            string responseQueueName = "response-queue-" + instanceId.ToLowerInvariant().Replace('_', '-');

            Task queueSubmitterTask = TestServiceQueueSubmitter.StartRequestSubmitterAsync(storageConnectionString, requestQueueName, responseQueueName, tps, totalTransactions);

            try
            {
                Task.WhenAny(queueSubmitterTask, webApiSubmitterTask).Wait();
            }
            catch (Exception exception)
            {
                TestRecorder.LogException(storageConnectionString, instanceId, exception);
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
            return base.OnStart();
        }
    }
}
