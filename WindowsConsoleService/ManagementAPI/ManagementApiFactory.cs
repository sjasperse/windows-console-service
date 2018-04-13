using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WindowsConsoleService.ManagementAPI
{
    class ManagementApiFactory
    {
        public IDisposable Create(
            string binding,
            Func<ServiceModel, Result> addService,
            Func<string, Result> removeServiceByName,
            Func<string, Result> stopServiceByName,
            Func<string, Result> startServiceByName
        )
        {
            return new Disposable();
        }

        private class Disposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
