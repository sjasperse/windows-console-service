using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WindowsConsoleService
{
    public class StorageModel
    {
        public ManagementApiModel ManagementApi { get; set; }

        public IEnumerable<ServiceModel> Services { get; set; }

        public class ManagementApiModel
        {
            public string Binding { get; set; }
        }
    }
}
