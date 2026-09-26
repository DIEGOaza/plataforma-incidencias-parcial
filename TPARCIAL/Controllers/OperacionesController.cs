using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TPARCIAL.Data;
using TPARCIAL.Models;
using TPARCIAL.Services;

namespace TPARCIAL.Controllers;

[Authorize]
public class OperacionesController(ApplicationDbContext context, IIncidenciasCache cache) : Controller
{
    // GET: /Operaciones/Incidencias
    public async Task<IActionResult> Incidencias(CancellationToken ct)
    {
        var incidencias = await cache.ObtenerListadoAsync(CargarListadoAsync, ct);
        return View(incidencias);
    }

    // Proyección sin ciclos (Estacion -> Incidencias) para poder serializarla en Redis.
    private Task<List<Incidencia>> CargarListadoAsync(CancellationToken ct) =>
        context.Incidencias
            .AsNoTracking()
            .OrderByDescending(i => i.FechaReporte)
            .Select(i => new Incidencia
            {
                Id = i.Id,
                Descripcion = i.Descripcion,
                Estado = i.Estado,
                FechaReporte = i.FechaReporte,
                EstacionId = i.EstacionId,
                Estacion = new Estacion { Id = i.Estacion!.Id, Nombre = i.Estacion.Nombre }
            })
            .ToListAsync(ct);
}
