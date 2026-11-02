# VNews — permanent instructions for Claude Code

Build a **working portfolio news platform**, not a static mockup. Communicate in Vietnamese. Work only within this project folder.

## Stack (do not substitute)
- .NET 10 LTS, C#, ASP.NET Core MVC + Razor, REST API controllers, EF Core 10 + LINQ, **SQL Server / SQL Server LocalDB**, ASP.NET Core Identity.
- Modular monolith: Web, Application, Domain, Infrastructure; tests separate. Server-rendered public UI. No Next.js/Node backend/SQLite/in-memory database as a replacement.
- Use minimal free dependencies. Avoid Docker, Redis, React, queues and paid services until MVP passes; these are optional enhancements.

## Implementation rules
1. Read `docs/SCOPE.md` and `docs/DESIGN.md` when implementing affected features; read `docs/PROGRESS.md` at the start of each session. Do NOT reread all docs or dump whole files unnecessarily.
2. Implement **one end-to-end milestone at a time**. Prefer a complete working vertical slice to many incomplete scaffolds. Don't create a long plan unless a major architectural decision is unresolved.
3. After edits, run targeted checks: `dotnet build` and relevant `dotnet test`; verify database interactions and HTTP behavior when tools exist. Fix failures before saying DONE. If an environment prerequisite is missing, mark BLOCKED and state the exact missing prerequisite; never invent test results.
4. Keep `docs/PROGRESS.md` short: DONE, CURRENT, NEXT, BLOCKERS, VERIFICATION (commands/results). Commit or create an explicit checkpoint after each verified milestone when possible.
5. Public content MUST come from SQL Server (EF Core). No fake runtime arrays or dummy admin buttons. Seed original Vietnamese demo articles idempotently. Exception (owner-approved): real headlines may be imported from the publisher's public RSS feeds (title, short summary, link back, date) with the source named on every article; never copy full article text.
6. Security: auth/roles for mutations, antiforgery on forms, input validation, parameterized SQL, safe upload, no secrets committed. Never run destructive data-reset or out-of-repo commands without asking.
7. Visual goal: resemble the editorial *layout and density* of a Vietnamese news portal like VnExpress; use independent **VNews** branding and original content/legally usable images. Do not copy proprietary logos, article text, images, or HTML/CSS (the RSS summary + link above is the only text taken; publisher images stay off unless `NewsImport:ShowSourceThumbnails` is enabled by the site owner).
8. To verify visual changes, compare real browser screenshots of desktop 1440px and mobile 390px with `references/homepage.png` when it exists. If no browser screenshot tool exists, report that visual verification is not performed.
9. In final responses: summarize changed paths, real verification results, blockers, next milestone in <=12 lines. Avoid verbose narration and repeatedly reprinting source files.

## Key commands and targets
- Build: `dotnet build VNews.sln` (if solution uses .slnx, use that exact name).
- Test: `dotnet test`.
- Acceptance criteria: `docs/SCOPE.md`; design: `docs/DESIGN.md`; session status: `docs/PROGRESS.md`.
- Startup guidance must be maintained in `RUN-WINDOWS.md` once solution exists.
