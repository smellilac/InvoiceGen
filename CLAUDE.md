# InvoiceGen

## What this is

A web app for creating billing documents (invoices, receipts, quotes, and
other types — see `docs/api-contract.md`). Users log in, pick a document
type, fill out a form, and get a PDF. Inspired by invoice-generator.com's
feature set, but an independent product: own backend, own data model, own UI.

**Status: active development — core backend implemented.** The Phase 1
feature set is built and wired end to end (Domain → Application → Infrastructure
→ Api), backed by EF Core migrations and integration tests. Implemented today:

- **Auth** (`/auth`): register, login, JWT refresh, logout, `GET`/`PATCH`/`DELETE
  /me` (self-service account deletion), and business-logo upload/serve/delete
  (`POST`/`DELETE /me/logo`, `GET /auth/logo/{id}`, processed via ImageSharp).
- **Customers** (`/customers`): full CRUD — create, list, get, update, and
  soft delete.
- **Documents** (`/documents`): create, list, get, delete, on-demand PDF render
  (`GET /{id}/pdf`, QuestPDF), async email send (`POST /{id}/send`, MailKit/Brevo),
  settlement recording (`POST /{id}/settlement`), and ephemeral **guest**
  generation for unauthenticated users (`POST /documents/guest`).
- **Document types** (`/document-types`): list.

Keep this file and `docs/` in sync as further features land; don't let them
go stale.

## Tech stack

- Backend: ASP.NET Core (.NET 10), C#
- Architecture: **Clean Architecture + VSA** — Clean Architecture layers for
  separation of concerns; features organized as vertical slices within each layer
- Auth: **ASP.NET Core Identity** (user store + password hashing) with
  **JWT** access + refresh tokens layered on top — see `docs/api-contract.md`
  and `docs/decisions-log.md`
- Database: **PostgreSQL** via EF Core
- ORM: EF Core
- PDF: **QuestPDF** — rendered **on demand**, never stored (`pdf_url` points at
  the download endpoint); synchronous. See `docs/decisions-log.md`.
