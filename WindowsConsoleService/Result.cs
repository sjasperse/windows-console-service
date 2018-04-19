using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WindowsConsoleService
{
    public class Result
    {
        public static Result Fail(string failureMessage)
        {
            return new Result(
                false,
                new[] { failureMessage }
            );
        }
        public static Result<TData> Fail<TData>(string failureMessage)
        {
            return new Result<TData>(
                false,
                new[] { failureMessage },
                default(TData)
            );
        }
        public static Result Successful()
        {
            return new Result(true, Enumerable.Empty<string>());
        }
        public static Result<TData> Successful<TData>(TData data)
        {
            return new Result<TData>(true, Enumerable.Empty<string>(), data);
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

    public class Result<TData> : Result
    {
        public Result(
            bool success,
            IEnumerable<string> failureMessages,
            TData data
        ) : base(success, failureMessages)
        {
            Data = data;
        }

        public TData Data { get; }
    }
}
