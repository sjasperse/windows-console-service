using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

namespace WindowsConsoleService.ManagementAPI
{
    public class ManagementApiController : ApiController
    {
        private readonly IServiceManager serviceManager;
        private readonly System.Web.Http.Description.IApiExplorer apiExplorer;

        public ManagementApiController(
            IServiceManager serviceManager,
            System.Web.Http.Description.IApiExplorer apiExplorer
        )
        {
            this.serviceManager = serviceManager;
            this.apiExplorer = apiExplorer;
        }

        [Route("ping")]
        [HttpGet]
        public IHttpActionResult Ping()
        {
            return this.Ok("OK");
        }

        [Route("error")]
        [HttpGet]
        public IHttpActionResult Error()
        {
            throw new Exception();
        }

        [Route("services")]
        [HttpGet]
        public IHttpActionResult GetServices(ServiceModel service)
        {
            return this.Ok(serviceManager.GetServices());
        }

        [Route("services")]
        [HttpPost]
        public IHttpActionResult AddService(ServiceModel service)
        {
            return serviceManager.AddService(service).ToActionResult(this);
        }

        [Route("services/{serviceName}")]
        [HttpDelete]
        public IHttpActionResult RemoveService(string serviceName)
        {
            return serviceManager.RemoveService(serviceName).ToActionResult(this);
        }

        [Route("services/{serviceName}/start")]
        [HttpPut]
        public IHttpActionResult StartService(string serviceName)
        {
            return serviceManager.StartService(serviceName).ToActionResult(this);
        }

        [Route("services/{serviceName}/stop")]
        [HttpPut]
        public IHttpActionResult StopService(string serviceName)
        {
            return serviceManager.StopService(serviceName).ToActionResult(this);
        }

        [Route("")]
        [HttpGet]
        public IHttpActionResult Help()
        {
            return this.Ok(
                apiExplorer
                    .ApiDescriptions
                    .Select(x => new
                    {
                        Method = x.HttpMethod.Method,
                        Path = x.RelativePath,
                        x.Documentation
                    })
                    .ToArray()
            );
        }
    }

    public static class ResultExtensions
    {
        public static IHttpActionResult ToActionResult(this Result result, ApiController controller)
        {
            if (result.Success)
                return new System.Web.Http.Results.NegotiatedContentResult<string>(System.Net.HttpStatusCode.OK, "OK", controller);

            return new System.Web.Http.Results.NegotiatedContentResult<IEnumerable<string>>(System.Net.HttpStatusCode.BadRequest, result.FailureMessages, controller);
        }
    }
}
