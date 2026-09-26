using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TPARCIAL.Data;

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
}
