# InvoiceGen

## What this is

A web app for creating billing documents (invoices, receipts, quotes, and
other types — see `docs/api-contract.md`). Users log in, pick a document
type, fill out a form, and get a PDF. Inspired by invoice-generator.com's
feature set, but an independent product: own backend, own data model, own UI.

**Status: Phase 1 — scaffolded, pre-feature.** Solution structure exists;
no feature endpoints implemented yet. Update this file and `docs/` as real
implementation choices land; don't let them go stale once code exists.

## Tech stack

- Backend: ASP.NET Core (.NET 10), C#
- Architecture: **Clean Architecture + VSA** — Clean Architecture layers for
  separation of concerns; features organized as vertical slices within each layer
- Auth: JWT (access + refresh token pair) — see `docs/api-contract.md`
- Database: **PostgreSQL** via EF Core
- ORM: EF Core
- Frontend: **not yet decided**

## Solution structure

```
src/
  InvoiceGen.Domain/              — Entities, interfaces, domain rules (no dependencies)
    Entities/
    Interfaces/
  InvoiceGen.Application/         — Use cases organized as vertical slices
    Features/
      Auth/                       — Login, Register, Refresh, Logout
      Documents/                  — Create, List, Get, GetPdf
      DocumentTypes/              — List
    Common/                       — Shared abstractions, base types
  InvoiceGen.Infrastructure/      — EF Core, PostgreSQL, PDF renderer
    Persistence/
    Pdf/
  InvoiceGen.Api/                 — Minimal API endpoints organized as vertical slices
    Features/
      Auth/
      Documents/
      DocumentTypes/
tests/
  InvoiceGen.Tests/               — Integration tests (xUnit + Testcontainers)
```

**Reference direction:** Domain ← Application ← Infrastructure ← Api

## Non-negotiable conventions

These came out of real bugs/decisions during spec design, not arbitrary
style preferences — see `docs/decisions-log.md` for the reasoning behind
each one.

- **Money is always `decimal`, never `double`/`float`.** Verified concretely:
  `0.1m + 0.2m == 0.3m` exactly in C#; the `double` equivalent does not.
  Applies to every layer: C# models, EF Core entities/columns, DTOs.
- **Tax is rounded per line item, then summed** — not computed once on the
  subtotal. The two methods can differ by 1 minor unit; this repo always
  uses the per-line method. See `docs/decisions-log.md` for the worked
  example.
- **Document `number` is free-text and optional. The API never generates,
  validates, or mutates it.** Any "suggest the next number" behavior is a
  frontend-only convenience using a conservative regex (safe only when the
  previous number ends in digits) — never guess when it doesn't match.

## Where to look

| Doc | Covers |
|---|---|
| `docs/api-contract.md` | The API surface itself — summary of `docs/openapi.yaml`, what's in Phase 1 vs. deferred |
| `docs/openapi.yaml` | The actual OpenAPI 3.0 spec — source of truth for request/response shapes |
| `docs/domain.md` | Core entities (User, Document, LineItem, DocumentType) and the business rules around them |
| `docs/decisions-log.md` | Why things are the way they are — numbering, money precision, rounding order, auth model. Read this before "fixing" something that looks odd; it might be deliberate |
| `docs/conventions.md` | Coding conventions specific to this repo |
| `docs/data-layer.md` | Database/persistence approach — currently mostly placeholders, fill in as decided |
| `docs/architecture.md` | High-level request flow (auth, document creation, PDF rendering) |
| `docs/development.md` | How to run/build/test locally — placeholder until the project is scaffolded |

Deferred to a later phase (not yet documented in detail — see
`x-future-phases` in `docs/openapi.yaml`): Customers as a standalone
resource, payments/webhooks, email delivery, UBL/e-invoice export.
`infrastructure.md`, `observability.md`, and `service-boundaries.md` aren't
created yet either — add them when there's actually infrastructure or more
than one service to document.
