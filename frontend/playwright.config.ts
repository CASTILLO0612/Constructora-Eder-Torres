import { defineConfig } from "@playwright/test";
export default defineConfig({
    testDir: "./tests/e2e",
    workers: 1,
    fullyParallel: false,
    timeout: 60_000,
    expect: { timeout: 15_000 },
    retries: 0,
    outputDir: "../artifacts/Frontend/navegador",
    reporter: [
        ["list"],
        [
            "html",
            {
                outputFolder: "../artifacts/Frontend/informe-playwright",
                open: "never",
            },
        ],
    ],
    use: {
        baseURL: process.env.CET_E2E_URL,
        channel: "msedge",
        headless: true,
        viewport: { width: 1440, height: 1000 },
        locale: "es-NI",
        timezoneId: "America/Managua",
        screenshot: "only-on-failure",
        trace: "retain-on-failure",
    },
});
