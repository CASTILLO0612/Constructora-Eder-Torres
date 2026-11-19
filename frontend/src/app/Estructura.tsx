import { Suspense, useEffect, useState } from "react";
import { Link, NavLink, Outlet, useLocation } from "react-router-dom";
import {
    Building2,
    Users,
    Clock,
    ClipboardList,
    HardHat,
    Wrench,
    Shield,
    History,
    ChartColumn,
    RefreshCw,
    House,
    LogOut,
    Menu,
    X,
    Settings2,
    WifiOff,
} from "lucide-react";
import { useSesion } from "./Sesion";
import { useCola, MotorSincronizacion } from "../hooks/usoCola";
import { etiqueta } from "./formatos";
import { useAviso } from "./Avisos";
import { mensajeError } from "../api/cliente";
import { permisos as p } from "../api/permisos";
import { useQueryClient } from "@tanstack/react-query";
import { Boton, Cargando } from "../components/Interfaz";

export const navegacion = [
    { ruta: "/", nombre: "Resumen", icono: House, permisos: [] },
    {
        ruta: "/obras",
        nombre: "Obras",
        icono: Building2,
        permisos: [p.consultarObras, p.gestionarObras],
    },
    {
        ruta: "/personal",
        nombre: "Personal",
        icono: Users,
        permisos: [p.consultarTrabajadores, p.gestionarTrabajadores],
    },
    {
        ruta: "/asistencia",
        nombre: "Asistencia",
        icono: Clock,
        permisos: [
            p.registrarAsistencia,
            p.consultarAsistenciaPropia,
            p.consultarConsolidadoAsistencia,
        ],
    },
    {
        ruta: "/seguimiento",
        nombre: "Seguimiento",
        icono: ChartColumn,
        permisos: [
            p.gestionarActividades,
            p.registrarAvances,
            p.registrarEvidencias,
            p.registrarIncidenciasObra,
        ],
    },
    {
        ruta: "/actas",
        nombre: "Actas semanales",
        icono: ClipboardList,
        permisos: [p.elaborarActas, p.aprobarActas],
    },
    {
        ruta: "/epp",
        nombre: "Equipo de protección",
        icono: HardHat,
        permisos: [
            p.gestionarInventarioEpp,
            p.registrarEntregasEpp,
            p.gestionarReposicionesEpp,
            p.consultarHistorialEpp,
        ],
    },
    {
        ruta: "/herramientas",
        nombre: "Herramientas",
        icono: Wrench,
        permisos: [
            p.gestionarInventarioHerramientas,
            p.gestionarAsignacionesHerramientas,
            p.registrarIncidenciasHerramientas,
        ],
    },
    {
        ruta: "/reportes",
        nombre: "Reportes",
        icono: ChartColumn,
        permisos: [p.consultarConsolidadoAsistencia, p.consultarReportesEpp],
    },
    {
        ruta: "/accesos",
        nombre: "Usuarios y accesos",
        icono: Shield,
        permisos: [p.gestionarUsuarios, p.gestionarRoles],
    },
    {
        ruta: "/auditoria",
        nombre: "Auditoría",
        icono: History,
        permisos: [p.consultarAuditoria],
    },
    {
        ruta: "/pendientes",
        nombre: "Sincronización",
        icono: RefreshCw,
        permisos: [p.operarSincronizacion],
    },
    { ruta: "/ajustes", nombre: "Ajustes", icono: Settings2, permisos: [] },
];
export function Estructura() {
    const { perfil, puede, salir, sinVerificar, verificando, actualizar } =
        useSesion();
    const [menu, setMenu] = useState(
        () => window.matchMedia("(min-width: 701px)").matches,
    );
    const [enLinea, setEnLinea] = useState(navigator.onLine);
    const { pendientes } = useCola();
    const aviso = useAviso();
    const ubicacion = useLocation();
    const consultas = useQueryClient();
    const enlaces = navegacion.filter(
        (n) => !n.permisos.length || puede(...n.permisos),
    );
    useEffect(() => {
        const movil = window.matchMedia("(max-width: 700px)");
        const desbordamientoAnterior = document.body.style.overflow;
        const ajustar = () => {
            document.body.style.overflow =
                menu && movil.matches ? "hidden" : desbordamientoAnterior;
        };
        const cerrarConTeclado = (evento: KeyboardEvent) => {
            if (evento.key === "Escape") setMenu(false);
        };
        ajustar();
        movil.addEventListener("change", ajustar);
        window.addEventListener("keydown", cerrarConTeclado);
        return () => {
            document.body.style.overflow = desbordamientoAnterior;
            movil.removeEventListener("change", ajustar);
            window.removeEventListener("keydown", cerrarConTeclado);
        };
    }, [menu]);
    useEffect(() => {
        if (window.innerWidth <= 700) setMenu(false);
        document.getElementById("contenido")?.focus();
        window.scrollTo(0, 0);
    }, [ubicacion.pathname]);
    useEffect(() => {
        const actualizar = () => setEnLinea(navigator.onLine);
        const cambiarDatos = () => {
            void consultas.invalidateQueries({ queryKey: ["datos"] });
        };
        window.addEventListener("online", actualizar);
        window.addEventListener("offline", actualizar);
        window.addEventListener("cet:datos-cambiados", cambiarDatos);
        return () => {
            window.removeEventListener("online", actualizar);
            window.removeEventListener("offline", actualizar);
            window.removeEventListener("cet:datos-cambiados", cambiarDatos);
        };
    }, [consultas]);
    const cerrarSesion = async () => {
        try {
            await salir();
        } catch (e) {
            aviso(mensajeError(e));
        }
    };
    return (
        <div className={`aplicacion ${menu ? "menu-visible" : "menu-oculto"}`}>
            <a className="saltar-contenido" href="#contenido">
                Ir al contenido
            </a>
            <MotorSincronizacion />
            {menu && (
                <button
                    className="fondo-menu"
                    aria-label="Cerrar menú"
                    onClick={() => setMenu(false)}
                />
            )}
            <aside
                className={`barra-lateral ${menu ? "menu-abierto" : ""}`}
                inert={!menu}
                id="navegacion-principal"
                aria-label="Navegación principal"
            >
                <Link to="/" className="marca">
                    <span>
                        <strong>EDER TORRES</strong>
                        <small>CONSTRUCTORA</small>
                    </span>
                </Link>
                <button
                    className="cerrar-menu boton-icono"
                    aria-label="Cerrar menú"
                    onClick={() => setMenu(false)}
                >
                    <X />
                </button>
                <p className="etiqueta-menu">ESPACIO DE TRABAJO</p>
                <nav>
                    {enlaces.map((n) => (
                        <NavLink
                            key={n.ruta}
                            to={n.ruta}
                            end={n.ruta === "/"}
                            className={({ isActive }) =>
                                `enlace-menu ${isActive ? "seleccionado" : ""}`
                            }
                        >
                            <n.icono size={19} strokeWidth={1.7} />
                            <span>{n.nombre}</span>
                            {n.ruta === "/pendientes" &&
                                pendientes.length > 0 && (
                                    <b className="contador">
                                        {pendientes.length}
                                    </b>
                                )}
                        </NavLink>
                    ))}
                </nav>
                <div className="pie-lateral">
                    <div className="perfil">
                        <span className="avatar">
                            {perfil?.nombreUsuario.slice(0, 2).toUpperCase()}
                        </span>
                        <div>
                            <strong title={perfil?.nombreUsuario}>
                                {perfil?.nombreUsuario}
                            </strong>
                            <small
                                title={perfil?.roles.map(etiqueta).join(" · ")}
                            >
                                {perfil?.roles.map(etiqueta).join(" · ")}
                            </small>
                        </div>
                        <button
                            className="boton-icono"
                            aria-label="Cerrar sesión"
                            title="Cerrar sesión"
                            onClick={cerrarSesion}
                        >
                            <LogOut size={18} />
                        </button>
                    </div>
                </div>
            </aside>
            <div className="zona-trabajo">
                <header className="barra-superior barra-superior-simple">
                    <button
                        className="abrir-menu boton-icono"
                        aria-label="Alternar menú principal"
                        aria-controls="navegacion-principal"
                        aria-expanded={menu}
                        onClick={() => setMenu((abierto) => !abierto)}
                    >
                        <Menu size={24} />
                    </button>
                </header>
                {(!enLinea || sinVerificar) && (
                    <div className="banda-offline">
                        <WifiOff size={16} />
                        <span>
                            {sinVerificar
                                ? "Conexión pendiente de verificar. Tus capturas se conservan hasta poder enviarlas."
                                : "Trabajas sin conexión. Las capturas compatibles se conservarán en este dispositivo."}
                        </span>
                        <Link to="/pendientes">Ver pendientes</Link>
                        {sinVerificar && (
                            <Boton
                                variante="secundario"
                                ocupado={verificando}
                                onClick={() => void actualizar()}
                            >
                                <RefreshCw size={15} />
                                Verificar conexión
                            </Boton>
                        )}
                    </div>
                )}
                <main id="contenido" tabIndex={-1}>
                    <Suspense fallback={<Cargando variante="pagina" />}>
                        <Outlet />
                    </Suspense>
                </main>
            </div>
            <nav className="navegacion-movil" aria-label="Accesos rápidos">
                {enlaces
                    .filter((n) =>
                        ["/", "/asistencia", "/obras", "/pendientes"].includes(
                            n.ruta,
                        ),
                    )
                    .slice(0, 4)
                    .map((n) => (
                        <NavLink key={n.ruta} to={n.ruta} end={n.ruta === "/"}>
                            <n.icono size={21} />
                            <span>
                                {n.nombre === "Sincronización"
                                    ? "Pendientes"
                                    : n.nombre}
                            </span>
                        </NavLink>
                    ))}
                <NavLink to="/ajustes">
                    <Settings2 size={21} />
                    <span>Ajustes</span>
                </NavLink>
            </nav>
        </div>
    );
}
