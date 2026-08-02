using System;
using System.Collections.Generic;
using System.Text;

namespace SimulcastUtility.Infrastructure.Options
{
    public sealed class JsonReceiverRepositoryOptions
    {
        public const string SectionName = "ReceiverStorage";

        public string Directory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SimulcastUtility");

        public string FileName { get; set; } = "receivers.json";

        public string GetFullPath()
        {
            if (string.IsNullOrWhiteSpace(Directory))
                throw new InvalidOperationException("The receiver storage directory has not been configured.");

            if (string.IsNullOrWhiteSpace(FileName))
                throw new InvalidOperationException("The receiver storage file name has not been configured.");

            return Path.Combine(Directory, FileName);
        }
    }
}
