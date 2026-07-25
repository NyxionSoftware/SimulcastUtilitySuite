using System;
using System.Collections.Generic;
using System.Text;

namespace SimulcastUtility.Shared.Models
{
    public sealed class ReceiverOperationResult
    {
        public bool Successful { get; }

        public string? Error { get; }

        public bool Changed { get; }

        private ReceiverOperationResult(bool successful, string? error, bool changed)
        {
            Successful = successful;
            Error = error;
            Changed = changed;
        }

        public static ReceiverOperationResult Success(bool changed = true)
        {
            return new ReceiverOperationResult(true, null, changed: changed);
        }

        public static ReceiverOperationResult Failure(string error)
        {
            return new ReceiverOperationResult(false, error, changed: false);
        }
    }
}
