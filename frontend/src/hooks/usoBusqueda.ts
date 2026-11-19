import { useEffect, useState } from "react";
import { limitesBusqueda } from "../api/busqueda";

export function normalizarBusqueda(valor: string) {
    return valor.trim().replace(/\s+/g, " ");
}

export function useBusqueda(valor: string, contexto: string) {
    const texto = normalizarBusqueda(valor);
    const incompleta = !!texto && texto.length < limitesBusqueda.longitudMinima;
    const demasiadasPalabras =
        texto.split(" ").length > limitesBusqueda.maximoPalabras;
    const valida =
        !incompleta &&
        !demasiadasPalabras &&
        texto.length <= limitesBusqueda.longitudMaxima;
    const [aplicada, setAplicada] = useState({ contexto, texto: "" });
    const consulta = aplicada.contexto === contexto ? aplicada.texto : "";
    const siguiente = valida ? texto : "";
    useEffect(() => {
        const espera = window.setTimeout(
            () => setAplicada({ contexto, texto: siguiente }),
            siguiente ? 350 : 0,
        );
        return () => window.clearTimeout(espera);
    }, [siguiente, contexto]);
    return {
        consulta,
        esperando: consulta !== siguiente,
        ayuda: incompleta
            ? `Escribe al menos ${limitesBusqueda.longitudMinima} caracteres para buscar.`
            : demasiadasPalabras
              ? `Usa hasta ${limitesBusqueda.maximoPalabras} palabras para buscar.`
              : "",
    };
}

export function coincideBusqueda(texto: string, buscar: string) {
    const normalizar = (valor: string) =>
        valor
            .normalize("NFD")
            .replace(/[\u0300-\u036f]/g, "")
            .toLocaleLowerCase();
    const contenido = normalizar(texto);
    return normalizar(normalizarBusqueda(buscar))
        .split(" ")
        .every((palabra) => contenido.includes(palabra));
}
