using Api.Services;
using JPS_ClassLibrary.core.Contexto;
using Microsoft.EntityFrameworkCore;
using Urano_Net_Core_ClassLibrary.Core.Context;

var configuration = new ConfigurationBuilder()
               .SetBasePath(Directory.GetCurrentDirectory())
               .AddJsonFile("appsettings.json")
               .Build();

var optionsBuilder = new DbContextOptionsBuilder<JPSContexto>();
var connectionStringJPS = configuration.GetConnectionString("connectionstring");
var connectionStringUrano = configuration.GetConnectionString("Urano");

var builder = WebApplication.CreateBuilder(args);

// Deshabilitar HTTPS y forzar solo HTTP
builder.WebHost.UseUrls("http://localhost:5000");

builder.Services.AddControllers();

// Add services to the container.

builder.Services.AddDbContext<JPSContexto>(options => options.UseLazyLoadingProxies().UseNpgsql(connectionStringJPS));
builder.Services.AddDbContext<UranoContext>(options => options.UseLazyLoadingProxies().UseNpgsql(connectionStringUrano));

builder.Services.AddScoped<BdService>();
builder.Services.AddScoped<ArchivosService>();

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

app.UseCors(builder =>
{
    builder.AllowAnyOrigin()
        .AllowAnyHeader()
        .WithMethods("GET", "PUT", "POST");
});

app.UseAuthorization();

app.MapControllers();

app.Run();

