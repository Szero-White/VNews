import { defineConfig } from '@playwright/test';

// Sub-category menu + category page specs. They create parent/child categories and articles through the CMS, so they run against their
// OWN server (port 5397) and database (VNewsE2ESub: the allowed "VNewsE2E" prefix, never VNewsDb) instead of the shared E2E database.
//   npx playwright test -c playwright.subcategories.config.ts
// Prerequisite: the built output, like the main config (E2E_BUILD_CONFIG=Verify to use bin/Verify).
const port = 5397;
const buildConfig = process.env.E2E_BUILD_CONFIG ?? 'Debug';

export default defineConfig({
  testDir: '.',
  testMatch: /category-submenu\.spec\.ts/,
  timeout: 30000,
  workers: 1,
  reporter: [['list']],
  use: { baseURL: `http://localhost:${port}`, viewport: { width: 1440, height: 900 } },
  webServer: {
    command: `dotnet bin/${buildConfig}/net10.0/VNews.Web.dll --urls http://localhost:${port}`,
    cwd: '../src/VNews.Web',
    url: `http://localhost:${port}/`,
    reuseExistingServer: false,
    timeout: 180000,
    env: {
      ASPNETCORE_ENVIRONMENT: 'Development',
      DemoAuth__Enabled: 'true',
      ConnectionStrings__DefaultConnection: `Server=(localdb)\\MSSQLLocalDB;Database=VNewsE2ESub;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True`,
      RateLimiting__Auth__PermitLimit: '1000',
    },
  },
});
