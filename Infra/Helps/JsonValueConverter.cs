using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Newtonsoft.Json;

namespace Api_Project_Prn.Infra.Helps
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
