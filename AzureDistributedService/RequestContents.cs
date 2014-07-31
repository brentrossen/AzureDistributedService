using System;

namespace AzureDistributedService
{
    /// <summary>
    /// Used within the Distributed Service to wrap request objects for passing from
    /// <see cref="ServiceClient{TRequest,TResponse}"/> to <see cref="ServiceWorker{TRequest,TResponse}"/>.
    /// </summary>
    /// <typeparam name="TRequest">
    /// The application specific request content
    /// </typeparam>
    internal class RequestContents<TRequest>
    {
        public Guid RequestId { get; set; }
        public string ResponseQueueName { get; set; }
        public TRequest Request { get; set; }
    }
}