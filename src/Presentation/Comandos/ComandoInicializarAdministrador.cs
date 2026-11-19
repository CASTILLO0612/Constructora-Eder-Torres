using System.Text;
using ConstructoraEderTorres.Aplicacion.Contratos.Seguridad;

namespace ConstructoraEderTorres.Presentacion.Comandos;

public static class ComandoInicializarAdministrador
{
    public static async Task<int> EjecutarAsync(IServiceProvider servicios, IConfiguration configuracion)
    {
        var nombre = configuracion["Seguridad:AdministradorInicial:NombreUsuario"];
        var contrasena = configuracion["Seguridad:AdministradorInicial:Contrasena"];
        if (string.IsNullOrWhiteSpace(nombre))
        {
            Console.Write("Nombre del primer administrador: ");
            nombre = Console.ReadLine();
        }
        if (string.IsNullOrWhiteSpace(contrasena))
        {
            Console.Write("Contraseña del primer administrador (no se muestra al escribir): ");
            contrasena = LeerContrasena();
            Console.Write("Repita la contraseña: ");
            var confirmacion = LeerContrasena();
            if (!string.Equals(contrasena, confirmacion, StringComparison.Ordinal))
            {
                Console.Error.WriteLine("Las contraseñas no coinciden. No se creó ninguna cuenta.");
                return 1;
            }
        }

        await using var alcance = servicios.CreateAsyncScope();
        var inicializador = alcance.ServiceProvider.GetRequiredService<IInicializadorSeguridad>();
        var resultado = await inicializador.CrearAdministradorInicialAsync(nombre ?? string.Empty, contrasena ?? string.Empty);
        Console.WriteLine(resultado.EsExitoso
            ? $"Administrador creado con identificador {resultado.Valor!.IdUsuario}."
            : resultado.Error!.Mensaje);
        return resultado.EsExitoso ? 0 : 1;
    }

    private static string? LeerContrasena()
    {
        if (Console.IsInputRedirected) return Console.ReadLine();
        var texto = new StringBuilder();
        while (true)
        {
            var tecla = Console.ReadKey(intercept: true);
            if (tecla.Key == ConsoleKey.Enter) break;
            if (tecla.Key == ConsoleKey.Backspace && texto.Length > 0) texto.Length--;
            else if (!char.IsControl(tecla.KeyChar)) texto.Append(tecla.KeyChar);
        }
        Console.WriteLine();
        return texto.ToString();
    }
}
