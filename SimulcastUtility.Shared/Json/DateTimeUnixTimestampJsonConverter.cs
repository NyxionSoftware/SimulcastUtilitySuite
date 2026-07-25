using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SimulcastUtility.Shared.Json
{
    public class DateTimeUnixTimestampJsonConverter : JsonConverter<DateTime?>
    {
        public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;

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

        public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
        {
            if (!value.HasValue)
            {
                writer.WriteNullValue();
                return;
            }

            DateTime utcDateTime = value.Value.Kind switch
            {
                DateTimeKind.Utc => value.Value,
                DateTimeKind.Local => value.Value.ToUniversalTime(),
                DateTimeKind.Unspecified => DateTime.SpecifyKind(
                    value.Value,
                    DateTimeKind.Utc),

                _ => value.Value
            };

            writer.WriteNumberValue(
                new DateTimeOffset(utcDateTime)
                    .ToUnixTimeMilliseconds());
        }
    }
}
