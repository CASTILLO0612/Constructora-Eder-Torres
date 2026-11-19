import { useState, type FormEvent } from "react";
import { Navigate, useLocation } from "react-router-dom";
import { ArrowRight, Eye, EyeOff, WifiOff } from "lucide-react";
import { useSesion } from "../../app/Sesion";
import { Boton, ErrorVisible } from "../../components/Interfaz";

export default function PaginaIngreso() {
    const { perfil, ingresar, motivo } = useSesion();
    const [usuario, setUsuario] = useState("");
    const [clave, setClave] = useState("");
    const [visible, setVisible] = useState(false);
    const [ocupado, setOcupado] = useState(false);
    const [error, setError] = useState<unknown>();
    const ubicacion = useLocation();
    if (perfil)
        return (
            <Navigate
                to={(ubicacion.state as { desde?: string })?.desde || "/"}
                replace
            />
        );
    const enviar = async (e: FormEvent) => {
        e.preventDefault();
        setError(undefined);
        setOcupado(true);
        try {
            await ingresar(usuario.trim(), clave);
            setClave("");
        } catch (err) {
            setError(err);
        } finally {
            setOcupado(false);
        }
    };
    return (
        <div className="pagina-ingreso">
            <div className="presentacion-ingreso">
                <h1 className="nombre-ingreso">Eder Torres</h1>
                <div className="lineas-arquitectura" aria-hidden="true">
                    <div />
                    <div />
                    <div />
                    <div />
                </div>
            </div>
            <div className="panel-ingreso">
                <div className="formulario-ingreso">
                    <h2>Iniciar sesión</h2>
                    {motivo && <div className="mensaje">{motivo}</div>}
                    {!navigator.onLine && (
                        <div className="mensaje">
                            <WifiOff size={18} />
                            Necesitas conexión para iniciar una nueva sesión.
                        </div>
                    )}
                    {error !== undefined && <ErrorVisible error={error} />}
                    <form onSubmit={enviar}>
                        <label className="campo">
                            Usuario
                            <input
                                autoComplete="username"
                                name="usuario"
                                value={usuario}
                                onChange={(e) => setUsuario(e.target.value)}
                                required
                                autoCapitalize="none"
                                spellCheck={false}
                                autoFocus
                            />
                        </label>
                        <label className="campo">
                            Contraseña
                            <div className="entrada-clave">
                                <input
                                    type={visible ? "text" : "password"}
                                    autoComplete="current-password"
                                    name="contrasena"
                                    value={clave}
                                    onChange={(e) => setClave(e.target.value)}
                                    required
                                />
                                <button
                                    type="button"
                                    aria-label={
                                        visible
                                            ? "Ocultar contraseña"
                                            : "Mostrar contraseña"
                                    }
                                    onClick={() => setVisible(!visible)}
                                >
                                    {visible ? (
                                        <EyeOff size={19} />
                                    ) : (
                                        <Eye size={19} />
                                    )}
                                </button>
                            </div>
                        </label>
                        <Boton
                            type="submit"
                            ocupado={ocupado}
                            className="ancho-completo"
                        >
                            Entrar a la aplicación
                            <ArrowRight size={18} />
                        </Boton>
                    </form>
                </div>
                <p className="pie-ingreso">Constructora Eder Torres</p>
            </div>
        </div>
    );
}
