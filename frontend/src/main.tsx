import React from "react";
import ReactDOM from "react-dom/client";
import { BrowserRouter } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ProveedorSesion } from "./app/Sesion";
import { ProveedorTema } from "./app/Tema";
import { ProveedorAvisos } from "./app/Avisos";
import Aplicacion from "./app/Aplicacion";
import "./styles/interfaz.css";
import "./styles/paneles.css";
import "./styles/cargas.css";

const consultas = new QueryClient({
    defaultOptions: {
        queries: {
            retry: false,
            refetchOnWindowFocus: false,
            gcTime: 10 * 60_000,
        },
    },
});
ReactDOM.createRoot(document.getElementById("root")!).render(
    <React.StrictMode>
        <QueryClientProvider client={consultas}>
            <BrowserRouter>
                <ProveedorTema>
                    <ProveedorSesion>
                        <ProveedorAvisos>
                            <Aplicacion />
                        </ProveedorAvisos>
                    </ProveedorSesion>
                </ProveedorTema>
            </BrowserRouter>
        </QueryClientProvider>
    </React.StrictMode>,
);
