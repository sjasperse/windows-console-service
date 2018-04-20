using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace WindowsConsoleService
{
    public partial class Form1 : Form
    {
        private readonly Logger logger;
        private readonly TabControlManager tabControlManager;
        private IDisposable managementApi;
        private readonly StoredStateManager storedStateManager;

        public Form1()
        {
            InitializeComponent();
            this.Disposed += Form1_Disposed;

            logger = new Logger();
            tabControlManager = new TabControlManager(tabControl1, logger);

            var configurationStoragePath =
                Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "config.json");
            storedStateManager = new StoredStateManager(configurationStoragePath);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            Initialize();
        }
        private void Form1_Disposed(object sender, EventArgs e)
        {
            tabControlManager.Dispose();
            managementApi.Dispose();
        }

        private void Initialize()
        {
            string defaultManagementApiBinding = "http://localhost:8080/";

            logger.Info($"Loading configuration from {storedStateManager.Filename}...");
            var storedModel = storedStateManager.Get();
            if (storedModel == null)
            {
                logger.Info("No configuration to load.");

                storedModel = new StorageModel()
                {
                    ManagementApi = new StorageModel.ManagementApiModel()
                    {
                        Binding = defaultManagementApiBinding
                    },
                    Services = new []
                    {
                        new ServiceModel()
                        {
                            Name = "example",
                            DisplayName = "Example",
                            Filename = "cmd",
                            Arguments = "/C \"echo Hello world && pause\""
                        }
                    }
                };
                storedStateManager.Store(storedModel);
                logger.Info("Created example config file");
            }

            var managementApiBinding = defaultManagementApiBinding;
            {
                if (!string.IsNullOrEmpty(storedModel.ManagementApi?.Binding))
                {
                    managementApiBinding = storedModel.ManagementApi.Binding;
                    logger.Info($"Using management API binding '{managementApiBinding}' from confgiuration");
                }

                if (storedModel.Services != null)
                {
                    foreach (var service in storedModel.Services)
                    {
                        logger.Info($"Adding service '{service.DisplayName}'...");
                        var r = tabControlManager.AddService(service);
                        if (!r.Success)
                        {
                            logger.Error($"Failed to add service.\n{string.Join("\n\t", r.FailureMessages)}");
                        }
                    }
                    logger.Info($"Finished loading services from configuration");
                }
            }

            logger.Info("Initializing Management Api...");
            var apiFactory = new ManagementAPI.ManagementApiFactory();
            try
            {
                var apiLogger = new Logger();
                managementApi = apiFactory.Create(
                    managementApiBinding,
                    tabControlManager,
                    apiLogger
                );
                tabControlManager.AddTab("api", "Api", apiLogger);

                logger.Info($"Management Api listening on {managementApiBinding}");
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to start Management Api.\n{ex}");
            }

            logger.Info("Attaching config write-back listeners...");
            {
                tabControlManager.OnServiceAdded += (s, e) =>
                {
                    try
                    {
                        var current = storedStateManager.Get();

                        current.Services =
                            current.Services
                            .Union(new[] { e.Data });

                        storedStateManager.Store(current);

                        logger.Info("Stored config updated.");
                    }
                    catch (Exception ex)
                    {
                        logger.Error($"Error while updating stored config.\n{ex}");
                    }
                };

                tabControlManager.OnServiceRemoved += (s, e) =>
                {
                    try
                    { 
                        var current = storedStateManager.Get();

                        current.Services =
                            current.Services
                            .Where(x => x.Name != e.Data.Name)
                            .ToArray();

                        storedStateManager.Store(current);

                        logger.Info("Stored config updated.");
                    }
                    catch (Exception ex)
                    {
                        logger.Error($"Error while updating stored config.\n{ex}");
                    }

                };
            }

            logger.Info("Ready");
        }
    }

    public interface IServiceManager
    {
        IEnumerable<ServiceModel> GetServices();
        Result AddService(ServiceModel service);
        Result RemoveService(string serviceName);
        Result StopService(string serviceName);
        Result StartService(string serviceName);
        Result<bool> GetIsRunning(string serviceName);
    }


    public class EventArgs<TData> : EventArgs
    {
        public EventArgs(
            TData data
            )
        {
            Data = data;
        }

        public TData Data { get; }
    }

    class StoredStateManager
    {
        private readonly string filename;
        private readonly JsonSerializerSettings serializerSettings;

        public StoredStateManager(string filename)
        {
            this.filename = filename;
            this.serializerSettings = new JsonSerializerSettings()
            {
                Formatting = Formatting.Indented
            };
        }

        public string Filename { get => filename; }

        public StorageModel Get()
        {
            if (File.Exists(filename))
            {
                var contents = File.ReadAllText(filename);
                return JsonConvert.DeserializeObject<StorageModel>(contents, serializerSettings);
            }

            return null;
        }
        public void Store(StorageModel storageModel)
        {
            File.WriteAllText(filename, JsonConvert.SerializeObject(storageModel, serializerSettings));
        }
    }
}
