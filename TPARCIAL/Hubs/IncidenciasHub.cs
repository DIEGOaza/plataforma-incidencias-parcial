using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace TPARCIAL.Hubs;

/// <summary>
/// Hub de tiempo real para incidencias. El servidor emite el evento "IncidenciaActualizada";
/// los clientes solo escuchan (no invocan métodos).
/// </summary>
[Authorize]
public class IncidenciasHub : Hub
{
    public const string Ruta = "/hubs/incidencias";
    public const string EventoIncidencia = "IncidenciaActualizada";
}
