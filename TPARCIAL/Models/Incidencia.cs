using System.ComponentModel.DataAnnotations;

namespace TPARCIAL.Models;

public enum EstadoIncidencia
{
    Abierta,
    EnProceso,
    Cerrada
}

public class Incidencia
{
    public int Id { get; set; }

    [Required, StringLength(500)]
    public string Descripcion { get; set; } = string.Empty;

    public EstadoIncidencia Estado { get; set; } = EstadoIncidencia.Abierta;

    public DateTime FechaReporte { get; set; }

    public int EstacionId { get; set; }
    public Estacion? Estacion { get; set; }
}
