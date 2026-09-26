using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TPARCIAL.Models;

namespace TPARCIAL.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext(options)
{
    public DbSet<Estacion> Estaciones => Set<Estacion>();
    public DbSet<Incidencia> Incidencias => Set<Incidencia>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Incidencia>()
            .Property(i => i.Estado)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Entity<Estacion>().HasData(
            new Estacion { Id = 1, Nombre = "Estación Central" },
            new Estacion { Id = 2, Nombre = "Estación Norte" },
            new Estacion { Id = 3, Nombre = "Estación Sur" });

        builder.Entity<Incidencia>().HasData(
            new Incidencia { Id = 1, EstacionId = 1, Descripcion = "Escalera mecánica detenida", Estado = EstadoIncidencia.Abierta, FechaReporte = new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc) },
            new Incidencia { Id = 2, EstacionId = 1, Descripcion = "Torniquete de acceso averiado", Estado = EstadoIncidencia.Cerrada, FechaReporte = new DateTime(2026, 9, 2, 9, 30, 0, DateTimeKind.Utc) },
            new Incidencia { Id = 3, EstacionId = 2, Descripcion = "Fuga de agua en andén", Estado = EstadoIncidencia.Abierta, FechaReporte = new DateTime(2026, 9, 3, 7, 15, 0, DateTimeKind.Utc) },
            new Incidencia { Id = 4, EstacionId = 3, Descripcion = "Iluminación deficiente en pasillo", Estado = EstadoIncidencia.EnProceso, FechaReporte = new DateTime(2026, 9, 4, 18, 45, 0, DateTimeKind.Utc) },
            new Incidencia { Id = 5, EstacionId = 3, Descripcion = "Máquina expendedora sin servicio", Estado = EstadoIncidencia.Abierta, FechaReporte = new DateTime(2026, 9, 5, 12, 0, 0, DateTimeKind.Utc) });
    }
}
