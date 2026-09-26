using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TPARCIAL.Data;
using TPARCIAL.Models;
using TPARCIAL.Services;

namespace TPARCIAL.Controllers;

[Authorize]
public class OperacionesController(ApplicationDbContext context, INotificadorIncidencias notificador) : Controller
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
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cerrar(int id, CancellationToken ct)
    {
        var incidencia = await context.Incidencias.FindAsync([id], ct);
        if (incidencia is null)
            return NotFound();

        if (incidencia.Estado != EstadoIncidencia.Cerrada)
        {
            // 1. Guardar primero el estado en la base.
            incidencia.Estado = EstadoIncidencia.Cerrada;
            await context.SaveChangesAsync(ct);

            // 2. Publicar IncidenciaActualizada { Id, Estado } en PieHost (y SignalR).
            //    Sin el token de la petición: el cierre ya está guardado y el evento debe salir igualmente.
            await notificador.NotificarAsync(new IncidenciaActualizada(incidencia.Id, incidencia.Estado.ToString()));
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
