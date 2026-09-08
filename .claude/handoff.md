# Handoff — feature/soft-delete (Document soft-delete)

## Completed
- [x] **Soft-delete for documents** — `DELETE /documents/{id}` now stamps
      `deleted_at` instead of removing the row, matching the customer convention.
  - `Document` entity: added `DeletedAt` + `IsDeleted` (mirrors `Customer`).
  - `DeleteDocumentHandler`: stamps `DeletedAt` via injected `TimeProvider`
    instead of `db.Documents.Remove(...)`.
  - `DocumentConfiguration`: global query filter
    `HasQueryFilter(d => d.DeletedAt == null)` + `Ignore(IsDeleted)` — auto-excludes
    soft-deleted docs from get / list / pdf / send (no per-handler changes needed).
  - Migration `20260906120000_AddDocumentSoftDelete` (+ Designer + snapshot):
    adds nullable `DeletedAt` column to `documents`.
  - Test renamed → `Delete_SoftDeletes_HidesFromGetListAndPdf`; asserts exclusion
    from get (404), list (total 0), and pdf (404).
  - `docs/openapi.yaml`: DELETE summary/description updated to soft-delete wording.
    **Wire contract unchanged** (204, same GET/list shapes, `DeletedAt` never in any DTO).
- [x] **Full suite green: 55/55** (Testcontainers Postgres; migration exercised
      via `MigrateAsync`). Built/tested via isolated `-o` dir to dodge the WSL bin lock.

## Pending / next steps
- [ ] Feature is complete. Optional: open PR to `master`.
- [ ] Pre-existing unrelated uncommitted changes left unstaged ON PURPOSE
      (present before this task, separate threads — see prior handoff):
      `src/InvoiceGen.Infrastructure/Pdf/PdfRenderer.cs` (PDF restyle),
      `docs/apicontract.md` + `docs/conventions.md` (CRLF-only churn,
      `git diff --ignore-all-space` = empty).

## Learned / gotchas
- **`dotnet ef migrations add` fails with MSB3021** when `InvoiceGen.Api.exe` is
  running on Windows (it locks `src/*/bin` DLLs; `rm` gives I/O error). Workaround
  used: hand-authored the migration `.cs` + `.Designer.cs` + updated
  `AppDbContextModelSnapshot.cs` to match EF's output, then validated via the test
  suite (which runs migrations). Alternative: stop the running app first.
- **Query filters are NOT emitted into migrations/snapshots** — only the schema
  column delta appears; the filter lives in `DocumentConfiguration`.
- Build/test to an isolated `-o <dir>` to avoid the WSL bin lock while the app runs.

## Context
- Branch: feature/soft-delete | Checkpoint: 438a648
- Base: master | Prev commit: 3bf9542
