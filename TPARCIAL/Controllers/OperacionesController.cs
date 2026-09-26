using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TPARCIAL.Data;
using TPARCIAL.Models;
using TPARCIAL.Services;

namespace TPARCIAL.Controllers;

[Authorize]
public class OperacionesController(
    ApplicationDbContext context,
    IIncidenciaSearch buscador,
    ILogger<OperacionesController> logger) : Controller
{
    // GET: /Operaciones/Incidencias?busqueda=texto
    public async Task<IActionResult> Incidencias(string? busqueda, CancellationToken ct)
    {
        busqueda = busqueda?.Trim();
        ViewData["Busqueda"] = busqueda;

        // Sin texto: listado habitual, solo con incidencias Abiertas.
        if (string.IsNullOrEmpty(busqueda))
        {
            var abiertas = await context.Incidencias
                .Include(i => i.Estacion)
                .Where(i => i.Estado == EstadoIncidencia.Abierta)
                .OrderByDescending(i => i.FechaReporte)
                .AsNoTracking()
                .ToListAsync(ct);

            return View(abiertas);
        }

        IReadOnlyList<int> ids;
        try
        {
            ids = await buscador.BuscarIdsAsync(busqueda, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Error al consultar Algolia con el texto {Busqueda}", busqueda);
            ViewData["ErrorBusqueda"] = "No se pudo realizar la búsqueda en este momento. Inténtelo nuevamente.";
            return View(new List<Incidencia>());
        }

        // Solo incidencias que existan en la BD y estén Abiertas (Algolia puede tener registros obsoletos).
        var encontradas = await context.Incidencias
            .Include(i => i.Estacion)
            .Where(i => ids.Contains(i.Id) && i.Estado == EstadoIncidencia.Abierta)
            .AsNoTracking()
            .ToListAsync(ct);

        // Conservar el orden de relevancia devuelto por Algolia.
        var orden = ids.Select((id, pos) => (id, pos)).ToDictionary(x => x.id, x => x.pos);
        return View(encontradas.OrderBy(i => orden[i.Id]).ToList());
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
