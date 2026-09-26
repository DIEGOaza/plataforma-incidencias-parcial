using System.ComponentModel.DataAnnotations;

namespace TPARCIAL.Models;

public class Estacion
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string Nombre { get; set; } = string.Empty;

    public ICollection<Incidencia> Incidencias { get; set; } = new List<Incidencia>();
}
