using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Alt_Support.Converters
{
    public class JiraDateTimeConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                var dateString = reader.GetString();
                if (string.IsNullOrEmpty(dateString))
                    return DateTime.MinValue;

                // Try different Jira date formats
                var formats = new[]
                {
                    "yyyy-MM-ddTHH:mm:ss.fffzzz",    // ISO 8601 with milliseconds and timezone
                    "yyyy-MM-ddTHH:mm:ss.ffzzz",     // ISO 8601 with milliseconds and timezone
                    "yyyy-MM-ddTHH:mm:ss.fzzz",      // ISO 8601 with milliseconds and timezone
                    "yyyy-MM-ddTHH:mm:sszzz",        // ISO 8601 with timezone
                    "yyyy-MM-ddTHH:mm:ss.fffZ",      // ISO 8601 with milliseconds and Z
                    "yyyy-MM-ddTHH:mm:ss.ffZ",       // ISO 8601 with milliseconds and Z
                    "yyyy-MM-ddTHH:mm:ss.fZ",        // ISO 8601 with milliseconds and Z
                    "yyyy-MM-ddTHH:mm:ssZ",          // ISO 8601 with Z
                    "yyyy-MM-ddTHH:mm:ss.fff",       // ISO 8601 with milliseconds
                    "yyyy-MM-ddTHH:mm:ss.ff",        // ISO 8601 with milliseconds
                    "yyyy-MM-ddTHH:mm:ss.f",         // ISO 8601 with milliseconds
                    "yyyy-MM-ddTHH:mm:ss"            // Basic ISO 8601
                };

                foreach (var format in formats)
                {
                    if (DateTime.TryParseExact(dateString, format, CultureInfo.InvariantCulture, 
                        DateTimeStyles.RoundtripKind, out var result))
                    {
                        return result;
                    }
                }

                // Fallback to standard parsing
                if (DateTime.TryParse(dateString, out var fallbackResult))
                {
                    return fallbackResult;
                }

                throw new JsonException($"Unable to parse DateTime: {dateString}");
            }

            throw new JsonException("Expected string for DateTime");
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
        }
    }

    public class JiraNullableDateTimeConverter : JsonConverter<DateTime?>
    {
        public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;

            if (reader.TokenType == JsonTokenType.String)
            {
                var dateString = reader.GetString();
                if (string.IsNullOrEmpty(dateString))
                    return null;

                // Try different Jira date formats
                var formats = new[]
                {
                    "yyyy-MM-ddTHH:mm:ss.fffzzz",    // ISO 8601 with milliseconds and timezone
                    "yyyy-MM-ddTHH:mm:ss.ffzzz",     // ISO 8601 with milliseconds and timezone
                    "yyyy-MM-ddTHH:mm:ss.fzzz",      // ISO 8601 with milliseconds and timezone
                    "yyyy-MM-ddTHH:mm:sszzz",        // ISO 8601 with timezone
                    "yyyy-MM-ddTHH:mm:ss.fffZ",      // ISO 8601 with milliseconds and Z
                    "yyyy-MM-ddTHH:mm:ss.ffZ",       // ISO 8601 with milliseconds and Z
                    "yyyy-MM-ddTHH:mm:ss.fZ",        // ISO 8601 with milliseconds and Z
                    "yyyy-MM-ddTHH:mm:ssZ",          // ISO 8601 with Z
                    "yyyy-MM-ddTHH:mm:ss.fff",       // ISO 8601 with milliseconds
                    "yyyy-MM-ddTHH:mm:ss.ff",        // ISO 8601 with milliseconds
                    "yyyy-MM-ddTHH:mm:ss.f",         // ISO 8601 with milliseconds
                    "yyyy-MM-ddTHH:mm:ss"            // Basic ISO 8601
                };

                foreach (var format in formats)
                {
                    if (DateTime.TryParseExact(dateString, format, CultureInfo.InvariantCulture, 
                        DateTimeStyles.RoundtripKind, out var result))
                    {
                        return result;
                    }
                }

                // Fallback to standard parsing
                if (DateTime.TryParse(dateString, out var fallbackResult))
                {
                    return fallbackResult;
                }

                throw new JsonException($"Unable to parse nullable DateTime: {dateString}");
            }

            throw new JsonException("Expected string or null for nullable DateTime");
        }

        public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
                writer.WriteStringValue(value.Value.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
            else
                writer.WriteNullValue();
        }
    }
}