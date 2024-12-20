using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Newtonsoft.Json;

namespace Sep490_Backend.Infra.Helps
{
    public class JsonValueConverter<T> : ValueConverter<T, string>
    {
        public JsonValueConverter() : base(
            v => JsonConvert.SerializeObject(v),
            v => JsonConvert.DeserializeObject<T>(v))
        {
        }
    }
}
