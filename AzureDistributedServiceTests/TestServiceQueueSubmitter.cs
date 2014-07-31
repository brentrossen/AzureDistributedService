using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using AzureDistributedService;

namespace AzureDistributedServiceTests
{
    /// <summary>
    /// Communicates directly to worker roles via queues
    /// </summary>
    public class TestServiceQueueSubmitter
    {
        private readonly ServiceClient<TestRequest, TestResponse> serviceClient;
        private readonly TimeSpan requestTimeout = TimeSpan.FromSeconds(15);

        private TestServiceQueueSubmitter(ServiceClient<TestRequest, TestResponse> serviceClient)
        {
            this.serviceClient = serviceClient;
        }

        public static async Task StartRequestSubmitterAsync(string storageConnectionString, 
            string requestQueueName,
            string responseQueueName,
            int tps,
            int totalRequests)
        {
            var serviceClient = new ServiceClient<TestRequest, TestResponse>(
                storageConnectionString,
                new ServiceClientQueueNames
                {
                    RequestQueueName = requestQueueName,
                    ResponseQueueName = responseQueueName,
                },
                responseCheckFrequency: TimeSpan.FromSeconds(0.01));

            await serviceClient.InitializeQueuesAsync();

            var testServiceQueueSubmitter = new TestServiceQueueSubmitter(serviceClient);
            while (true)
            {
                try
                {
                    var requestSubmissionTask = testServiceQueueSubmitter.SubmitRequestsAsync(totalRequests, tps);
                    var stopwatch = Stopwatch.StartNew();
                    TimeSpan avgReqTime = await requestSubmissionTask;
                    TestRecorder.RecordAvgLatency(storageConnectionString, responseQueueName, avgReqTime, totalRequests,
                        tps, stopwatch.Elapsed);
                }
                catch (Exception ex)
                {
                    TestRecorder.LogException(storageConnectionString, responseQueueName, ex);
                }
            }
        }

        public async Task<TimeSpan> SubmitRequestsAsync(int numRequests, int tps)
        {
            var delayBetweenRequests = TimeSpan.FromMilliseconds(1000.0/tps);

            var requestTimes = new ConcurrentQueue<TimeSpan>();
            var responseTasks = new List<Task>();
            for (int i = 0; i < numRequests; i++)
            {
                var stopwatch = Stopwatch.StartNew();
                var request = new TestRequest
                {
                    RequestNumber = i,
                    StartTime = DateTimeOffset.UtcNow
                };
                Debug.WriteLine("Submitting request {0}", i);
                var responseTask = serviceClient.SubmitRequestAsync(request, requestTimeout);
                Task continueWith = responseTask.ContinueWith(task =>
                {
                    var testResponse = task.Result;
                    var now = DateTimeOffset.UtcNow;
                    var processingTime = now - testResponse.StartTime;
                    Debug.WriteLine("Request {0} took {1}", testResponse.RequestNumber, processingTime);
                    requestTimes.Enqueue(processingTime);
                });
                responseTasks.Add(continueWith);
                responseTasks.Add(responseTask);

                stopwatch.Stop();
                var delayTime = delayBetweenRequests - stopwatch.Elapsed;

                if (delayTime > TimeSpan.Zero)
                {
                    await Task.Delay(delayTime);
                }
            }

            await Task.WhenAll(responseTasks.ToArray());

            if (!requestTimes.Any())
            {
                throw new Exception("Failed to retrieve any results. Are you sure your queue names are configured correctly?");
            }
            
            double averageMs = requestTimes.Select(t => t.TotalMilliseconds).Average();

            return TimeSpan.FromMilliseconds(averageMs);
        }
    }
}