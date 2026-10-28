using ConstructoraEderTorres.Infraestructura.Configuracion;
using ConstructoraEderTorres.Presentacion.Configuracion;
using ConstructoraEderTorres.Presentacion.PuntosAcceso.Salud;

var constructor = WebApplication.CreateBuilder(args);

constructor.Services.AgregarPresentacion();
constructor.Services.AgregarInfraestructura(constructor.Configuration);

var aplicacion = constructor.Build();

aplicacion.UseExceptionHandler();
aplicacion.UseStatusCodePages();

if (aplicacion.Environment.IsDevelopment())
{
    aplicacion.MapOpenApi();
}
else
{
    aplicacion.UseHsts();
    aplicacion.UseHttpsRedirection();
}

aplicacion.MapearPuntosAccesoSalud();

aplicacion.Run();

public partial class Program
{
}
