import { useEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { MoreHorizontal, ArrowUpRight } from "lucide-react";

export function MenuAcciones({
    opciones,
}: {
    opciones: { titulo: string; ejecutar: () => void }[];
}) {
    const boton = useRef<HTMLButtonElement>(null);
    const menu = useRef<HTMLDivElement>(null);
    const [posicion, setPosicion] = useState<{
        superior: number;
        derecha: number;
    } | null>(null);
    useEffect(() => {
        if (!posicion) return;
        menu.current?.querySelector("button")?.focus();
        const fuera = (e: PointerEvent) => {
            if (
                !menu.current?.contains(e.target as Node) &&
                !boton.current?.contains(e.target as Node)
            )
                setPosicion(null);
        };
        const teclado = (e: KeyboardEvent) => {
            if (e.key === "Escape") {
                setPosicion(null);
                boton.current?.focus();
            }
            if (["ArrowDown", "ArrowUp"].includes(e.key)) {
                e.preventDefault();
                const botones = [...menu.current!.querySelectorAll("button")];
                const indice = botones.indexOf(
                    document.activeElement as HTMLButtonElement,
                );
                botones[
                    (indice +
                        (e.key === "ArrowDown" ? 1 : -1) +
                        botones.length) %
                        botones.length
                ]?.focus();
            }
        };
        const cerrar = () => setPosicion(null);
        document.addEventListener("pointerdown", fuera);
        document.addEventListener("keydown", teclado);
        window.addEventListener("resize", cerrar);
        return () => {
            document.removeEventListener("pointerdown", fuera);
            document.removeEventListener("keydown", teclado);
            window.removeEventListener("resize", cerrar);
        };
    }, [posicion]);
    return (
        <>
            <button
                ref={boton}
                className="boton-icono"
                aria-label="Más acciones"
                aria-haspopup="menu"
                aria-expanded={!!posicion}
                onClick={() => {
                    if (posicion) {
                        setPosicion(null);
                        return;
                    }
                    const r = boton.current!.getBoundingClientRect();
                    setPosicion({
                        superior: Math.max(
                            8,
                            Math.min(
                                r.bottom + 4,
                                window.innerHeight - opciones.length * 43 - 22,
                            ),
                        ),
                        derecha: Math.max(8, window.innerWidth - r.right),
                    });
                }}
            >
                <MoreHorizontal size={19} />
            </button>
            {posicion &&
                createPortal(
                    <div
                        ref={menu}
                        role="menu"
                        aria-label="Acciones del registro"
                        className="menu-flotante"
                        style={{
                            top: posicion.superior,
                            right: posicion.derecha,
                        }}
                    >
                        {opciones.map((o) => (
                            <button
                                role="menuitem"
                                key={o.titulo}
                                onClick={() => {
                                    setPosicion(null);
                                    o.ejecutar();
                                }}
                            >
                                {o.titulo}
                                <ArrowUpRight size={14} />
                            </button>
                        ))}
                    </div>,
                    document.body,
                )}
        </>
    );
}
