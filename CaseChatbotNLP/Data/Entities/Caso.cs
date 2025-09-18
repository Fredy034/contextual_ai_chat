using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CaseChatbotNLP.Data.Entities
{
    public class Caso
    {
        [Key]
        public string NumeroCaso { get; set; }

        [Required]
        public string Descripcion { get; set; }

        [Required]
        public string Sede { get; set; }

        public string Usuario { get; set; }

        public string Responsable { get; set; }

        [Column(TypeName = "datetime2")]
        public DateTime FechaCreacion { get; set; }

        public string Estado { get; set; }
    }
}