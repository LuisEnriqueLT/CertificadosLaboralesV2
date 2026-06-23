using System.Text.Json;

namespace CertificadosLaboralesV2.Services.Documents
{
    public static class DatosVariablesHelper
    {
        public static Dictionary<string, string> Parse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new Dictionary<string, string>();

            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                       ?? new Dictionary<string, string>();
            }
            catch (JsonException)
            {
                return new Dictionary<string, string>();
            }
        }

        public static string ToJson(Dictionary<string, string> data) =>
            JsonSerializer.Serialize(data);
    }
}
