using System;
using System.Threading.Tasks;
using System.Web.Http;
using AzureDistributedService;
using AzureDistributedServiceTests;

namespace TestServiceFrontEnd.Controllers
{
    public class ServiceController : ApiController
    {
        private readonly ServiceClient<TestRequest, TestResponse> serviceClient;
        private readonly TimeSpan requestTimeout = TimeSpan.FromSeconds(30);

        public ServiceController(ServiceClient<TestRequest, TestResponse> serviceClient)
        {
            this.serviceClient = serviceClient;
        }

        /// <summary>
        /// POST a <see cref="TestRequest"/>
        /// </summary>
        /// <param name="request"></param>
        /// <returns>The resulting <see cref="TestResponse"/></returns>
        public async Task<TestResponse> Post([FromBody]TestRequest request)
        {
            return await serviceClient.SubmitRequestAsync(request, requestTimeout);
        }

        /// <summary>
        /// Utility method for verifying that the calls complete successfully.
        /// It's much easier to call a Get(int) from a browser than to call
        /// a Post(TestRequest). But Post should be used in deployment.
        /// </summary>
        public async Task<TestResponse> Get(int id)
        {
            var result = await serviceClient.SubmitRequestAsync(
                new TestRequest
                {
                    RequestNumber = id,
                    StartTime = DateTimeOffset.UtcNow
                }, requestTimeout);

            return result;
        }
    }
}
