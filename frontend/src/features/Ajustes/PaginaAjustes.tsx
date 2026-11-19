import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import {
    Sun,
    Moon,
    Monitor,
    UserRound,
    ShieldCheck,
    Wifi,
    WifiOff,
    RefreshCw,
    LogOut,
    ArrowUpRight,
    Check,
} from "lucide-react";
import { useSesion } from "../../app/Sesion";
import { useTema } from "../../app/Tema";
import { useCola } from "../../hooks/usoCola";
import { etiqueta, fecha } from "../../app/formatos";
import { Boton, Encabezado, ErrorVisible } from "../../components/Interfaz";
import { permisos as p } from "../../api/permisos";

const apariencias = [
    {
        valor: "claro",
        titulo: "Claro",
        descripcion: "Superficies blancas y texto oscuro.",
        Icono: Sun,
    },
    {
        valor: "oscuro",
        titulo: "Oscuro",
        descripcion: "Superficies oscuras y texto claro.",
        Icono: Moon,
    },
    {
        valor: "sistema",
        titulo: "Sistema",
        descripcion: "Sigue la apariencia de tu dispositivo.",
        Icono: Monitor,
    },
] as const;

export default function PaginaAjustes() {
    const { perfil, sinVerificar, verificando, actualizar, salir, puede } =
        useSesion();
    const { tema, cambiar } = useTema();
    const { pendientes } = useCola();
    const [enLinea, setEnLinea] = useState(navigator.onLine);
    const [error, setError] = useState<unknown>();
    const [cerrando, setCerrando] = useState(false);
    useEffect(() => {
        const red = () => setEnLinea(navigator.onLine);
        window.addEventListener("online", red);
        window.addEventListener("offline", red);
        return () => {
            window.removeEventListener("online", red);
            window.removeEventListener("offline", red);
        };
    }, []);
    const cerrar = async () => {
        setCerrando(true);
        try {
            await salir();
        } catch (e) {
            setError(e);
        } finally {
            setCerrando(false);
        }
    };
    return (
        <>
            <Encabezado titulo="Ajustes" />
            <div className="rejilla-ajustes">
                <section className="panel ajustes-apariencia">
                    <div className="titulo-panel">
                        <div>
                            <h2>Apariencia</h2>
                            <p className="detalle-panel">
                                Elige cómo quieres ver la aplicación.
                            </p>
                        </div>
                    </div>
                    <fieldset className="opciones-apariencia">
                        <legend className="visualmente-oculto">
                            Tema de la aplicación
                        </legend>
                        {apariencias.map(
                            ({ valor, titulo, descripcion, Icono }) => (
                                <label
                                    key={valor}
                                    className={`opcion-apariencia ${tema === valor ? "seleccionada" : ""}`}
                                >
                                    <input
                                        className="radio-apariencia"
                                        type="radio"
                                        name="apariencia"
                                        value={valor}
                                        checked={tema === valor}
                                        onChange={() => cambiar(valor)}
                                    />
                                    <div
                                        className={`muestra-apariencia muestra-${valor}`}
                                        aria-hidden="true"
                                    >
                                        <div />
                                        <div>
                                            <i />
                                            <i />
                                            <i />
                                        </div>
                                    </div>
                                    <span className="nombre-apariencia">
                                        <Icono size={18} />
                                        <strong>{titulo}</strong>
                                        <span
                                            className="indicador-apariencia"
                                            aria-hidden="true"
                                        >
                                            {tema === valor && (
                                                <Check
                                                    size={14}
                                                    strokeWidth={2.5}
                                                />
                                            )}
                                        </span>
                                    </span>
                                    <small>{descripcion}</small>
                                </label>
                            ),
                        )}
                    </fieldset>
                </section>
                <section className="panel">
                    <div className="titulo-panel">
                        <div>
                            <h2>Tu cuenta</h2>
                            <p className="detalle-panel">
                                Información de tu sesión actual.
                            </p>
                        </div>
                        <UserRound size={21} />
                    </div>
                    <dl className="datos-ajustes">
                        <div>
                            <dt>Usuario</dt>
                            <dd>{perfil?.nombreUsuario}</dd>
                        </div>
                        <div>
                            <dt>Perfil autorizado</dt>
                            <dd>{perfil?.roles.map(etiqueta).join(" · ")}</dd>
                        </div>
                        <div>
                            <dt>Inicio de sesión</dt>
                            <dd>{fecha(perfil?.autenticadoEnUtc, true)}</dd>
                        </div>
                    </dl>
                </section>
                <section className="panel">
                    <div className="titulo-panel">
                        <div>
                            <h2>Conexión y sincronización</h2>
                            <p className="detalle-panel">
                                Comprueba el acceso antes de enviar tus
                                capturas.
                            </p>
                        </div>
                        {enLinea ? <Wifi size={21} /> : <WifiOff size={21} />}
                    </div>
                    <dl className="datos-ajustes">
                        <div>
                            <dt>Red del dispositivo</dt>
                            <dd>{enLinea ? "Disponible" : "Sin conexión"}</dd>
                        </div>
                        <div>
                            <dt>Estado de tu sesión</dt>
                            <dd>
                                {sinVerificar
                                    ? "Pendiente de verificar"
                                    : "Verificada"}
                            </dd>
                        </div>
                        {puede(p.operarSincronizacion) && (
                            <div>
                                <dt>Capturas pendientes en este dispositivo</dt>
                                <dd>{pendientes.length}</dd>
                            </div>
                        )}
                    </dl>
                    <div className="acciones-ajustes">
                        <Boton
                            variante="secundario"
                            ocupado={verificando}
                            onClick={() => void actualizar()}
                        >
                            <RefreshCw size={17} />
                            Verificar conexión
                        </Boton>
                        {puede(p.operarSincronizacion) && (
                            <Link className="enlace-accion" to="/pendientes">
                                Ver pendientes
                                <ArrowUpRight size={17} />
                            </Link>
                        )}
                    </div>
                </section>
                <section className="panel">
                    <div className="titulo-panel">
                        <div>
                            <h2>Seguridad de la sesión</h2>
                            <p className="detalle-panel">
                                La sesión se cierra por inactividad o al
                                alcanzar su duración máxima.
                            </p>
                        </div>
                        <ShieldCheck size={21} />
                    </div>
                    <div className="acciones-ajustes">
                        <p>
                            Las capturas pendientes se conservan cuando cierras
                            sesión.
                        </p>
                        <Boton
                            variante="secundario"
                            ocupado={cerrando}
                            onClick={() => void cerrar()}
                        >
                            <LogOut size={17} />
                            Cerrar sesión
                        </Boton>
                    </div>
                    {error !== undefined && <ErrorVisible error={error} />}
                </section>
            </div>
        </>
    );
}
