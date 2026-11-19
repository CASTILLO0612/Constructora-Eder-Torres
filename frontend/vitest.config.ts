import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";
export default defineConfig({
    plugins: [react()],
    test: {
        environment: "jsdom",
        include: ["tests/unitarias/**/*.test.{ts,tsx}"],
        setupFiles: ["./tests/unitarias/preparacion.ts"],
        restoreMocks: true,
    },
});
