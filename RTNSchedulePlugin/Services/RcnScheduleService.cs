using HtmlAgilityPack;
using RTNSchedulePlugin.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;

namespace RTNSchedulePlugin.Services
{
    public static class RcnScheduleService
    {
        private const string ScheduleUrl = "https://www.rtn.tv/rcnschedule/rcnschedule.aspx";

        private static readonly HttpClient HttpClient = CreateHttpClient();

        public static async Task<RcnScheduleResult> LoadScheduleAsync(CancellationToken cancellationToken = default)
        {
            string html = await HttpClient.GetStringAsync(ScheduleUrl, cancellationToken);

            return ParseSchedule(html);
        }

        private static RcnScheduleResult ParseSchedule(string html)
        {
            HtmlDocument document = new();

            document.LoadHtml(html);

            string title = ParseScheduleTitle(document);

            HtmlNode? table = document.DocumentNode.SelectSingleNode("//table[@id='MainContent_tblScheduleList']");

            if (table is null)
                throw new InvalidOperationException("The RCN schedule table could not be found.");

            List<RcnScheduleItem> items = [];

            HtmlNodeCollection? rows = table.SelectNodes(".//tr");

            if (rows is null)
                return new RcnScheduleResult(title, items);

            foreach (HtmlNode row in rows)
            {
                HtmlNodeCollection? cells = row.SelectNodes("./td");

                if (cells is null || cells.Count != 4)
                    continue;

                string channel = CleanText(cells[0].InnerText);
                string eventName = CleanText(cells[1].InnerText);
                string onAirTimeText = CleanText(cells[2].InnerText);
                string durationText = CleanText(cells[3].InnerText);

                if (channel.Equals("CHANNEL #", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (string.IsNullOrWhiteSpace(channel) || string.IsNullOrWhiteSpace(eventName))
                    continue;

                DateTime? startTime = ParseOnAirTime(onAirTimeText);
                TimeSpan? duration = TryParseDuration(durationText);

                items.Add(new RcnScheduleItem
                {
                    ChannelNumber = channel,
                    EventName = eventName,
                    StartTime = startTime,
                    Duration = duration,
                    OriginalOnAirTime = onAirTimeText,
                    OriginalDuration = durationText
                });
            }

            IReadOnlyList<RcnScheduleItem> sortedItems = items
                .OrderBy(item => item.StartTime ?? DateTime.MaxValue)
                .ThenBy(item => item.EventName)
                .ToList();

            return new RcnScheduleResult(title, sortedItems);
        }

        private static string ParseScheduleTitle(HtmlDocument document)
        {
            HtmlNode? titleNode = document.DocumentNode.SelectSingleNode("//*[@id='MainContent_lblProgramList']");

            if (titleNode is null)
                return "Today's RCN Schedule";

            string text = CleanText(titleNode.InnerText);

            const string prefix = "RCN SIMULCAST SCHEDULE FOR";

            if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                text = text[prefix.Length..].Trim();

            return ToTitleCase(text);
        }

        private static DateTime? ParseOnAirTime(string value)
        {
            string normalized = value.Replace(".", string.Empty).Trim();

            string[] formats =
            [
                "h:mm tt",
            "hh:mm tt",
            "h:m tt",
            "hh:m tt"
            ];

            if (!DateTime.TryParseExact(
                    normalized,
                    formats,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateTime parsed))
            {
                return null;
            }

            TimeZoneInfo easternTimeZone = TimeZoneInfo.FindSystemTimeZoneById(
                OperatingSystem.IsWindows()
                    ? "Eastern Standard Time"
                    : "America/New_York");

            DateTime easternNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, easternTimeZone);

            DateTime easternDateTime = new(
                easternNow.Year,
                easternNow.Month,
                easternNow.Day,
                parsed.Hour,
                parsed.Minute,
                0,
                DateTimeKind.Unspecified);

            return TimeZoneInfo.ConvertTime(easternDateTime, easternTimeZone, TimeZoneInfo.Local);
        }

        private static TimeSpan? TryParseDuration(string value)
        {
            string[] parts = value.Split(':');

            if (parts.Length != 2)
                return null;

            if (!int.TryParse(parts[0], out int hours))
                return null;

            if (!int.TryParse(parts[1], out int minutes))
                return null;

            if (hours < 0 || minutes is < 0 or > 59)
                return null;

            return new TimeSpan(hours, minutes, 0);
        }

        private static string CleanText(string value)
        {
            return WebUtility.HtmlDecode(value)
                .Replace('\u00A0', ' ')
                .Trim();
        }

        private static string ToTitleCase(string value)
        {
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value.ToLowerInvariant());
        }

        private static HttpClient CreateHttpClient()
        {
            HttpClient client = new()
            {
                Timeout = TimeSpan.FromSeconds(20)
            };

            client.DefaultRequestHeaders.UserAgent.ParseAdd("SimulcastUtility-RTNSchedulePlugin/2.0");

            return client;
        }
    }
}
