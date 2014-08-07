namespace AzureDistributedService
{
    /// <summary>
    /// Small struct to contain both request and response queue names
    /// </summary>
    public struct ServiceClientQueueNames
    {
        /// <summary>
        /// The name of the storage request queue. This should be the same for all clients and workers.
        /// </summary>
        public string RequestQueueName;

        /// <summary>
        /// The name of the response queue. This should be unique for each client.
        /// </summary>
        public string ResponseQueueName;
    }
}