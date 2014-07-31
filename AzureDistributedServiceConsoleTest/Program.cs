using System;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using AzureDistributedServiceTests;
using Microsoft.WindowsAzure;

namespace AzureDistributedServiceConsoleTest
{
    static class Program
    {
        static void Main()
        {
#if DEBUG
            var listeners = new TraceListener[] { new TextWriterTraceListener(Console.Out) };
            Debug.Listeners.AddRange(listeners);
#endif

            SetupConnections();

            const int tps = 5;
            const int totalTransactions = 50;
            string requestQueueName = CloudConfigurationManager.GetSetting("ServiceRequestQueue");
            string instanceId = Environment.MachineName;
            string responseQueueName = "response-queue-" + instanceId.ToLowerInvariant().Replace('_', '-');
            string storageConnectionString = CloudConfigurationManager.GetSetting("StorageConnectionString");
            Task queueSubmitterTask = TestServiceQueueSubmitter.StartRequestSubmitterAsync(storageConnectionString, requestQueueName, responseQueueName, tps, totalTransactions);
            string serviceUri = CloudConfigurationManager.GetSetting("ServiceUri");
            var uri = new Uri(serviceUri);
            Task webApiSubmitterTask = TestWebApiSubmitter.StartRequestSubmitterAsync(uri, storageConnectionString, instanceId + "WebApi", tps, totalTransactions);

            Task.WhenAny(webApiSubmitterTask, queueSubmitterTask).Wait();
        }

        private static void SetupConnections()
        {
            ServicePointManager.DefaultConnectionLimit = 64;
            // Turning Nagle off improves latency and throughput for Queue and Table.
            // Since we do not know the queue and table URI we are setting it off for all ServicePoints.
            // For blobs it will not matter since we always will deal with full segments.
            ServicePointManager.UseNagleAlgorithm = false;
            ServicePointManager.Expect100Continue = false;
        }
    }
}
