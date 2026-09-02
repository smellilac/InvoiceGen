# Handoff — feature/documents

## Completed
- [x] Documents feature: Create / List / Get / Delete / GetPdf
- [x] Domain: Document + LineItem (child table), Recalculate() with per-line tax rounding
- [x] On-demand QuestPDF rendering (never stored); Packing Slip hides pricing/totals
- [x] Friendly type names from single source (DocumentTypeApi.ToDisplayName) — /document-types + PDF title
- [x] FluentValidation for CreateDocumentRequest via reusable ValidationFilter<T> (422, all fields)
- [x] CreateDocumentHandler slimmed; entity build extracted to BuildDocument
- [x] List pagination: newest-first + Id tiebreaker; per_page default 20 / max 30
- [x] Per-IP rate limiter on /auth (20/min), off under Testing env
- [x] JWT setup extracted to AddJwtBearerAuthentication
- [x] Tests migrated to Testcontainers + PostgreSQL (applies real migrations); SQLite + its converter removed
- [x] Build: GREEN. Checkpoint: 45a6ea8

## Pending / next steps
- [ ] **RUN THE TESTS** in a Docker-enabled session — never executed since the Testcontainers switch:
      cd /mnt/c/Users/dmitrii/Projects/invoiceapp && dotnet test InvoiceGen.slnx
      (first run pulls postgres:16-alpine; each test class starts its own container)
- [ ] docs/openapi.yaml: `per_page` still says `maximum: 100` but code now caps at 30 — update to match.
      Also the file has an uncommitted EOL-only change in the working tree (excluded from 45a6ea8).
- [ ] Customers feature (0.3.0, per docs) — not yet implemented in code: soft-delete,
      frozen `to` snapshot; add AddCustomersHandlers() + validator + endpoints.
- [ ] Deferred hardening: refresh-token reuse detection, expired-token cleanup, transaction
      around create+issue, ExecuteDelete option for DeleteDocument, PDF text-extraction test
      to assert packing-slip has no pricing.

## Learned / non-obvious
- Testcontainers 4.14: PostgreSqlBuilder parameterless ctor is obsolete → use new PostgreSqlBuilder("postgres:16-alpine")
- xUnit v2 here: IAsyncLifetime uses Task; its DisposeAsync clashes with WebApplicationFactory's
  ValueTask one → implement IAsyncLifetime.DisposeAsync explicitly
- Rate limiter would throttle the test suite (same loopback IP) → gated behind non-Testing env
- ValidationFilter<T> is generic/reused; each request adds only a validator + one .AddEndpointFilter line

## Context
- Branch: feature/documents
- Checkpoint: 45a6ea8
