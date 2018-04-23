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
            return ToActionResult(serviceManager.AddService(service));
        }

        [Route("services/{serviceName}")]
        [HttpDelete]
        public IHttpActionResult RemoveService(string serviceName)
        {
            return ToActionResult(serviceManager.RemoveService(serviceName));
        }

        [Route("services/{serviceName}/start")]
        [HttpPut]
        public IHttpActionResult StartService(string serviceName)
        {
            return ToActionResult(serviceManager.StartService(serviceName));
        }

        [Route("services/{serviceName}/stop")]
        [HttpPut]
        public IHttpActionResult StopService(string serviceName)
        {
            return ToActionResult(serviceManager.StopService(serviceName));
        }

        [Route("services/{serviceName}/status")]
        [HttpGet]
        public IHttpActionResult GetStatus(string serviceName)
        {
            var r = serviceManager.GetIsRunning(serviceName);

            if (!r.Success)
            {
                return this.Ok(r);
            }

            return this.Ok(new
            {
                Success = r.Success,
                Status = r.Data ? "Running" : "Stopped",
                Running = r.Data
            });
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

        private IHttpActionResult ToActionResult(Result result)
        {
            return this.Ok(result);
        }
    }
}
