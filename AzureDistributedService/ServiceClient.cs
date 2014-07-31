using System;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;

namespace AzureDistributedService
{
    public class ServiceClient<TRequest, TResponse>
    {
        private readonly string responseQueueName;
        private readonly CloudQueue requestQueue;
        private readonly CloudQueue responseQueue;
        private readonly ResponseRetriever<TRequest, TResponse> responseRetriever;

        /// <summary>
        /// Constructs a service client for communicating with a distributed set of <see cref="ServiceWorker{TRequest,TResponse}"/> workers
        /// </summary>
        public ServiceClient(string storageConnectionString, ServiceClientQueueNames queueNames, TimeSpan responseCheckFrequency)
        {
            if (storageConnectionString == null) throw new ArgumentNullException("storageConnectionString");

            responseQueueName = queueNames.ResponseQueueName;
            var cloudStorageAccount = CloudStorageAccount.Parse(storageConnectionString);
            var cloudQueueClient = cloudStorageAccount.CreateCloudQueueClient();
            requestQueue = cloudQueueClient.GetQueueReference(queueNames.RequestQueueName);
            responseQueue = cloudQueueClient.GetQueueReference(responseQueueName);
            responseRetriever = new ResponseRetriever<TRequest, TResponse>(responseQueue, responseCheckFrequency);
        }

        /// <summary>
        /// Creates the queues for this service client. Must be called at least once before using the service client.
        /// </summary>
        /// <returns></returns>
        public async Task InitializeQueuesAsync()
        {
            await requestQueue.CreateIfNotExistsAsync();
            await responseQueue.CreateIfNotExistsAsync();
        }

        /// <summary>
        /// Submits a request for the workers to process
        /// </summary>
        public async Task<TResponse> SubmitRequestAsync(TRequest request, TimeSpan requestTimeout)
        {
            var requestContents = new RequestContents<TRequest>
            {
                RequestId = Guid.NewGuid(),
                ResponseQueueName = responseQueueName,
                Request = request
            };

            var jsonContents = JsonConvert.SerializeObject(requestContents);
            var cloudMessage = new CloudQueueMessage(jsonContents);
            await requestQueue.AddMessageAsync(cloudMessage);

            Task<TResponse> responseTask = responseRetriever.SubmitRequest(requestContents, requestTimeout);

            return await responseTask;
        }
    }
}
