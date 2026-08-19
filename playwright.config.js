const { defineConfig, devices } = require("@playwright/test");

module.exports = defineConfig({
    testDir: "./tests/e2e",
    outputDir: "test-results/playwright",
    reporter: [
        ["list"],
        ["html", { outputFolder: "playwright-report", open: "never" }],
    ],
    timeout: 30_000,
    expect: { timeout: 5_000 },
    webServer: {
        command:
            "dotnet run --project src/MarineInsight.Web/MarineInsight.Web.csproj --configuration Release --no-build --no-launch-profile",
        url: "http://127.0.0.1:5180/health/live",
        reuseExistingServer: true,
        timeout: 120_000,
        env: {
            ASPNETCORE_URLS: "http://127.0.0.1:5180",
            ASPNETCORE_ENVIRONMENT: "Development",
            TideProviders__WorldTides__Enabled: "false",
            AI__Enabled: "false",
        },
    },
    use: {
        baseURL:
            process.env.MARINE_INSIGHT_E2E_BASE_URL || "http://127.0.0.1:5180",
        launchOptions: process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH
            ? {
                  executablePath:
                      process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH,
              }
            : {},
        trace: "retain-on-failure",
        screenshot: "only-on-failure",
    },
    projects: [
        {
            name: "desktop-chromium",
            use: {
                ...devices["Desktop Chrome"],
                viewport: { width: 1440, height: 900 },
            },
        },
        {
            name: "mobile-chromium",
            use: {
                ...devices["Desktop Chrome"],
                viewport: { width: 360, height: 800 },
                isMobile: true,
            },
        },
    ],
});
