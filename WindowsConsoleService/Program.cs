using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WindowsConsoleService
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }
    }


    public class Logger
    {
        public event EventHandler<MessageEventArgs> OnMessage;

        public void Debug(string message)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine(message);
            Console.ResetColor();

            OnMessage?.Invoke(this, new MessageEventArgs(MessageLevel.Debug, message));
        }

        public void Info(string message)
        {
            Console.WriteLine(message);

            OnMessage?.Invoke(this, new MessageEventArgs(MessageLevel.Info, message));
        }

        public void Warn(string message)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(message);
            Console.ResetColor();

            OnMessage?.Invoke(this, new MessageEventArgs(MessageLevel.Warn, message));
        }

        public void Error(string message)
        {
            Console.Error.WriteLine(message);

            OnMessage?.Invoke(this, new MessageEventArgs(MessageLevel.Error, message));
        }
    }

    public enum MessageLevel
    {
        Debug,
        Info,
        Warn,
        Error
    }

    public class MessageEventArgs : EventArgs
    {
        public MessageEventArgs(
            MessageLevel level,
            string message
        )
        {
            Level = level;
            Message = message;
        }

        public MessageLevel Level { get; }
        public string Message { get; }
    }

    public class StorageModel
    {
        public ManagementApiModel ManagementApi { get; set; }

        public IEnumerable<Service> Services { get; set; }

        public class Service
        {
            public string Name { get; set; }
            public string Filename { get; set; }
            public string Arguments { get; set; }
        }

        public class ManagementApiModel
        {
            public string Binding { get; set; }
            public int Port { get; set; }
        }
    }

    class ServiceModel
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string Filename { get; set; }
        public string Arguments { get; set; }
    }

    public class Result
    {
        public static Result Fail(string failureMessage)
        {
            return new Result(
                false,
                new[] { failureMessage }
            );
        }
        public static Result Successful()
        {
            return new Result(true, Enumerable.Empty<string>());
        }

        public Result(
            bool success,
            IEnumerable<string> failureMessages
            )
        {
            Success = success;
            FailureMessages = failureMessages;
        }

        public bool Success { get; }
        public IEnumerable<string> FailureMessages { get; }
    }
}
