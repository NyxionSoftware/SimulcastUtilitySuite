using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SimulcastUtility.Application.Converters
{
    public class DateTimeUnixTimestampJsonConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            long milliseconds = reader.TokenType switch
            {
                JsonTokenType.Number => reader.GetInt64(),

                JsonTokenType.String when long.TryParse(
                    reader.GetString(),
                    out long parsed) => parsed,

                _ => throw new JsonException(
                    $"Expected a Unix timestamp in milliseconds, but found {reader.TokenType}.")
            };

            return DateTimeOffset
                .FromUnixTimeMilliseconds(milliseconds)
                .UtcDateTime;
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            DateTime utcDateTime = value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                DateTimeKind.Unspecified => DateTime.SpecifyKind(
                    value,
                    DateTimeKind.Utc),

                _ => value
            };

            writer.WriteNumberValue(
                new DateTimeOffset(utcDateTime)
                    .ToUnixTimeMilliseconds());
        }
    }
}
