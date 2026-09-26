using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TPARCIAL.Data;
using TPARCIAL.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
    options.UseSqlite(connectionString)
           .AddInterceptors(sp.GetRequiredService<InvalidarCacheIncidenciasInterceptor>()));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddControllersWithViews();

// Algolia: la AdminApiKey se obtiene de user-secrets o de la variable de entorno Algolia__AdminApiKey.
builder.Services.Configure<AlgoliaOptions>(builder.Configuration.GetSection(AlgoliaOptions.Seccion));
builder.Services.AddSingleton<IIncidenciaSearch, AlgoliaIncidenciaSearch>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();

    // Publica las incidencias en el índice de Algolia si hay credenciales configuradas.
    if (app.Services.GetRequiredService<IOptions<AlgoliaOptions>>().Value.EstaConfigurado)
    {
        try
        {
            var incidencias = await db.Incidencias.Include(i => i.Estacion).AsNoTracking().ToListAsync();
            await app.Services.GetRequiredService<IIncidenciaSearch>().SincronizarAsync(incidencias);
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning(ex, "No se pudo sincronizar el índice de Algolia.");
        }
    }
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
   .WithStaticAssets();

app.MapHub<IncidenciasHub>(IncidenciasHub.Ruta);

app.Run();
