using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;

namespace AzureDistributedService
{
    /// <summary>
    /// The class responsible for dequeuing requests and calling the process request function to 
    /// generate responses. Once a response is generated, it is placed into the queue for the 
    /// <see cref="ServiceClient{TRequest,TResponse}"/>
    /// </summary>
    /// <typeparam name="TRequest">The data structure containing the request</typeparam>
    /// <typeparam name="TResponse">The data structure containing the response</typeparam>
    public class ServiceWorker<TRequest, TResponse>
    {
        private const int DefaultDequeueCountLimit = 5;
        private const int DefaultMessagesPerRequest = 2;
        private readonly CloudQueue requestQueue;
        private readonly CloudQueueClient cloudQueueClient;

        private readonly TimeSpan defaultDelayWhenNothingInQueue
            = TimeSpan.FromMilliseconds(100);

        /// <summary>
        /// Performs the actual work of processing a <see cref="TRequest"/> 
        /// and creating a <see cref="TResponse"/>
        /// </summary>
        private readonly Func<TRequest, Task<TResponse>> processRequestFunc;

        private readonly TimeSpan defaultRequestTimeout = TimeSpan.FromSeconds(30);

        /// <summary>
        /// When no messages are retrieved from the queue, the worker will sleep
        /// for this long before trying again. Increase this number if you find that
        /// your workers are frequently spinning on an empty queue and your service
        /// can accept a longer delay for the first message in the queue.
        /// </summary>
        public TimeSpan DelayWhenNothingInQueue { get; set; }

        /// <summary>
        /// The maximum number of times a message can be retrieved before it is considered a poison message.
        /// If it has been retrieved more than this many times it either can't be processed
        /// or can't be processed within the time limit. 
        /// Poison messages will timeout on the <see cref="ServiceClient{TRequest,TResponse}"/>.
        /// </summary>
        public int DequeueCountPoisonMessageLimit { get; set; }

        /// <summary>
        /// The number of requests to retrieve from the queue to process in a batch.
        /// Higher numbers increase queue usage efficiency, lower numbers will distribute
        /// the work more evenly and lower the response time. If the load on the system is low,
        /// a lower number is recommended here. Increase if the average load is high and you
        /// want more efficient use of each worker.
        /// This number also determines the parallelism of each worker. Use lower numbers for 
        /// smaller VMs, and higher numbers if each worker has more available resources.
        /// </summary>
        public int MessagesPerRequest { get; set; }

        /// <summary>
        /// The maximum amount of tiem allotted to processing a request (or batch of requests) before timing out.
        /// After this timeout, the request will reappear in the queue and another worker will pick it up.
        /// This timeout must be higher than the time it takes to process a request, otherwise the request
        /// will be repeatedly dequeued, and will be considered a poison message and deleted after
        /// <see cref="DequeueCountPoisonMessageLimit"/>.
        /// </summary>
        public TimeSpan MaxProcessingTimeout { get; set; }

        /// <summary>
        /// Constructs a <see cref="ServiceWorker{TRequest,TResponse}"/> object.
        /// </summary>
        /// <param name="storageConnectionString">The storage connection string for the account containing the <paramref name="requestQueueName" /></param>
        /// <param name="requestQueueName">The name of the storage queue containing requests.</param>
        /// <param name="processRequestFunc">
        /// The function used to process requests and create responses. The return type is Task{TResponse} to allow using async-await within
        /// the processing function.
        /// </param>
        public ServiceWorker(string storageConnectionString, string requestQueueName, Func<TRequest, Task<TResponse>> processRequestFunc)
        {
            // Set the defaults
            if (storageConnectionString == null) throw new ArgumentNullException("storageConnectionString");
            if (requestQueueName == null) throw new ArgumentNullException("requestQueueName");
            if (processRequestFunc == null) throw new ArgumentNullException("processRequestFunc");

            DelayWhenNothingInQueue = defaultDelayWhenNothingInQueue;
            DequeueCountPoisonMessageLimit = DefaultDequeueCountLimit;
            MessagesPerRequest = DefaultMessagesPerRequest;
            MaxProcessingTimeout = defaultRequestTimeout;

            this.processRequestFunc = processRequestFunc;
            var cloudStorageAccount = CloudStorageAccount.Parse(storageConnectionString);
            cloudQueueClient = cloudStorageAccount.CreateCloudQueueClient();
            requestQueue = cloudQueueClient.GetQueueReference(requestQueueName);
        }

        /// <summary>
        /// Processes requests from the request queue.
        /// Retrieves requests continuously as long as there is something in the request queue.
        /// Delays if there is nothing in the request queue.
        /// Runs forever, does not return.
        /// </summary>
        /// <param name="cancellationToken">
        /// Pass a cancellation token to allow stopping request processing. Will break out of
        /// retrieving messages, deleting, or delaying, but will not immediately break out of message processing.
        /// </param>
        public async Task ProcessRequestsAsync(CancellationToken? cancellationToken = null)
        {
            var nonNullCancellationToken = GetNonNullCancellationToken(cancellationToken);

            while (!nonNullCancellationToken.IsCancellationRequested)
            {
                var requestCloudMessages = await requestQueue.GetMessagesAsync(MessagesPerRequest, MaxProcessingTimeout, null, null, nonNullCancellationToken);

                var cloudQueueMessages = requestCloudMessages.ToArray();

                if (cloudQueueMessages.Any())
                {
                    await ProcessRetrievedMessages(cloudQueueMessages, nonNullCancellationToken);
                }
                else
                {
                    await Task.Delay(DelayWhenNothingInQueue, nonNullCancellationToken);
                }
            }
        }

        private static CancellationToken GetNonNullCancellationToken(CancellationToken? cancellationToken)
        {
            return cancellationToken.HasValue
                ? cancellationToken.Value
                : new CancellationTokenSource().Token;            
        }

        private async Task ProcessRetrievedMessages(IEnumerable<CloudQueueMessage> cloudQueueMessages, CancellationToken cancellationToken)
        {
            IEnumerable<Task> tasks = cloudQueueMessages.Select(m => ProcessMessage(cancellationToken, m));

            // Await for all tasks to complete
            await Task.WhenAll(tasks);
        }

        private async Task ProcessMessage(CancellationToken cancellationToken, CloudQueueMessage cloudQueueMessage)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                // On cancellation the message will become visible after the timeout and another worker will process it
                return;
            }

            // Process the message if it has not failed DequeueCountPoisonMessageLimit times, which would indicate it is a poison message
            if (cloudQueueMessage.DequeueCount < DequeueCountPoisonMessageLimit)
            {
                var requestContents = JsonConvert.DeserializeObject<RequestContents<TRequest>>(cloudQueueMessage.AsString);

                var response = await processRequestFunc(requestContents.Request);

                await SubmitResponseAsync(requestContents, response, cancellationToken);
            }

            // Once the message has been processed, delete it
            await requestQueue.DeleteMessageAsync(cloudQueueMessage, cancellationToken);
        }

        private async Task SubmitResponseAsync(RequestContents<TRequest> requestContents, TResponse response, CancellationToken cancellationToken)
        {
            var responseQueue = cloudQueueClient.GetQueueReference(requestContents.ResponseQueueName);

            var responseContents = new ResponseContents<TResponse>
            {
                RequestId = requestContents.RequestId,
                Response = response
            };

            var responseJson = JsonConvert.SerializeObject(responseContents);

            var responseMessage = new CloudQueueMessage(responseJson);

            await responseQueue.AddMessageAsync(responseMessage, cancellationToken);
        }
    }
}