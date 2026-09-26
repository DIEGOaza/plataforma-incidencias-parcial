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
    IIncidenciasCache cache,
    INotificadorIncidencias notificador,
    ILogger<OperacionesController> logger) : Controller
{
    // GET: /Operaciones/Incidencias?busqueda=texto
    public async Task<IActionResult> Incidencias(string? busqueda, CancellationToken ct)
    {
        busqueda = busqueda?.Trim();
        ViewData["Busqueda"] = busqueda;

        // Sin texto: listado general de incidencias Abiertas, cacheado en Redis.
        if (string.IsNullOrEmpty(busqueda))
            return View(await cache.ObtenerListadoAsync(CargarAbiertasAsync, ct));

        // Con texto: se consulta Algolia directamente, sin pasar por la caché.
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

            // El cierre ya está guardado: lo que sigue no se cancela aunque el cliente corte la petición.
            // 2. Invalidar la clave del listado en Redis antes de volver a consultarlo.
            await cache.InvalidarAsync();

            // 3. Publicar IncidenciaActualizada { Id, Estado } en PieHost (y SignalR).
            await notificador.NotificarAsync(new IncidenciaActualizada(incidencia.Id, incidencia.Estado.ToString()));
        }

        return RedirectToAction(nameof(Incidencias));
    }

    // GET: /Operaciones/EstadoIncidencias
    // Estado vigente (directo de la BD) de las incidencias abiertas; el cliente lo consulta al reconectar el WebSocket.
    [HttpGet]
    public async Task<IActionResult> EstadoIncidencias(CancellationToken ct)
    {
        var abiertas = await CargarAbiertasAsync(ct);
        return Json(abiertas.Select(i => new
        {
            i.Id,
            Estacion = i.Estacion?.Nombre,
            i.Descripcion,
            Estado = i.Estado.ToString(),
            i.FechaReporte
        }));
    }

    // Proyección sin ciclos (Estacion -> Incidencias) para poder serializarla en Redis.
    private Task<List<Incidencia>> CargarAbiertasAsync(CancellationToken ct) =>
        context.Incidencias
            .AsNoTracking()
            .Where(i => i.Estado == EstadoIncidencia.Abierta)
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
