# VNews — Feature map (JD skill → code location)

Dùng để chứng minh nhanh các kỹ năng .NET/SQL khi phỏng vấn, trỏ thẳng vào source thật, không mô tả suông.

| Kỹ năng / yêu cầu JD | Nơi thể hiện trong code |
|---|---|
| ASP.NET Core MVC (Razor Views, Controllers) | `src/VNews.Web/Controllers/*.cs`, `src/VNews.Web/Views/**/*.cshtml` |
| REST API, versioning | `src/VNews.Web/Controllers/Api/*.cs` (route `api/v1/...`) |
| EF Core + LINQ + Lambda | `src/VNews.Infrastructure/Services/ArticleQueryService.cs` (projection LINQ: `GetHomePageDataAsync`, `SearchAsync`), `ArticleCommandService.cs` |
| SQL Server Stored Procedures (tham số hoá) | Migration `src/VNews.Infrastructure/Persistence/Migrations/20261029052406_AddStoredProcedures.cs`; gọi qua `ArticleStoredProcedureService.cs` (`FromSqlRaw` + `SqlParameter`, keyless entity) |
| Indexes / database design | `src/VNews.Infrastructure/Persistence/Configurations/*.cs` (unique index trên `Slug`, composite index `(Status, PublishedAt)`, index `AuthorUserId`) |
| ASP.NET Core Identity, Authentication | `src/VNews.Infrastructure/DependencyInjection.cs` (`AddIdentity`), `src/VNews.Web/Controllers/AccountController.cs`, `src/VNews.Domain/Identity/ApplicationUser.cs` |
| Role-based Authorization | `[Authorize(Roles=...)]` trên `CmsArticlesController.cs`, `CmsCategoriesController.cs`, `CmsUsersController.cs`; `src/VNews.Domain/Identity/Roles.cs` |
| Business workflow (state machine) | `ArticleCommandService.cs` — Draft → PendingReview → Published/Rejected, validate chuyển trạng thái, ném `ArticleWorkflowException` khi sai quy tắc |
| Optimistic concurrency | `Article.RowVersion` (`[Timestamp]`) trong `src/VNews.Domain/Entities/Article.cs`; xử lý `DbUpdateConcurrencyException` trong `ArticleCommandService.UpdateDraftAsync` |
| CSRF protection | `[ValidateAntiForgeryToken]` trên mọi POST CMS; form tag helper tự sinh token |
| XSS prevention / input sanitization | `ArticleCommandService.BuildSanitizer()` (Ganss.Xss `HtmlSanitizer`, allow-list thẻ/thuộc tính/scheme) |
| File upload an toàn | `src/VNews.Web/Services/ImageUploadService.cs` — allow-list extension/Content-Type, giới hạn kích thước, kiểm tra magic bytes, tên file ngẫu nhiên |
| Audit logging | `src/VNews.Domain/Entities/AuditLog.cs`, `AuditLogService.cs`, gọi từ mọi hành động ghi trong `ArticleCommandService` |
| Validation (server-side + client-side) | Data Annotations trên `ArticleSaveRequest.cs`/`CategorySaveViewModel.cs`; `_ValidationScriptsPartial.cshtml` cho client-side |
| Dependency Injection, Clean Architecture | `src/VNews.Infrastructure/DependencyInjection.cs`; phân lớp Domain/Application/Infrastructure/Web độc lập |
| Pagination, filtering, searching | `PagedResult<T>` (`src/VNews.Application/Common/PagedResult.cs`), dùng xuyên suốt `IArticleQueryService`, API, CMS list |
| Unit testing (xUnit) | `tests/VNews.Tests/Unit/PagedResultTests.cs` |
| Integration testing (WebApplicationFactory, SQL Server thật) | `tests/VNews.Tests/Integration/ArticlesApiIntegrationTests.cs`, `CmsWorkflowIntegrationTests.cs` — không mock DB |
| CI/CD | `.github/workflows/ci.yml` (build + test trên `windows-latest`, có LocalDB sẵn — **chưa chạy thật trên GitHub, chỉ build/test local đã xác nhận**) |
| Responsive UI, accessibility cơ bản | `src/VNews.Web/wwwroot/css/site.css` (breakpoint 1440/768/390), đã kiểm chứng bằng Playwright CLI screenshot thật |
| SEO cơ bản | *(chưa làm — xem `docs/PROGRESS.md` mục thiếu sót)* |

## Lưu ý trung thực
Bảng trên trỏ đúng vị trí code thật tại thời điểm viết (2026-10-29), không phải mô tả lý thuyết. Một số mục (CI thực thi trên GitHub, SEO metadata/sitemap) chưa được triển khai hoặc chưa chạy thật — xem `docs/PROGRESS.md` để biết chính xác trạng thái từng milestone.
