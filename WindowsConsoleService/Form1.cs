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
                            Arguments = "/C \"echo Hello world\""
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

    class TabControlManager : IServiceManager
    {
        private readonly TabControl tabControl;
        private readonly Logger mainLogger;

        public event EventHandler<EventArgs<ServiceModel>> OnServiceAdded;
        public event EventHandler<EventArgs<ServiceModel>> OnServiceRemoved;

        public TabControlManager(TabControl tabControl, Logger logger)
        {
            this.tabControl = tabControl;
            this.mainLogger = logger;

            AddMainTab(logger);
        }

        private void AddMainTab(Logger logger)
        {
            var tab = CreateTab("Main", "Main", logger);
            tabControl.TabPages.Add(tab);
        }

        public Result AddService(ServiceModel service)
        {
            var tabPages = tabControl.TabPages.Cast<TabPage>();
            if (tabPages.Any(x => x.Name == service.Name)) return Result.Fail($"There is already a service registered with the name '{service.Name}'");

            var serviceExecutionModel = new ServiceExecutionModel(service);

            var tab = CreateTab(
                service.Name,
                service.DisplayName,
                serviceExecutionModel.Logger,
                serviceExecutionModel
            );

            ModifyUI(() =>
            {
                tabControl.TabPages.Add(tab);
            });

            serviceExecutionModel.Start();

            try
            {
                OnServiceAdded?.Invoke(this, new EventArgs<ServiceModel>(service));
            }
            catch (Exception ex)
            {
                mainLogger.Error(ex.ToString());
            }
            return Result.Successful();
        }
        public Result RemoveService(string serviceName)
        {
            var tab = GetTabByName(serviceName);

            if (tab == null) return Result.Fail($"Service named '{serviceName}' not found");

            var serviceExecutionModel = GetModel(tab);
            tabControl.TabPages.Remove(tab);
            tab.Dispose();
            serviceExecutionModel.Dispose();

            try
            {
                OnServiceRemoved?.Invoke(this, new EventArgs<ServiceModel>(serviceExecutionModel.Source));
            }
            catch (Exception ex)
            {
                mainLogger.Error(ex.ToString());
            }
            return Result.Successful();
        }
        public Result StopService(string serviceName)
        {
            var tab = GetTabByName(serviceName);

            if (tab == null) return Result.Fail($"Service named '{serviceName}' not found");

            var model = GetModel(tab);
            if (model == null) throw new Exception($"The model for tab '{serviceName}' is null");
            if (!model.IsRunning) return Result.Fail($"Service named '{serviceName}' is not running");

            model.Stop();

            return Result.Successful();
        }
        public Result StartService(string serviceName)
        {
            var tab = GetTabByName(serviceName);

            if (tab == null) return Result.Fail($"Service named '{serviceName}' not found");

            var model = GetModel(tab);
            if (model == null) throw new Exception($"The model for tab '{serviceName}' is null");
            if (model.IsRunning) return Result.Fail($"Service named '{serviceName}' is already running");

            model.Start();

            return Result.Successful();
        }
        public IEnumerable<ServiceModel> GetServices()
        {
            return
                GetServiceTabPages()
                .Select(x => GetModel(x).Source)
                .ToArray();
        }
        public Result AddTab(string name, string displayName, Logger logger)
        {
            var tab = CreateTab(name, displayName, logger);
            tabControl.TabPages.Add(tab);

            return Result.Successful();
        }
        public Result<bool> GetIsRunning(string serviceName)
        {
            var tab = GetTabByName(serviceName);

            if (tab == null) return Result.Fail<bool>($"Service named '{serviceName}' not found");

            var model = GetModel(tab);
            if (model == null) throw new Exception($"The model for tab '{serviceName}' is null");

            return Result.Successful(model.IsRunning);
        }

        private IEnumerable<TabPage> GetServiceTabPages()
        {
            return
                tabControl.TabPages
                .Cast<TabPage>()
                .Where(x => x.Tag is ServiceExecutionModel);
        }
        private TabPage GetTabByName(string serviceName)
        {
            return
                GetServiceTabPages()
                .SingleOrDefault(x => x.Name == serviceName);
        }
        private ServiceExecutionModel GetModel(TabPage tabPage)
        {
            return tabPage.Tag as ServiceExecutionModel;
        }
        private TabPage CreateTab(string name, string displayName, Logger logger, ServiceExecutionModel serviceExecutionModel = null)
        {
            var tabPage = new TabPage(displayName)
            {
                Name = name,
                Tag = serviceExecutionModel
            };

            var richTextBox = new RichTextBox()
            {
                Font = new Font("Lucida Console", 10),
                ForeColor = Color.LightGray,
                BackColor = Color.Black,
                Dock = DockStyle.Fill,
                ReadOnly = true,
                WordWrap = false,
                ScrollBars = RichTextBoxScrollBars.Both,
                DetectUrls = true
            };
            richTextBox.LinkClicked += (s, e) => { Process.Start(e.LinkText); };

            logger.OnMessage += (s, e) =>
            {
                var color = richTextBox.ForeColor;
                switch (e.Level)
                {
                    case MessageLevel.Error:
                        color = Color.Red;
                        break;

                    case MessageLevel.Warn:
                        color = Color.Yellow;
                        break;

                    case MessageLevel.Debug:
                        color = Color.Gray;
                        break;
                }

                ModifyUI(() =>
                {
                    richTextBox.Select(richTextBox.TextLength, 0);
                    richTextBox.SelectionColor = color;
                    richTextBox.SelectedText = $"{e.Message}\n";
                });
            };

            tabPage.Controls.Add(richTextBox);
            return tabPage;
        }
        private void ModifyUI(Action modify)
        {
            if (tabControl.InvokeRequired)
            {
                tabControl.Invoke(modify);
            }
            else
            {
                modify();
            }
        }

        class ServiceExecutionModel : IDisposable
        {
            private Process process;

            public ServiceExecutionModel(
                ServiceModel service
            )
            {
                Source = service;
                Name = service.Name;
                Filename = service.Filename;
                Arguments = service.Arguments;

                Logger = new Logger();
            }

            public ServiceModel Source { get; }
            public string Name { get; }
            public string Filename { get; }
            public string Arguments { get; }
            public Logger Logger { get; }
            public bool IsRunning { get => (!process?.HasExited) ?? false; }

            public void Start()
            {
                if (IsRunning) throw new Exception("Already running");

                var startInfo = new ProcessStartInfo()
                {
                    FileName = Filename,
                    Arguments = Arguments,
                    
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };
                process = new Process() { StartInfo = startInfo };
                process.ErrorDataReceived += (s, e) => Logger.Error(e.Data);
                process.OutputDataReceived += (s, e) => Logger.Info(e.Data);
                process.Start();
                process.BeginErrorReadLine();
                process.BeginOutputReadLine();
                process.Exited += (s, e) => Logger.Info("Exited");

            }

            public void Stop()
            {
                if (!IsRunning) throw new Exception("Already stopped");

                process.Close();
                process = null;
            }

            public void Dispose()
            {
                process?.Dispose();
            }
        }
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
