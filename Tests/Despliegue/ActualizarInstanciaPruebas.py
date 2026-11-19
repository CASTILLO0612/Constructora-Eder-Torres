import importlib.util
from pathlib import Path
import tarfile
import unittest

especificacion = importlib.util.spec_from_file_location("actualizar_instancia",
    Path(__file__).resolve().parents[2] / "Tools/Despliegue/ActualizarInstancia.py")
actualizar = importlib.util.module_from_spec(especificacion)
especificacion.loader.exec_module(actualizar)


class Paquete:
    def __init__(self, adicionales=()):
        self.miembros = [tarfile.TarInfo(nombre) for nombre in (
            "Api/ConstructoraEderTorres.Presentacion.dll", "Web/index.html", "Web/sw.js", "manifiesto.json")]
        self.miembros.extend(adicionales)

    def getmembers(self):
        return self.miembros


class ActualizarInstanciaPruebas(unittest.TestCase):
    def test_admite_un_paquete_acotado_de_la_aplicacion(self):
        paquete = Paquete([tarfile.TarInfo("Operacion/migracion.sql")])
        self.assertEqual(5, len(actualizar.miembros_seguros(paquete)))

    def test_rechaza_rutas_fuera_de_los_componentes_permitidos(self):
        for nombre in ("../fuera", "/etc/entorno", "Web/../../etc/entorno", "C:/Windows/archivo", "Web\\..\\fuera", "OtroProyecto/api.dll"):
            with self.subTest(nombre=nombre), self.assertRaises(ValueError):
                actualizar.miembros_seguros(Paquete([tarfile.TarInfo(nombre)]))

    def test_rechaza_enlaces_y_duplicados_normalizados(self):
        enlace = tarfile.TarInfo("Web/enlace")
        enlace.type = tarfile.SYMTYPE
        enlace.linkname = "../../etc/entorno"
        for miembro in (enlace, tarfile.TarInfo("./Web/index.html")):
            with self.assertRaises(ValueError):
                actualizar.miembros_seguros(Paquete([miembro]))

    def test_rechaza_configuracion_privada_y_paquetes_incompletos(self):
        for nombre in ("Api/sesiones.pfx", "Api/.env.local", "Api/secrets.json", "Api/appsettings.Development.json"):
            with self.subTest(nombre=nombre), self.assertRaises(ValueError):
                actualizar.miembros_seguros(Paquete([tarfile.TarInfo(nombre)]))
        paquete = Paquete()
        paquete.miembros.pop()
        with self.assertRaises(ValueError):
            actualizar.miembros_seguros(paquete)

    def test_rechaza_tamanos_excesivos(self):
        archivo = tarfile.TarInfo("Web/grande")
        archivo.size = 513 * 1024 * 1024
        with self.assertRaises(ValueError):
            actualizar.miembros_seguros(Paquete([archivo]))


if __name__ == "__main__":
    unittest.main()
