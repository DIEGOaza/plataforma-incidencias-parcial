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
}
