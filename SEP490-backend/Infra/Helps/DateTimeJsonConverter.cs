using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sep490_Backend.Infra.Helps
{
    public class DateTimeJsonConverter : JsonConverter<DateTime>
    {
        private static readonly string[] DateTimeFormats = new[]
        {
            "yyyy-MM-ddTHH:mm:ss.fffZ", // ISO8601 with milliseconds
            "yyyy-MM-ddTHH:mm:ssZ",     // ISO8601
            "yyyy-MM-ddTHH:mm:ss",      // Without timezone
            "yyyy-MM-dd HH:mm:ss",      // Common format with space
            "yyyy/MM/dd HH:mm:ss",      // Slash format
            "dd/MM/yyyy HH:mm:ss",      // European format
            "MM/dd/yyyy HH:mm:ss",      // US format
            "yyyy-MM-dd",               // Date only ISO
            "yyyy/MM/dd",               // Date only slash
            "dd/MM/yyyy",               // Date only European
            "MM/dd/yyyy"                // Date only US
        };

        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                string dateString = reader.GetString();

                // Handle empty or null string values
                if (string.IsNullOrWhiteSpace(dateString))
                {
                    return DateTime.MinValue;
                }

                // Try to parse using the specified formats
                if (DateTime.TryParseExact(dateString, DateTimeFormats, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeLocal, out DateTime date))
                {
                    return date;
                }

                // If that fails, try standard parsing
                if (DateTime.TryParse(dateString, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeLocal, out date))
                {
                    return date;
                }

                // Return minimum value instead of throwing an exception
                return DateTime.MinValue;
            }

            // For numeric timestamps (Unix epoch)
            if (reader.TokenType == JsonTokenType.Number)
            {
                long timestamp = reader.GetInt64();
                return DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime;
            }

            // Handle null values
            if (reader.TokenType == JsonTokenType.Null)
            {
                return DateTime.MinValue;
            }

            return DateTime.MinValue;
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            // Format consistently for output 
            writer.WriteStringValue(value.ToString("yyyy-MM-ddTHH:mm:ss"));
        }
    }

    public class NullableDateTimeJsonConverter : JsonConverter<DateTime?>
    {
        private static readonly string[] DateTimeFormats = new[]
        {
            "yyyy-MM-ddTHH:mm:ss.fffZ", // ISO8601 with milliseconds
            "yyyy-MM-ddTHH:mm:ssZ",     // ISO8601
            "yyyy-MM-ddTHH:mm:ss",      // Without timezone
            "yyyy-MM-dd HH:mm:ss",      // Common format with space
            "yyyy/MM/dd HH:mm:ss",      // Slash format
            "dd/MM/yyyy HH:mm:ss",      // European format
            "MM/dd/yyyy HH:mm:ss",      // US format
            "yyyy-MM-dd",               // Date only ISO
            "yyyy/MM/dd",               // Date only slash
            "dd/MM/yyyy",               // Date only European
            "MM/dd/yyyy"                // Date only US
        };

        public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                string dateString = reader.GetString();

                // Handle empty or null string values
                if (string.IsNullOrWhiteSpace(dateString))
                {
                    return null;
                }

                // Try to parse using the specified formats
                if (DateTime.TryParseExact(dateString, DateTimeFormats, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeLocal, out DateTime date))
                {
                    return date;
                }

                // If that fails, try standard parsing
                if (DateTime.TryParse(dateString, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeLocal, out date))
                {
                    return date;
                }

                // Return null on parsing error instead of throwing
                return null;
            }

            // Handle null JSON value
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            return null;
        }

        public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
            {
                writer.WriteStringValue(value.Value.ToString("yyyy-MM-ddTHH:mm:ss"));
            }
            else
            {
                writer.WriteNullValue();
            }
        }
    }
}