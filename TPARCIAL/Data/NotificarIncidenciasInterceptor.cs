using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TPARCIAL.Models;
using TPARCIAL.Services;

namespace TPARCIAL.Data;

/// <summary>
/// Detecta altas y modificaciones de incidencias al guardar y, una vez confirmado el guardado,
/// notifica en tiempo real: "nueva", "abierta" (el estado pasó a Abierta) o "actualizada".
/// Cubre cualquier código que modifique incidencias mediante el DbContext.
/// </summary>
public class NotificarIncidenciasInterceptor(INotificadorIncidencias notificador) : SaveChangesInterceptor
{
    private sealed record Pendiente(Incidencia Incidencia, string Tipo);

    private static readonly ConditionalWeakTable<DbContext, List<Pendiente>> Pendientes = new();

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Registrar(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Registrar(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        NotificarAsync(eventData.Context, CancellationToken.None).GetAwaiter().GetResult();
        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        await NotificarAsync(eventData.Context, cancellationToken);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        Descartar(eventData.Context);
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Descartar(eventData.Context);
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private static void Registrar(DbContext? context)
    {
        if (context is null)
            return;

        var eventos = context.ChangeTracker.Entries<Incidencia>()
            .Select(e => e.State switch
            {
                EntityState.Added => new Pendiente(e.Entity, "nueva"),
                EntityState.Modified when PasoAAbierta(e) => new Pendiente(e.Entity, "abierta"),
                EntityState.Modified => new Pendiente(e.Entity, "actualizada"),
                _ => null
            })
            .OfType<Pendiente>()
            .ToList();

        if (eventos.Count > 0)
            Pendientes.AddOrUpdate(context, eventos);
    }

    private static bool PasoAAbierta(EntityEntry<Incidencia> entry)
    {
        var estado = entry.Property(i => i.Estado);
        return estado.IsModified
               && estado.CurrentValue == EstadoIncidencia.Abierta
               && estado.OriginalValue != EstadoIncidencia.Abierta;
    }

    private async Task NotificarAsync(DbContext? context, CancellationToken ct)
    {
        if (context is null || !Pendientes.TryGetValue(context, out var eventos))
            return;

        Pendientes.Remove(context);

        foreach (var (incidencia, tipo) in eventos)
        {
            // Tras guardar, una incidencia nueva ya tiene su Id generado.
            var estacion = incidencia.Estacion?.Nombre
                           ?? (await context.Set<Estacion>().FindAsync([incidencia.EstacionId], ct))?.Nombre;

            await notificador.NotificarAsync(new IncidenciaNotificacion(
                Guid.NewGuid(),
                tipo,
                incidencia.Id,
                incidencia.Descripcion,
                incidencia.Estado.ToString(),
                estacion,
                incidencia.FechaReporte), ct);
        }
    }

    private static void Descartar(DbContext? context)
    {
        if (context is not null)
            Pendientes.Remove(context);
    }
}
