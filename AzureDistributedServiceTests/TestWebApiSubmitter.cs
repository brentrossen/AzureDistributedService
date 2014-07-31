using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;

namespace AzureDistributedServiceTests
{
    /// <summary>
    /// Communicates to the worker roles through the web api
    /// See http://www.asp.net/web-api/overview/web-api-clients/calling-a-web-api-from-a-net-client for details
    /// </summary>
    public class TestWebApiSubmitter
    {
        private readonly Uri serviceUri;

        private TestWebApiSubmitter(Uri serviceUri)
        {
            this.serviceUri = serviceUri;
        }

        public static async Task StartRequestSubmitterAsync(
            Uri serviceUri,
            string storageConnectionString,
            string instanceId,
            int tps,
            int totalRequests)
        {
            var testClient = new TestWebApiSubmitter(serviceUri);
            while (true)
            {
                try
                {
                    var requestSubmissionTask = testClient.SubmitRequestsAsync(totalRequests, tps);
                    var stopwatch = Stopwatch.StartNew();
                    TimeSpan avgReqTime = await requestSubmissionTask;
                    TestRecorder.RecordAvgLatency(storageConnectionString, instanceId, avgReqTime, totalRequests,
                        tps, stopwatch.Elapsed);
                }
                catch (Exception ex)
                {
                    TestRecorder.LogException(storageConnectionString, instanceId, ex);
                }
            }
        }

        public async Task<TimeSpan> SubmitRequestsAsync(int numRequests, int tps)
        {
            var delayBetweenRequests = TimeSpan.FromMilliseconds(1000.0 / tps);

            var requestTimes = new ConcurrentQueue<TimeSpan>();
            var responseTasks = new List<Task>();
            using (var client = new HttpClient())
            {
                client.BaseAddress = serviceUri;
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                for (int i = 0; i < numRequests; i++)
                {
                    var stopwatch = Stopwatch.StartNew();
                    var request = new TestRequest
                    {
                        RequestNumber = i,
                        StartTime = DateTimeOffset.UtcNow
                    };
                    Debug.WriteLine("Submitting request {0}", i);

                    // New code:
                    Task<HttpResponseMessage> responseTask = client.PostAsJsonAsync("api/service/", request);

                    Task continueWith = responseTask.ContinueWith(
                        async task =>
                        {
                            var response = task.Result;
                            if (response.IsSuccessStatusCode)
                            {
                                var testResponse = await response.Content.ReadAsAsync<TestResponse>();
                                var now = DateTimeOffset.UtcNow;
                                var processingTime = now - testResponse.StartTime;
                                Debug.WriteLine("Request {0} took {1}", testResponse.RequestNumber, processingTime);
                                requestTimes.Enqueue(processingTime);
                            }
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
            }

            if (!requestTimes.Any())
            {
                throw new Exception("Failed to retrieve any results. Are you sure your queue names are configured correctly?");
            }

            double averageMs = requestTimes.Select(t => t.TotalMilliseconds).Average();

            return TimeSpan.FromMilliseconds(averageMs);                
        }
    }
}