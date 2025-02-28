using Api.Services;
using JPS_ClassLibrary.core.Contexto;
using Microsoft.EntityFrameworkCore;

var configuration = new ConfigurationBuilder()
               .SetBasePath(Directory.GetCurrentDirectory())
               .AddJsonFile("appsettings.json")
               .Build();

var optionsBuilder = new DbContextOptionsBuilder<JPSContexto>();
var connectionStringJPS = configuration.GetConnectionString("connectionstring");

var builder = WebApplication.CreateBuilder(args);

// Deshabilitar HTTPS y forzar solo HTTP
builder.WebHost.UseUrls("http://localhost:5000");

builder.Services.AddControllers();

// Add services to the container.

builder.Services.AddDbContext<JPSContexto>(options => options.UseLazyLoadingProxies().UseNpgsql(connectionStringJPS));

builder.Services.AddScoped<ConsultasService>();

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

