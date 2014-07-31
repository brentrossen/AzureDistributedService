using System;
using System.Diagnostics;
using System.Threading.Tasks;
using AzureDistributedService;

namespace AzureDistributedServiceTests
{
    public class TestWorker
    {
        private readonly ServiceWorker<TestRequest, TestResponse> serviceWorker;
        public TestWorker(string storageConnectionString, string requestQueueName, TimeSpan requestTimeout, int messagesPerRequest) 
        {
            serviceWorker = new ServiceWorker<TestRequest, TestResponse>(storageConnectionString, 
                requestQueueName,
                ProcessRequest)
            {
                MaxProcessingTimeout = requestTimeout,
                MessagesPerRequest = messagesPerRequest,
                DelayWhenNothingInQueue = TimeSpan.FromMilliseconds(100),
                DequeueCountPoisonMessageLimit = 5
            };
        }

        protected static Task<TestResponse> ProcessRequest(TestRequest request)
        {
            var testResponse = new TestResponse
            {
                RequestNumber = request.RequestNumber,
                ProcessingCompletionTime = DateTimeOffset.UtcNow,
                StartTime = request.StartTime
            };

            Debug.WriteLine("Worker processed request: {0}", request.RequestNumber);

            return Task.FromResult(testResponse);
        }

        public async Task ProcessRequestsAsync()
        {
            await serviceWorker.ProcessRequestsAsync();
        }
    }
}