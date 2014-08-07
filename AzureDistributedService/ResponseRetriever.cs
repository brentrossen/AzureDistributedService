using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;

namespace AzureDistributedService
{
    /// <summary>
    /// Manages the retrieval task and maps response objects to the original request object.
    /// </summary>
    /// <typeparam name="TRequest"></typeparam>
    /// <typeparam name="TResponse"></typeparam>
    internal class ResponseRetriever<TRequest, TResponse>
    {
        private readonly CloudQueue responseQueue;
        private readonly TimeSpan responseCheckFrequency;
        private readonly ConcurrentDictionary<Guid, TimeoutAndCompletionSource> completionSources;
        private Task retrievalTask;

        public ResponseRetriever(CloudQueue responseQueue, TimeSpan responseCheckFrequency)
        {
            if (responseQueue == null) throw new ArgumentNullException("responseQueue");

            this.responseQueue = responseQueue;
            this.responseCheckFrequency = responseCheckFrequency;
            completionSources = new ConcurrentDictionary<Guid, TimeoutAndCompletionSource>();
            StartRetrievalTask();
            retrievalTask.ContinueWith(
                FaultAllWaitingRequests,
                TaskContinuationOptions.OnlyOnFaulted);
        }

        private void StartRetrievalTask()
        {
            retrievalTask = Task.Run(
                async () => { await DoRetrieveResponsesAsync(); });
        }

        public Task<TResponse> SubmitRequest(RequestContents<TRequest> requestContents, TimeSpan timeout)
        {
            if (requestContents == null) throw new ArgumentNullException("requestContents");

            var completionSource = new TaskCompletionSource<TResponse>();

            completionSources[requestContents.RequestId] = new TimeoutAndCompletionSource
            {
                TimeoutDateTimeOffset = DateTimeOffset.UtcNow + timeout,
                TaskCompletionSource = completionSource
            };

            GuaranteeRetrievalTaskRunning();

            return completionSource.Task;
        }

        private void GuaranteeRetrievalTaskRunning()
        {
            if (retrievalTask.IsFaulted || retrievalTask.IsCompleted)
            {
                // If the retrieval task has stopped, start it again
                StartRetrievalTask();
            }
        }

        private async Task DoRetrieveResponsesAsync()
        {
            // Run forever, checking if there are any tasks waiting for reponses
            while (true)
            {
                // Try to retrieve responses only if there are tasks waiting
                while (completionSources.Any())
                {
                    try
                    {
                        await RetrieveSetOfResponsesAsync();
                    }
                    catch (Exception ex)
                    {
                        Trace.TraceError(ex.ToString());
                    }
                }
                await Task.Delay(10);
            }
        }

        private async Task RetrieveSetOfResponsesAsync()
        {
            // Retrieve the max number of messages possible in a batch
            // Set the messages invisible for 5 seconds for processing time, after 5 seconds they will re-appear
            // and be processed a second time.
            var cloudQueueMessages = await responseQueue.GetMessagesAsync(32, TimeSpan.FromSeconds(5), null, null);
            var responseMessages = cloudQueueMessages.ToArray();
            var deletionTasks = new List<Task>();

            foreach (var responseMessage in responseMessages)
            {
                var responseContents =
                    JsonConvert.DeserializeObject<ResponseContents<TResponse>>(responseMessage.AsString);

                SetCompletionResult(responseContents);

                var deletionTask = responseQueue.DeleteMessageAsync(responseMessage);
                deletionTasks.Add(deletionTask);
            }

            if (deletionTasks.Any()) await Task.WhenAll(deletionTasks);

            if (!responseMessages.Any())
            {
                // Only check for timed out tasks if there are no tasks in the queue to process
                CheckTimedOutTasks();

                await Task.Delay(responseCheckFrequency);
            }
        }

        private void SetCompletionResult(ResponseContents<TResponse> responseContents)
        {
            TimeoutAndCompletionSource completionSource;
            var requestId = responseContents.RequestId;
            if (completionSources.TryRemove(requestId, out completionSource))
            {
                completionSource.TaskCompletionSource.TrySetResult(responseContents.Response);
            }
            else
            {
                Trace.WriteLine("Error: ServiceClient received response that was not requested with the current client.");
            }
        }

        private void CheckTimedOutTasks()
        {
            var now = DateTimeOffset.UtcNow;
            var completionSourcesToRemove = new List<Guid>();
            foreach (var taskCompletionSource in completionSources)
            {
                var timeoutAndCompletionSource = taskCompletionSource.Value;
                if (timeoutAndCompletionSource.TimeoutDateTimeOffset >= now) continue;
                timeoutAndCompletionSource.TaskCompletionSource.TrySetException(new TimeoutException("Task timed out."));
                completionSourcesToRemove.Add(taskCompletionSource.Key);
            }
            foreach (var guid in completionSourcesToRemove)
            {
                TimeoutAndCompletionSource timedOutCompletionSource;
                completionSources.TryRemove(guid, out timedOutCompletionSource);
            }
        }

        private void FaultAllWaitingRequests(Task task)
        {
            if (!task.IsFaulted || task.Exception == null) return;

            foreach (var taskCompletionSource in completionSources.Values)
            {
                taskCompletionSource.TaskCompletionSource.TrySetException(task.Exception);
            }
        }

        private struct TimeoutAndCompletionSource
        {
            public DateTimeOffset TimeoutDateTimeOffset;
            public TaskCompletionSource<TResponse> TaskCompletionSource;
        }
    }
}