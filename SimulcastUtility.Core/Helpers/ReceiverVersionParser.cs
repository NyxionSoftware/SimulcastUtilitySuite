using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SimulcastUtility.Core.Helpers
{
    public static partial class ReceiverVersionParser
    {
        public static readonly DateTime NewVersionCutoff = new(2025, 5, 4);

        public static DateTime? ParseApkVersionDate(string? apkVersion)
        {
            if (string.IsNullOrWhiteSpace(apkVersion))
                return null;

            Match match = ApkDateRegex().Match(apkVersion);

            if (!match.Success)
                return null;

            return DateTime.TryParseExact(
                match.Groups["date"].Value,
                "yyyyMMdd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime versionDate)
                    ? versionDate
                    : null;
        }

        [GeneratedRegex(@"(?:^|\.)T(?<date>\d{8})(?:\.|$)", RegexOptions.CultureInvariant)]
        private static partial Regex ApkDateRegex();
    }
}
