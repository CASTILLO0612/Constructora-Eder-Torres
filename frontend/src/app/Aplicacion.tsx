import {
    Component,
    lazy,
    Suspense,
    type ErrorInfo,
    type ReactNode,
} from "react";
import { Navigate, Outlet, Route, Routes, useLocation } from "react-router-dom";
import { useActualizacion } from "../hooks/usoActualizacion";
import { useSesion } from "./Sesion";
import { Estructura } from "./Estructura";
import { Boton, Cargando, Vacio } from "../components/Interfaz";
import { permisos as p } from "../api/permisos";

const Ingreso = lazy(() => import("../features/Seguridad/PaginaIngreso"));
const Ajustes = lazy(() => import("../features/Ajustes/PaginaAjustes"));
const Inicio = lazy(() => import("../features/Inicio/PaginaInicio"));
const Modulo = lazy(() => import("../features/Modulos/PaginaModulo"));
const Historial = lazy(
    () => import("../features/Asistencia/PaginaHistorialPropio"),
);
const Asistida = lazy(() => import("../features/Asistencia/PaginaAsistida"));
const Pendientes = lazy(
    () => import("../features/Sincronizacion/PaginaPendientes"),
);
function Protegido() {
    const { perfil, cargando } = useSesion();
    const lugar = useLocation();
    if (cargando)
        return <Cargando variante="pagina" texto="Verificando tu sesión…" />;
    return perfil ? (
        <Outlet />
    ) : (
        <Navigate
            to="/ingresar"
            replace
            state={{ desde: lugar.pathname + lugar.search }}
        />
    );
}
function Asistencia() {
    const { puede } = useSesion();
    return puede(p.consultarAsistenciaPropia) &&
        !puede(p.registrarAsistencia, p.consultarConsolidadoAsistencia) ? (
        <Historial />
    ) : (
        <Modulo nombre="asistencia" />
    );
}
function AsistidaAutorizada() {
    const { puede } = useSesion();
    return puede(p.registrarAsistencia) ? (
        <Asistida />
    ) : (
        <Vacio
            titulo="Acceso restringido"
            detalle="Tu cuenta permite únicamente la asistencia propia."
        />
    );
}
function ActualizacionPwa() {
    const { disponible, ocultar, actualizar } = useActualizacion();
    return disponible ? (
        <div className="aviso-actualizacion" role="status">
            <span>
                Hay una nueva versión disponible. Guarda los formularios
                abiertos antes de actualizar.
            </span>
            <Boton variante="secundario" onClick={ocultar}>
                Más tarde
            </Boton>
            <Boton onClick={actualizar}>Actualizar</Boton>
        </div>
    ) : null;
}
export class LimiteErrores extends Component<
    { children: ReactNode },
    { error: boolean }
> {
    state = { error: false };
    static getDerivedStateFromError() {
        return { error: true };
    }
    componentDidCatch(_error: Error, _info: ErrorInfo) {
        /* No enviar datos personales a consolas o terceros. */
    }
    render() {
        return this.state.error ? (
            <div className="fallo-aplicacion">
                <h1>No se pudo mostrar esta vista</h1>
                <p>
                    Tus capturas guardadas se conservan. Vuelve a cargar para
                    recuperar el espacio de trabajo.
                </p>
                <Boton onClick={() => location.reload()}>Volver a cargar</Boton>
            </div>
        ) : (
            this.props.children
        );
    }
}
export default function Aplicacion() {
    return (
        <LimiteErrores>
            <Suspense fallback={<Cargando variante="pagina" />}>
                <Routes>
                    <Route path="/ingresar" element={<Ingreso />} />
                    <Route element={<Protegido />}>
                        <Route element={<Estructura />}>
                            <Route index element={<Inicio />} />
                            <Route path="ajustes" element={<Ajustes />} />
                            <Route path="asistencia" element={<Asistencia />} />
                            <Route
                                path="asistencia/marcar"
                                element={<AsistidaAutorizada />}
                            />
                            <Route path="pendientes" element={<Pendientes />} />
                            <Route path=":modulo" element={<Modulo />} />
                            <Route
                                path="*"
                                element={
                                    <Vacio titulo="Página no encontrada" />
                                }
                            />
                        </Route>
                    </Route>
                </Routes>
            </Suspense>
            <ActualizacionPwa />
        </LimiteErrores>
    );
}
