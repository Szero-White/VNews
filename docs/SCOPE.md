# VNews — functional scope and milestones

## M0 — runnable project skeleton
A .NET 10 solution with Web/MVC, Application, Domain, Infrastructure, Tests. SQL Server EF Core migrations and connection config without hardcoded secrets; layout, local assets, basic routes; `dotnet build` passes. Provide `RUN-WINDOWS.md` and explain SQL Server LocalDB prereqs. Don't report SQL connectivity if not tested.

## M1 — Public site, real SQL data
- Pages: `/`, `/chuyen-muc/{slug}`, `/tin/{slug}`, `/tim-kiem?q=`, `/chu-de/{slug}`; graceful 404 and empty states.
- Home: top stories hero, side features, latest list, category panels, sidebar, category nav, footer.
- Tables/entities: Article (title, slug, summary, body, cover, author, status, category, publishedAt), Category, Tag, ArticleTag; indexes on slugs and published status/date.
- Seed >= 8 categories and >= 40 original *fictional demo* Vietnamese articles (idempotent); article pages must query SQL Server via EF Core.
- Desktop 1440, tablet 768, mobile 390 responsive; links/buttons functional.
- Exit gate: build passes + actual SQL-backed homepage/article/search demonstrated. If SQL unavailable, mark runtime BLOCKED.

## M2 — API + SQL skills for the target job
- Versioned REST endpoints for public articles, categories, search, paging; DTOs, validation, consistent errors; no leaked unpublished article.
- Controllers -> services -> EF Core; use LINQ/Lambda expression and projections.
- At least 2 **parameterized SQL Server stored procedures** for meaningful queries (e.g. category published article pagination, daily publish stats), called through app code, with migration and documentation.
- Add indexes and explain how to inspect actual execution plans; never claim speedups without measuring.
- Exit gate: API endpoints and stored procedure calls verified, appropriate tests passing.

## M3 — Editor CMS
- ASP.NET Core Identity + role-based authorization: Admin, Editor, Author, Reader.
- Author creates/edits own drafts; Editor reviews/publishes; Admin manages categories and users. Server-enforced workflow and audit logs.
- CMS CRUD connected to SQL Server, safe uploads; draft must never appear publicly; role checks; CSRF prevention; optimistic concurrency if practical.
- Exit gate: create draft -> review/publish -> public article; unauthorized mutation is rejected and tested.

## M4 — interview ready
- xUnit targeted unit/integration tests, validation and error handling, logging, SEO metadata, sitemap if feasible.
- GitHub Actions CI for build/tests; README, architectural diagram and compact JD-to-code feature map (`docs/FEATURE-MAP.md`).
- Actual browser screenshots of responsive layouts if the tools exist, and test checklist with verifiable status.
- Do not claim 1 year of real work experience or synthetic performance achievements.

## Optional only after M0–M4
Redis caching, Docker Compose, SignalR breaking-news notification, scheduled publishing. Do not let extras delay core.

## Success criteria
A reviewer can run the site on Windows with SQL Server LocalDB, read a genuine SQL-backed article, search, log into admin, create and publish an article, query REST endpoints, run tests, and trace source locations proving OOP/EF/LINQ/Stored Procedures.
