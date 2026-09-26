using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TPARCIAL.Data;
using TPARCIAL.Models;

namespace TPARCIAL.Controllers;

[Authorize]
public class OperacionesController(ApplicationDbContext context) : Controller
{
    // GET: /Operaciones/Incidencias
    public async Task<IActionResult> Incidencias()
    {
        var incidencias = await context.Incidencias
            .Include(i => i.Estacion)
            .OrderByDescending(i => i.FechaReporte)
            .AsNoTracking()
            .ToListAsync();

        return View(incidencias);
    }

    // POST: /Operaciones/Cerrar/5
    // Primero se guarda el estado; al confirmarse el guardado, NotificarIncidenciasInterceptor
    // publica IncidenciaActualizada { Id, Estado } en PieHost y SignalR.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cerrar(int id, CancellationToken ct)
    {
        var incidencia = await context.Incidencias.FindAsync([id], ct);
        if (incidencia is null)
            return NotFound();

        if (incidencia.Estado != EstadoIncidencia.Cerrada)
        {
            incidencia.Estado = EstadoIncidencia.Cerrada;
            await context.SaveChangesAsync(ct);
        }

        return RedirectToAction(nameof(Incidencias));
    }

    // GET: /Operaciones/EstadoIncidencias
    // Estado vigente del listado; el cliente lo consulta al reconectar el WebSocket.
    [HttpGet]
    public async Task<IActionResult> EstadoIncidencias(CancellationToken ct)
    {
        var incidencias = await context.Incidencias
            .AsNoTracking()
            .OrderByDescending(i => i.FechaReporte)
            .Select(i => new
            {
                i.Id,
                Estacion = i.Estacion!.Nombre,
                i.Descripcion,
                Estado = i.Estado.ToString(),
                i.FechaReporte
            })
            .ToListAsync(ct);

        return Json(incidencias);
    }
}
