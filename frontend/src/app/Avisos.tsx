import { createContext, useContext, useState, type ReactNode } from "react";
import { CircleCheck, X } from "lucide-react";
const contexto = createContext<(texto: string) => void>(() => undefined);
export const useAviso = () => useContext(contexto);
export function ProveedorAvisos({ children }: { children: ReactNode }) {
    const [avisos, setAvisos] = useState<{ id: number; texto: string }[]>([]);
    const avisar = (texto: string) => {
        const id = Date.now() + Math.random();
        setAvisos((a) => [...a, { id, texto }]);
        window.setTimeout(
            () => setAvisos((a) => a.filter((x) => x.id !== id)),
            7000,
        );
    };
    return (
        <contexto.Provider value={avisar}>
            {children}
            <div className="avisos" aria-live="polite">
                {avisos.map((a) => (
                    <div className="aviso" key={a.id}>
                        <CircleCheck size={20} />
                        <span>{a.texto}</span>
                        <button
                            aria-label="Cerrar aviso"
                            onClick={() =>
                                setAvisos((x) => x.filter((v) => v.id !== a.id))
                            }
                        >
                            <X size={16} />
                        </button>
                    </div>
                ))}
            </div>
        </contexto.Provider>
    );
}
