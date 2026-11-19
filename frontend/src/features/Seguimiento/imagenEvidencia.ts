// El endpoint protegido entrega un archivo binario. Solo se visualizan imágenes
// raster admitidas, identificadas por contenido y sin ejecutar documentos activos.
export async function prepararImagenEvidencia(contenido: Blob): Promise<Blob> {
    const cabecera = await new Promise<Uint8Array>((resolver, rechazar) => {
        const lector = new FileReader();
        lector.onload = () =>
            resolver(new Uint8Array(lector.result as ArrayBuffer));
        lector.onerror = () =>
            rechazar(new Error("No se pudo leer la imagen."));
        lector.readAsArrayBuffer(contenido.slice(0, 12));
    });
    const empiezaCon = (firma: number[], inicio = 0) =>
        firma.every((valor, indice) => cabecera[inicio + indice] === valor);
    const tipo = empiezaCon([137, 80, 78, 71, 13, 10, 26, 10])
        ? "image/png"
        : empiezaCon([255, 216, 255])
          ? "image/jpeg"
          : empiezaCon([82, 73, 70, 70]) && empiezaCon([87, 69, 66, 80], 8)
            ? "image/webp"
            : null;
    if (!tipo)
        throw new Error(
            "La vista previa admite imágenes JPEG, PNG y WebP. Puedes descargar este archivo para consultarlo.",
        );
    return new Blob([contenido], { type: tipo });
}
