import { defineConfig, loadEnv } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig(({ mode }) => {
    const entorno = loadEnv(mode, process.cwd(), "");
    const destino = entorno.API_DESTINO || "http://localhost:5180";
    return {
        plugins: [react()],
        server: {
            port: 5173,
            strictPort: true,
            proxy: {
                "/api": { target: destino, changeOrigin: true },
                "/salud": { target: destino, changeOrigin: true },
            },
        },
        build: {
            sourcemap: false,
            rollupOptions: {
                output: {
                    manualChunks: {
                        react: ["react", "react-dom", "react-router-dom"],
                        consultas: ["@tanstack/react-query"],
                    },
                },
            },
        },
    };
});
