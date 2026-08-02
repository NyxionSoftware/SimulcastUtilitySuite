using System;
using System.Collections.Generic;
using System.Text;

namespace SimulcastUtility.Application.Protocol
{
    public sealed class CommandResult<TResponse>
    {
        private CommandResult(bool isSuccess, TResponse? response, string? errorMessage, Exception? exception)
        {
            IsSuccess = isSuccess;
            Response = response;
            ErrorMessage = errorMessage;
            Exception = exception;
        }

        public bool IsSuccess { get; }

        public TResponse? Response { get; }

        public string? ErrorMessage { get; }

        public Exception? Exception { get; }

        public static CommandResult<TResponse> Success(TResponse response)
        {
            return new CommandResult<TResponse>(true, response, null, null);
        }

        public static CommandResult<TResponse> Failure(string errorMessage, Exception? exception = null)
        {
            return new CommandResult<TResponse>(false, default, errorMessage, exception);
        }
    }
}
