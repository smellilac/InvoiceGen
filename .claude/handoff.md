# Handoff — feature/document-types

## Completed
- [x] Solution scaffolded: Clean Architecture + VSA (Domain / Application / Infrastructure / Api / Tests)
- [x] Stack confirmed: .NET 10, PostgreSQL, EF Core, ErrorOr, xUnit + WebApplicationFactory
- [x] GET /document-types — full slice end-to-end (handler, endpoint, 6 passing integration tests)
- [x] ErrorOr<T> adopted; custom Result<T> removed; convention documented in CLAUDE.md
- [x] Explicit endpoint registration (no reflection scanning)

## Pending
- [ ] Auth feature: Register, Login, Refresh, Logout, GET /auth/me — run `/scaffold Auth` next
- [ ] After Auth: Documents feature (Create, List, Get, GetPdf, Delete)
- [ ] EF Core + PostgreSQL wiring (DbContext, migrations) — needed before Documents
- [ ] PDF rendering infrastructure (InvoiceGen.Infrastructure/Pdf/)

## Learned
- ErrorOr implicit conversion doesn't work with interface type parameters (e.g. IReadOnlyList<T>); use concrete types (T[], List<T>)
- UseExceptionHandler() requires AddProblemDetails() in .NET 10 when called with no arguments

## Context
- Branch: feature/document-types
- Checkpoint: 01c5c1e
