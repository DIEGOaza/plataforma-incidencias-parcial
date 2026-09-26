using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TPARCIAL.Data;
using TPARCIAL.Hubs;
using TPARCIAL.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
    options.UseSqlite(connectionString)
           .AddInterceptors(sp.GetRequiredService<NotificarIncidenciasInterceptor>()));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddControllersWithViews();

// Tiempo real: SignalR (WebSockets) y, opcionalmente, PieHost.
// El ApiSecret de PieHost se lee de user-secrets o de la variable de entorno PieHost__ApiSecret.
builder.Services.AddSignalR();
builder.Services.Configure<PieHostOptions>(builder.Configuration.GetSection(PieHostOptions.Seccion));
builder.Services.AddHttpClient(NotificadorIncidencias.NombreHttpClient, c => c.Timeout = TimeSpan.FromSeconds(5));
builder.Services.AddSingleton<INotificadorIncidencias, NotificadorIncidencias>();
builder.Services.AddSingleton<NotificarIncidenciasInterceptor>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.Migrate();
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
