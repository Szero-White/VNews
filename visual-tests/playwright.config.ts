import { defineConfig } from '@playwright/test';

// By default the specs run against their OWN server on port 5399 backed by a dedicated LocalDB database
// (VNewsE2E, created and seeded on first start), so automated runs never touch the developer's VNewsDb:
// opening an article increments ViewCount, CMS sign-ins write audit rows, etc.
//
// To deliberately run against an already running server (e.g. the dev site), set E2E_BASE_URL:
//   E2E_BASE_URL=http://localhost:5299 npx playwright test
// Prerequisite for the isolated server: `dotnet build VNews.slnx` (it starts the built Debug output).
const externalBaseUrl = process.env.E2E_BASE_URL;
const port = 5399;
const e2eDatabase = 'VNewsE2E'; // never "VNewsDb"

export default defineConfig({
  testDir: '.',
  timeout: 30000,
  reporter: [['list']],
  use: {
    baseURL: externalBaseUrl ?? `http://localhost:${port}`,
    viewport: { width: 1440, height: 900 },
  },
  webServer: externalBaseUrl
    ? undefined
    : {
        command: `dotnet bin/Debug/net10.0/VNews.Web.dll --urls http://localhost:${port}`,
        cwd: '../src/VNews.Web',
        url: `http://localhost:${port}/`,
        reuseExistingServer: false,
        timeout: 180000,
        env: {
          ASPNETCORE_ENVIRONMENT: 'Development',
          ConnectionStrings__DefaultConnection: `Server=(localdb)\\MSSQLLocalDB;Database=${e2eDatabase};Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True`,
          // Many specs sign in from one IP; keep the limiter from tripping them (the real limit is covered by .NET tests).
          RateLimiting__Auth__PermitLimit: '1000',
        },
      },
});
