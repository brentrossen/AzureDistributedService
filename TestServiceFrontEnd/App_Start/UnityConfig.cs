using System;
using AzureDistributedService;
using AzureDistributedServiceTests;
using Microsoft.Practices.Unity;
using System.Web.Http;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.ServiceRuntime;
using Unity.WebApi;

namespace TestServiceFrontEnd
{
    public static class UnityConfig
    {
        public static void RegisterComponents()
        {
			var container = new UnityContainer();
            
            // Register the ServiceClient for use in the controller
            var tuple = GetServiceClientSettings();
            var requestQueueName = tuple.Item1;
            string responseQueueName = tuple.Item2;
            string storageConnectionString = tuple.Item3;
            TimeSpan waitBetweenPolls = tuple.Item4;
            RegisterServiceClient(storageConnectionString, requestQueueName, responseQueueName, waitBetweenPolls, container);

            GlobalConfiguration.Configuration.DependencyResolver = new UnityDependencyResolver(container);
        }

        private static Tuple<string, string, string, TimeSpan> GetServiceClientSettings()
        {
            string requestQueueName = CloudConfigurationManager.GetSetting("ServiceRequestQueue");
            string instanceId = RoleEnvironment.IsAvailable ? RoleEnvironment.CurrentRoleInstance.Id : Environment.MachineName;
            string responseQueueName = "response-queue-" + instanceId.ToLowerInvariant().Replace('_', '-');
            string storageConnectionString = CloudConfigurationManager.GetSetting("StorageConnectionString");
            int waitBetweenPollsMs = Int32.Parse(CloudConfigurationManager.GetSetting("WaitBetweenPollsMs"));
            TimeSpan waitBetweenPolls = TimeSpan.FromMilliseconds(waitBetweenPollsMs);
            return Tuple.Create(requestQueueName, responseQueueName, storageConnectionString, waitBetweenPolls);
        }

        private static void RegisterServiceClient(string storageConnectionString, string requestQueueName,
            string responseQueueName, TimeSpan waitBetweenPolls, IUnityContainer container)
        {
            var serviceClient = new ServiceClient<TestRequest, TestResponse>(
                storageConnectionString,
                new ServiceClientQueueNames
                {
                    RequestQueueName = requestQueueName,
                    ResponseQueueName = responseQueueName
                }, waitBetweenPolls);

            serviceClient.InitializeQueuesAsync().Wait();

            // Uses ContainerControlledLifetimeManager to guarantee the service client is a singleton
            container.RegisterInstance(serviceClient,
                new ContainerControlledLifetimeManager());
        }
    }
}