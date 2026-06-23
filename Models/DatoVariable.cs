namespace CertificadosLaboralesV2.Models
{
    public class DatoVariable
    {
        public int Id { get; set; }
        public string NombreCampo { get; set; } = null!;
        public string Clave { get; set; } = null!;
        public TipoDatoVariable TipoDato { get; set; }
    }

    public enum TipoDatoVariable
    {
        Texto,
        Numero,
        Fecha,
        Moneda,
        Booleano
    }
}
