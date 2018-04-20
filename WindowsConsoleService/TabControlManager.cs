using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WindowsConsoleService
{
    class TabControlManager : IServiceManager, IDisposable
    {
        private readonly TabControl tabControl;
        private readonly Logger mainLogger;
        private readonly Queue<Action> waitingForUIUpdate;
        private readonly System.Timers.Timer uiUpdateTimer;

        public event EventHandler<EventArgs<ServiceModel>> OnServiceAdded;
        public event EventHandler<EventArgs<ServiceModel>> OnServiceRemoved;

        public TabControlManager(TabControl tabControl, Logger logger)
        {
            this.tabControl = tabControl;
            this.mainLogger = logger;
            this.waitingForUIUpdate = new Queue<Action>();
            this.uiUpdateTimer = new System.Timers.Timer(1000);
            this.uiUpdateTimer.Elapsed += (s, e) => UpdateUI();
            this.uiUpdateTimer.Start();

            AddMainTab(logger);
        }

        private void AddMainTab(Logger logger)
        {
            var tab = CreateTab("Main", "Main", logger, null, true);
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
                .Where(x => x.Tag is ServiceExecutionModel)
                .ToArray();
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
        private TabPage CreateTab(string name, string displayName, Logger logger, ServiceExecutionModel serviceExecutionModel = null, bool useImmediateMessageWriting = false)
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
                ModifyUI(() => WriteMessageToRichTextBox(richTextBox, 
                    e.Message,
                    e.Level
                ), useImmediateMessageWriting);
            };

            tabPage.Controls.Add(richTextBox);
            return tabPage;
        }
        private void WriteMessageToRichTextBox(RichTextBox richTextBox, string message, MessageLevel messageLevel)
        {
            var color = richTextBox.ForeColor;
            switch (messageLevel)
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

            richTextBox.Select(richTextBox.TextLength, 0);
            richTextBox.SelectionColor = color;
            richTextBox.SelectedText = $"{message}\n";
        }
        private void ModifyUI(Action modify, bool useImmediateMessageWriting = false)
        {
            if (tabControl.IsDisposed) return;

            if (!useImmediateMessageWriting)
            {
                waitingForUIUpdate.Enqueue(modify);
            }
            else
            {
                tabControl.Invoke(modify);
            }
        }

        private bool updatingUI = false;
        private void UpdateUI()
        {
            if (updatingUI) return;
            if (tabControl.IsDisposed) return;
            if (!waitingForUIUpdate.Any()) return;

            try
            {
                var stopwatch = Stopwatch.StartNew();
                while (waitingForUIUpdate.Any())
                {
                    var action = waitingForUIUpdate.Dequeue();

                    if (tabControl.IsDisposed) return;
                    tabControl.Invoke(action);
                }
                mainLogger.Debug($"UI Update: {stopwatch.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                mainLogger.Error($"Error while updating UI:\n{ex.ToString()}");
            }
            finally
            {
                updatingUI = false;
            }
        }

        public void Dispose()
        {
            foreach (var tab in GetServiceTabPages())
            {
                var model = GetModel(tab);
                model.Dispose();
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
            public bool IsRunning {
                get
                {
                    return !(process?.HasExited ?? false);
                }
            }

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

                process.Kill();
                process.Dispose();
                process = null;
            }

            #region IDisposable Support
            private bool isDisposed = false; // To detect redundant calls

            protected virtual void Dispose(bool disposing)
            {
                if (!isDisposed)
                {
                    if (disposing)
                    {
                        if (!(process?.HasExited ?? false))
                        {
                            process?.Kill();
                        }
                        process?.Dispose();
                    }

                    isDisposed = true;
                }
            }

            public void Dispose()
            {
                Dispose(true);
            }
            #endregion
        }
    }
}
