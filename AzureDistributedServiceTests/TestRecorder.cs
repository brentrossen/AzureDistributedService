using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace AzureDistributedServiceTests
{
    /// <summary>
    /// Records test results
    /// TODO: only try to create tables once per instantiation of the instance, save the connection string, etc
    /// </summary>
    public static class TestRecorder
    {
        public static void RecordAvgLatency(string storageConnectionString, 
            string instanceName, 
            TimeSpan avgRequestTime,
            int totalRequests, 
            int tps, 
            TimeSpan elapsed)
        {
            var cloudStorageAccount = CloudStorageAccount.Parse(storageConnectionString);
            var cloudTableClient = cloudStorageAccount.CreateCloudTableClient();
            CloudTable tableReference = cloudTableClient.GetTableReference("DistributedServiceTestResults");
            tableReference.CreateIfNotExists();
            var now = DateTimeOffset.UtcNow;
            var nowTicks = (DateTimeOffset.MaxValue - now).Ticks.ToString();
            tableReference.Execute(
                TableOperation.InsertOrReplace(new DynamicTableEntity(nowTicks, nowTicks, null,
                    new Dictionary<string, EntityProperty>
                    {
                        {"InstanceName", new EntityProperty(instanceName)},
                        {"AvgRequestTimeInSec", new EntityProperty(avgRequestTime.TotalSeconds)},
                        {"TotalRequests", new EntityProperty(totalRequests)},
                        {"TargetTPS", new EntityProperty(tps)},
                        {"TotalTimeInSec", new EntityProperty(elapsed.TotalSeconds)},
                        {"ActualTPS", new EntityProperty(totalRequests/elapsed.TotalSeconds) }
                    })));
        }

        public static void LogException(string storageConnectionString, string instanceName, Exception ex)
        {
            Trace.WriteLine(ex);
            var cloudStorageAccount = CloudStorageAccount.Parse(storageConnectionString);
            var cloudTableClient = cloudStorageAccount.CreateCloudTableClient();
            CloudTable tableReference = cloudTableClient.GetTableReference("DistributedServiceExceptions");
            tableReference.CreateIfNotExists();
            var now = DateTimeOffset.UtcNow;
            var nowTicks = (DateTimeOffset.MaxValue - now).Ticks.ToString();
            tableReference.Execute(
                TableOperation.InsertOrReplace(new DynamicTableEntity(nowTicks, nowTicks, null,
                    new Dictionary<string, EntityProperty>
                    {
                        {"InstanceName", new EntityProperty(instanceName)},
                        {"Exception", new EntityProperty(ex.ToString())},
                    })));
        }
    }
}