- Email delivery: `POST /documents/{id}/send` emails the rendered PDF —
  **asynchronous** (enqueue + `202 Accepted`, a background worker does the
  actual send). Provider: **Brevo**, reached via **SMTP** using the
  **MailKit** library (not Brevo's REST SDK). See `docs/decisions-log.md`.
- Frontend: **Angular** (with its ecosystem — Angular CLI, RxJS, etc.)

## Solution structure

```
src/
  InvoiceGen.Domain/              — Entities, interfaces, domain rules (no dependencies)
    Entities/
    Interfaces/
  InvoiceGen.Application/         — Use cases organized as vertical slices
    Features/
      Auth/                       — Login, Register, Refresh, Logout
      Customers/                  — Create, List, Get, Update, Delete (soft)
      Documents/                  — Create, List, Get, GetPdf, Send
      DocumentTypes/              — List
    Common/                       — Shared abstractions, base types
  InvoiceGen.Infrastructure/      — EF Core, PostgreSQL, PDF renderer
    Persistence/
    Pdf/
  InvoiceGen.Api/                 — Minimal API endpoints organized as vertical slices
    Features/
      Auth/
      Customers/
      Documents/
      DocumentTypes/
tests/
  InvoiceGen.Tests/               — Integration tests (xUnit + Testcontainers)
```

**Reference direction:** Domain ← Application ← Infrastructure ← Api

## Error handling

Use `ErrorOr<T>` from the `ErrorOr` library for all result/error handling.
Do not introduce custom Result or discriminated-union wrappers.

## Auth (decided — see `docs/decisions-log.md` for the full reasoning)

- **User store: ASP.NET Core Identity.** `AppUser : IdentityUser<Guid>`,
  extended with business fields (`BusinessName`, `BusinessAddress`, `LogoUrl`,
  `DefaultCurrency`, `CreatedAt`). Chosen over a custom store because email
  confirmation and 2FA are planned; don't reintroduce a hand-rolled user table.
- **Passwords:** Identity's `PasswordHasher<AppUser>` (PBKDF2). Never hand-roll
  hashing.
- **Tokens:** short-lived **JWT** access token (~15 min, stateless, not stored)
  + long-lived **refresh token stored hashed in Postgres** (custom
  `refresh_tokens` table). Logout revokes the refresh-token row server-side —
  a refresh token that still works after logout is a bug.
- **Authorization:** `.RequireAuthorization()` + a `user_id` claim + an
  ownership filter (`WHERE user_id = caller`) in handlers. Non-owner → **404**,
  not 403. No roles, no policies unless a privilege tier actually appears.
- **Brute-force protection (two layers):** Identity **account lockout** (5
  failed logins → 5-min lockout, per account) + **per-IP rate limiting** on the
  whole `/auth` group (fixed window, 20 req/min/IP → **429**). A locked account
  currently surfaces as `401` (indistinguishable from bad credentials); a future
  `423 Locked` would separate them. Rate limiting is disabled in the test env.
- **JWT signing key:** via `dotnet user-secrets` in dev, env vars/secret store
  in prod. Never commit it to `appsettings.json`.
- Layering: entities in Domain, auth handlers + token interface in Application,
  Identity/JWT implementations in Infrastructure, bearer config + endpoints in
  Api. No separate auth project.

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
- **Discount is applied before tax.** Tax is computed on the subtotal *after*
  `discount_percent` is subtracted, never on the raw subtotal. Worked
  example: $4,520.00 − 5% = $4,294.00, then 21% tax = $901.74 (not $949.20).
  See `docs/decisions-log.md`.
- **Rendered amounts are formatted per the document's own `currency`**, not a
  fixed host locale — a USD document renders `$5,220.74`, never `5 220,74`.
  Drive number formatting off `currency` explicitly in the PDF renderer.
- **A document type's display label comes from `DocumentTypeInfo.name`** — the
  single source of truth. Reuse it everywhere a type is shown (picker, PDF
  title, future surfaces); never re-derive it from the raw `DocumentType`
  enum (`.ToString()` on `CreditNote` is not fit for display).
- **Document `number` is free-text and optional. The API never generates,
  validates, or mutates it.** Any "suggest the next number" behavior is a
  frontend-only convenience using a conservative regex (safe only when the
  previous number ends in digits) — never guess when it doesn't match.
  `related_document_number` (the invoice a `credit_note` credits) follows the
  identical rule — free text, never an FK/ID link to a document in this system.
- **Settlement is one shared field pair: `amount_settled`/`balance_remaining`**
  (never the old `amount_paid`/`balance_due`, never split into paid/refunded).
  Meaning depends on `type` — paid/owed for money-owed-to-you types,
  refunded/not-yet-refunded for `credit_note`; `balance_remaining` is always
  `total − amount_settled`. See `docs/decisions-log.md`.
- **A document's `to` (customer name/address) is a frozen snapshot taken at
  creation time, not a live link to the Customer record.** `customer_id` is
  kept only for filtering/lookup; editing a customer later never rewrites the
  `to` text on documents already created. Deleting a customer is a **soft
  delete** (`deleted_at`), never a row removal — historical documents keep
  their `to` text and `customer_id`. See `docs/decisions-log.md`.
- **Email send is async and resolves the recipient at send time.** `POST
  /documents/{id}/send` enqueues and returns **202** — it does *not* confirm
  delivery. The recipient email is a **live lookup** (`to_email` wins, else the
  linked customer's *current* `email`) — a deliberate exception to the `to`
  snapshot rule, because a stale email misdelivers. No email resolvable → 422.
  See `docs/decisions-log.md`.

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
`x-future-phases` in `docs/openapi.yaml`): payments/webhooks (explicitly
scoped down to "not needed yet" as of 0.3.0, not designed) and UBL/e-invoice
export. (Customers landed in 0.3.0 and email delivery in 0.8.0 — both no
longer deferred.)
`infrastructure.md`, `observability.md`, and `service-boundaries.md` aren't
created yet either — add them when there's actually infrastructure or more
than one service to document.
