using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Dependencies;
using System.Web.Http.ExceptionHandling;
using LightInject;
using Microsoft.Owin.Hosting;
using Owin;

namespace WindowsConsoleService.ManagementAPI
{
    class ManagementApiFactory
    {
        public IDisposable Create(
            string binding,
            IServiceManager serviceManager,
            Logger logger
        )
        {
            var app = WebApp.Start(binding, appBuilder =>
            {
                appBuilder.Use(async (context, next) =>
                {

                    await next();

                    logger.Debug($"API : {context.Request.Method.ToUpper()} {context.Request.Path} - {context.Response.StatusCode}");
                });

                var ioc = new ServiceContainer();
                ioc.RegisterInstance(serviceManager);
                ioc.RegisterApiControllers();

                var http = new HttpConfiguration();
                ioc.EnableWebApi(http);

                // not sure why I have to add it here instead of into the IoC
                http.Services.Add(typeof(System.Web.Http.ExceptionHandling.IExceptionLogger), new GlobalExceptionLogger(logger));

                http.MapHttpAttributeRoutes();
                http.EnsureInitialized();
                http.Formatters.Remove(http.Formatters.XmlFormatter);
                http.Formatters.OfType<System.Net.Http.Formatting.JsonMediaTypeFormatter>().First().SerializerSettings.ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver();
                ioc.RegisterInstance(http.Services.GetApiExplorer());

                appBuilder.UseWebApi(http);
            });

            return app;
        }

        private class GlobalExceptionLogger : System.Web.Http.ExceptionHandling.IExceptionLogger
        {
            private readonly Logger logger;

            public GlobalExceptionLogger(Logger logger)
            {
                this.logger = logger;
            }

            public Task LogAsync(ExceptionLoggerContext context, CancellationToken cancellationToken)
            {
                logger.Error($"API : {context.Request.Method.ToString().ToUpper()} {context.Request.RequestUri.ToString()}\n{context.Exception}");

                return Task.FromResult<object>(null);
            }
        }
    }
}
