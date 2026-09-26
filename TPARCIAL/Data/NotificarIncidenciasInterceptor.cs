using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TPARCIAL.Models;
using TPARCIAL.Services;

namespace TPARCIAL.Data;

/// <summary>
/// Detecta altas y modificaciones de incidencias (p. ej. el cierre) y, solo después de que el estado
/// quede guardado en la base, publica el evento IncidenciaActualizada con Id y Estado.
/// Cubre cualquier código que modifique incidencias mediante el DbContext.
/// </summary>
public class NotificarIncidenciasInterceptor(INotificadorIncidencias notificador) : SaveChangesInterceptor
{
    private static readonly ConditionalWeakTable<DbContext, List<Incidencia>> Pendientes = new();

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

        var incidencias = context.ChangeTracker.Entries<Incidencia>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified)
            .Select(e => e.Entity)
            .ToList();

        if (incidencias.Count > 0)
            Pendientes.AddOrUpdate(context, incidencias);
    }

    private async Task NotificarAsync(DbContext? context, CancellationToken ct)
    {
        if (context is null || !Pendientes.TryGetValue(context, out var incidencias))
            return;

        Pendientes.Remove(context);

        // Tras guardar, una incidencia nueva ya tiene su Id generado.
        foreach (var incidencia in incidencias)
            await notificador.NotificarAsync(new IncidenciaActualizada(incidencia.Id, incidencia.Estado.ToString()), ct);
    }

    private static void Descartar(DbContext? context)
    {
        if (context is not null)
            Pendientes.Remove(context);
    }
}
