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

namespace WindowsConsoleService
{
    public partial class Form1 : Form
    {
        private readonly string configurationStoragePath;
        private readonly Logger logger;
        private readonly TabControlManager tabControlManager;

        public Form1()
        {
            InitializeComponent();

            logger = new Logger();
            tabControlManager = new TabControlManager(tabControl1, logger);

            configurationStoragePath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "config.json");
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            Initialize();
        }

        private void Initialize()
        {
            logger.Info("Initializing...");

            logger.Info("Adding test service...");
            tabControlManager.AddService(new ServiceModel()
            {
                Name = "test",
                DisplayName = "Test",
                Filename = "cmd",
                Arguments = "/C echo test"
            });

            logger.Info("Finished initializing");
        }
    }

    class TabControlManager
    {
        private readonly TabControl tabControl;
        private readonly Logger mainLogger;

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

            tabControl.TabPages.Add(tab);

            serviceExecutionModel.Start();

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
                ScrollBars = RichTextBoxScrollBars.Both
            };
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

                Action addText = () =>
                {
                    richTextBox.Select(richTextBox.TextLength, 0);
                    richTextBox.SelectionColor = color;
                    richTextBox.SelectedText = $"{e.Message}\n";
                };
                if (!richTextBox.InvokeRequired)
                {
                    addText();
                }
                else
                {
                    richTextBox.Invoke(addText);
                }
            };

            tabPage.Controls.Add(richTextBox);
            return tabPage;
        }

        class ServiceExecutionModel : IDisposable
        {
            private Process process;

            public ServiceExecutionModel(
                ServiceModel service
            )
            {
                Name = service.Name;
                Filename = service.Filename;
                Arguments = service.Arguments;

                Logger = new Logger();
            }

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
                process.Exited += (s, e) => Logger.Info("Exited");
                process.Start();
                process.BeginErrorReadLine();
                process.BeginOutputReadLine();

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
}
