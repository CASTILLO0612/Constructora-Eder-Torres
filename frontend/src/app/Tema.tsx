import {
    createContext,
    useContext,
    useEffect,
    useState,
    type ReactNode,
} from "react";
type Tema = "claro" | "oscuro" | "sistema";
const contexto = createContext<{ tema: Tema; cambiar: (tema: Tema) => void }>({
    tema: "claro",
    cambiar: () => undefined,
});
export const useTema = () => useContext(contexto);
export function ProveedorTema({ children }: { children: ReactNode }) {
    const [tema, cambiar] = useState<Tema>(() => {
        let guardado: string | null = null;
        try {
            guardado = localStorage.getItem("cet-tema");
        } catch {
            /* Usar el tema claro si el navegador bloquea preferencias. */
        }
        return guardado === "oscuro" || guardado === "sistema"
            ? guardado
            : "claro";
    });
    useEffect(() => {
        const medio = matchMedia("(prefers-color-scheme: dark)");
        const aplicar = () =>
            (document.documentElement.dataset.tema =
                tema === "sistema"
                    ? medio.matches
                        ? "oscuro"
                        : "claro"
                    : tema);
        aplicar();
        try {
            localStorage.setItem("cet-tema", tema);
        } catch {
            /* El cambio visual sigue funcionando en memoria. */
        }
        medio.addEventListener("change", aplicar);
        return () => medio.removeEventListener("change", aplicar);
    }, [tema]);
    return (
        <contexto.Provider value={{ tema, cambiar }}>
            {children}
        </contexto.Provider>
    );
}
