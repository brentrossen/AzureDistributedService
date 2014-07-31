using System;

namespace AzureDistributedServiceTests
{
    public class TestResponse
    {
        public int RequestNumber { get; set; }
        public DateTimeOffset StartTime { get; set; }
        public DateTimeOffset ProcessingCompletionTime { get; set; }
    }
}