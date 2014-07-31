namespace AzureDistributedService
{
    /// <summary>
    /// Small struct to contain both request and response queue names
    /// </summary>
    public struct ServiceClientQueueNames
    {
        public string RequestQueueName, ResponseQueueName;
    }
}