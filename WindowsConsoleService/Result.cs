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
