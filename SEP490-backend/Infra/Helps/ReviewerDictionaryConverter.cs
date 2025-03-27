using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Sep490_Backend.Infra.Helps
{
    public class ReviewerDictionaryConverter : System.Text.Json.Serialization.JsonConverter<Dictionary<int, bool>>
    {
        public override Dictionary<int, bool> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return new Dictionary<int, bool>();
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                var jsonString = reader.GetString();
                return System.Text.Json.JsonSerializer.Deserialize<Dictionary<int, bool>>(jsonString, options) ?? new Dictionary<int, bool>();
            }

            using var doc = JsonDocument.ParseValue(ref reader);
            var dictionary = new Dictionary<int, bool>();

            foreach (var property in doc.RootElement.EnumerateObject())
            {
                if (int.TryParse(property.Name, out var key))
                {
                    dictionary[key] = property.Value.GetBoolean();
                }
            }

            return dictionary;
        }

        public override void Write(Utf8JsonWriter writer, Dictionary<int, bool> value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            foreach (var item in value)
            {
                writer.WritePropertyName(item.Key.ToString());
                writer.WriteBooleanValue(item.Value);
            }

            writer.WriteEndObject();
        }
    }

    public class ReviewerDictionaryValueConverter : ValueConverter<Dictionary<int, bool>, string>
    {
        public ReviewerDictionaryValueConverter() 
            : base(
                  v => ConvertToDatabase(v),
                  v => ConvertFromDatabase(v))
        {
        }

        private static string ConvertToDatabase(Dictionary<int, bool> dictionary)
        {
            if (dictionary == null || dictionary.Count == 0)
            {
                return "{}";
            }
            return Newtonsoft.Json.JsonConvert.SerializeObject(dictionary);
        }

        private static Dictionary<int, bool> ConvertFromDatabase(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return new Dictionary<int, bool>();
            }

            try
            {
                // First try to deserialize as a standard dictionary
                return Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<int, bool>>(value) ?? new Dictionary<int, bool>();
            }
            catch
            {
                try
                {
                    // If standard deserialization fails, try parsing as JToken
                    var token = JToken.Parse(value);
                    
                    if (token.Type == JTokenType.Integer)
                    {
                        // If it's just a number (like "1"), create a dictionary with that key
                        var userId = token.Value<int>();
                        return new Dictionary<int, bool> { { userId, false } };
                    }
                    else if (token.Type == JTokenType.Array)
                    {
                        // If it's an array, create a dictionary with default entries
                        var result = new Dictionary<int, bool>();
                        var array = (JArray)token;
                        for (int i = 0; i < array.Count; i++)
                        {
                            result[i] = true;
                        }
                        return result;
                    }

                    // For other unexpected formats, return empty dictionary
                    return new Dictionary<int, bool>();
                }
                catch
                {
                    // If all parsing fails, return an empty dictionary
                    return new Dictionary<int, bool>();
                }
            }
        }
    }
} 