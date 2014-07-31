using System;

namespace AzureDistributedService
{
    /// <summary>
    /// Used within the Distributed Service to wrap response objects for passing from
    /// <see cref="ServiceWorker{TRequest,TResponse}"/> to <see cref="ServiceClient{TRequest,TResponse}"/>.
    /// </summary>
    /// <typeparam name="TResponse">
    /// The application specific response content
    /// </typeparam>
    internal class ResponseContents<TResponse>
    {
        public Guid RequestId { get; set; }
        public TResponse Response { get; set; }
    }
}