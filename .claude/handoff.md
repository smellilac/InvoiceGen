# Handoff — feature/auth

## Completed
- [x] Auth feature end-to-end: Register, Login, Refresh, Logout, GET/PATCH /auth/me
- [x] ASP.NET Identity user store (AppUser : IdentityUser<Guid>) + business fields
- [x] JWT access token (sub + jti) + DB-backed hashed rotating refresh token
- [x] Account lockout (5 attempts / 5 min)
- [x] EF Core + Npgsql + InitialAuth migration; snake_case JSON matching OpenAPI
- [x] Secrets in user-secrets (Jwt:Key set; connection string = user to set); fail-fast key check
- [x] 17 integration tests (WebApplicationFactory + in-memory SQLite) — all passing
- [x] All handlers/services/endpoints/Program.cs reviewed
- [x] Checkpoint committed: 564a785

## Pending
- [ ] USER ACTION: set connection string in user-secrets:
      dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=invoicegen;Username=postgres;Password=postgres" --project src/InvoiceGen.Api
- [ ] Run a real Postgres + apply migration:
      dotnet ef database update --project src/InvoiceGen.Infrastructure --startup-project src/InvoiceGen.Api
- [ ] Next feature: Documents (Create, List, Get, GetPdf, Delete) — needs the
      JSON-vs-child-table decision for line items (see docs/data-layer.md)
- [ ] Deferred hardening (not bugs): refresh-token reuse detection (revoke family),
      transaction around create+issue token, expired-token cleanup job,
      FluentValidation for request shapes, PATCH can't-clear-a-field

## Learned
- ErrorOr implicit conversion doesn't work through interface type params (use concrete T[]/List<T>)
- Minimal-hosting: config via factory ConfigureAppConfiguration doesn't reach startup-time
  builder.Configuration reliably — use env vars in tests
- Docker unavailable here → tests use in-memory SQLite (EnsureCreated) instead of Testcontainers
- EF tooling: no design-time factory; use --startup-project src/InvoiceGen.Api (needs
  EntityFrameworkCore.Design ref in Api)

## Context
- Branch: feature/auth
- Checkpoint: 564a785
