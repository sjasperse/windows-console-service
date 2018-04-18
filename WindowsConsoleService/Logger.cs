using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WindowsConsoleService
{
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
}
