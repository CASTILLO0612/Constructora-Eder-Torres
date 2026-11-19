import { useEffect, useRef, useState } from "react";
import { Camera, Upload, X, FileText, LocateFixed } from "lucide-react";
import { Boton, ErrorVisible } from "./Interfaz";
import { obtenerGps } from "../features/Asistencia/geocerca";
import { configuracion } from "../app/configuracion";
import { horaLocal } from "../app/formatos";
import type { Fila } from "../features/Modulos/catalogo";

const aBase64 = (archivo: Blob) =>
    new Promise<string>((resolver, rechazar) => {
        const lector = new FileReader();
        lector.onload = () => resolver(String(lector.result).split(",")[1]);
        lector.onerror = () =>
            rechazar(new Error("No se pudo leer el archivo."));
        lector.readAsDataURL(archivo);
    });
export function CapturaArchivo({
    datos,
    cambiar,
}: {
    datos: Fila;
    cambiar: (campos: Fila) => void;
}) {
    const video = useRef<HTMLVideoElement>(null);
    const flujo = useRef<MediaStream | null>(null);
    const montado = useRef(true);
    const [camara, setCamara] = useState(false);
    const [ocupado, setOcupado] = useState(false);
    const [error, setError] = useState<unknown>();
    const [vista, setVista] = useState("");
    useEffect(() => {
        montado.current = true;
        return () => {
            montado.current = false;
            flujo.current?.getTracks().forEach((p) => p.stop());
        };
    }, []);
    const parar = () => {
        flujo.current?.getTracks().forEach((p) => p.stop());
        flujo.current = null;
        setCamara(false);
    };
    const guardar = async (
        archivo: File | Blob,
        nombre: string,
        tomadaAhora: boolean,
    ) => {
        if (archivo.size > configuracion.archivoMaximoBytes)
            throw new Error(
                `El archivo supera el límite de ${configuracion.archivoMaximoBytes / 1024 / 1024} MiB.`,
            );
        const contenido = await aBase64(archivo);
        const campos: Fila = {
            contenidoArchivo: contenido,
            nombreArchivo: nombre,
        };
        if (tomadaAhora) {
            const gps = await obtenerGps();
            campos.capturadaEn = horaLocal(new Date());
            campos.latitud = gps.latitud;
            campos.longitud = gps.longitud;
        }
        cambiar(campos);
        if (archivo.type.startsWith("image/"))
            setVista(`data:${archivo.type};base64,${contenido}`);
        else setVista("");
    };
    const abrir = async () => {
        setError(undefined);
        try {
            flujo.current?.getTracks().forEach((p) => p.stop());
            flujo.current = await navigator.mediaDevices.getUserMedia({
                video: { facingMode: { ideal: "environment" } },
                audio: false,
            });
            if (!montado.current) {
                flujo.current.getTracks().forEach((p) => p.stop());
                return;
            }
            setCamara(true);
            requestAnimationFrame(() => {
                if (video.current) video.current.srcObject = flujo.current;
            });
        } catch (e) {
            setError(e);
        }
    };
    const tomar = async () => {
        setOcupado(true);
        setError(undefined);
        try {
            const v = video.current;
            if (!v?.videoWidth)
                throw new Error("La cámara todavía no está lista.");
            const lienzo = document.createElement("canvas");
            lienzo.width = v.videoWidth;
            lienzo.height = v.videoHeight;
            lienzo.getContext("2d")!.drawImage(v, 0, 0);
            const imagen = await new Promise<Blob>((res, rej) =>
                lienzo.toBlob(
                    (b) =>
                        b
                            ? res(b)
                            : rej(new Error("No se pudo capturar la imagen.")),
                    "image/jpeg",
                    0.95,
                ),
            );
            await guardar(
                imagen,
                `evidencia-${new Date().toISOString().replace(/[:.]/g, "-")}.jpg`,
                true,
            );
            parar();
        } catch (e) {
            setError(e);
        } finally {
            setOcupado(false);
        }
    };
    return (
        <div className="captura-archivo">
            <div className="captura-acciones">
                {datos.tipoEvidencia === "FOTOGRAFIA" && (
                    <Boton type="button" variante="secundario" onClick={abrir}>
                        <Camera size={18} />
                        Usar cámara
                    </Boton>
                )}
                <label className="boton boton-secundario">
                    <Upload size={18} />
                    Seleccionar archivo
                    <input
                        type="file"
                        className="visualmente-oculto"
                        accept={
                            datos.tipoEvidencia === "FOTOGRAFIA"
                                ? ".jpg,.jpeg,.png,.webp"
                                : ".jpg,.jpeg,.png,.webp,.pdf,.txt"
                        }
                        onChange={async (e) => {
                            const f = e.target.files?.[0];
                            if (!f) return;
                            setOcupado(true);
                            setError(undefined);
                            try {
                                await guardar(f, f.name, false);
                            } catch (err) {
                                setError(err);
                            } finally {
                                setOcupado(false);
                            }
                        }}
                    />
                </label>
            </div>
            {camara && (
                <div className="camara">
                    <video ref={video} autoPlay playsInline muted />
                    <div>
                        <Boton type="button" ocupado={ocupado} onClick={tomar}>
                            <Camera size={18} />
                            Capturar y obtener ubicación
                        </Boton>
                        <Boton
                            type="button"
                            variante="secundario"
                            onClick={parar}
                        >
                            <X size={16} />
                            Cerrar cámara
                        </Boton>
                    </div>
                </div>
            )}
            {vista && (
                <img
                    className="vista-evidencia"
                    src={vista}
                    alt="Vista previa del archivo seleccionado"
                />
            )}
            {!!datos.nombreArchivo && (
                <p className="archivo-elegido">
                    <FileText size={16} />
                    {String(datos.nombreArchivo)}
                </p>
            )}
            {datos.tipoEvidencia === "FOTOGRAFIA" && (
                <p className="ayuda-campo">
                    <LocateFixed size={14} />
                    La cámara toma la hora y ubicación actuales. Para una
                    fotografía anterior, informa la fecha y coordenadas
                    originales en los campos siguientes.
                </p>
            )}
            {error !== undefined && <ErrorVisible error={error} />}
        </div>
    );
}
