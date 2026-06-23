using System.Net;

namespace CertificadosLaboralesV2.Services.Documents
{
    public class ReplaceService
    {
        // Keys in this set contain intentional HTML (images, etc.) — skip encoding
        private static readonly HashSet<string> HtmlSafeKeys =
        [
            "{{LogoEmpresa}}",
            "{{Firma}}"
        ];

        public string ReplacePlaceholders(string contenido, Dictionary<string, string> reemplazos)
        {
            foreach (var par in reemplazos)
            {
                var value = HtmlSafeKeys.Contains(par.Key)
                    ? par.Value
                    : WebUtility.HtmlEncode(par.Value);

                contenido = contenido.Replace(par.Key, value);
            }
            return contenido;
        }
    }
}
