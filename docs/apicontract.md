# API Contract

Source of truth: `docs/openapi.yaml` (OpenAPI 3.0.3). This file is a summary
and index — when the two disagree, the YAML wins; update this file to match
it, not the other way around.

## Phase 1 scope (current)

- **Auth** (`/auth/register`, `/auth/login`, `/auth/refresh`, `/auth/logout`,
  `/auth/me`) — JWT access + refresh token pair, not bare API keys. Chosen
  because this is a multi-user web app people log into, not a single-tenant
  integration tool.
- **Document types** (`/document-types`) — public, unauthenticated. Lets the
  main page render its "what do you want to create?" choices without
  hardcoding the list client-side.
- **Documents** (`/documents`, `/documents/{id}`, `/documents/{id}/pdf`) —
  create, list (history), get, delete, download PDF. Creating a document
  persists it first and returns JSON with a `pdf_url`, rather than streaming
  the PDF directly back — this is a deliberate difference from
  invoice-generator.com's API, made because documents belong to a logged-in
  user's account and need to show up in their history.

## Explicitly deferred (see `x-future-phases` in the YAML)

- **Customers** as a standalone resource — right now `to` on a document is
  free text. Deferred until repeat-customer support is actually needed;
  `Document`'s shape is written so this slots in without breaking existing
  fields.
- **Payments & webhooks** — no payment tracking or external notifications yet.
- **Email delivery** — no "send this document to the customer" endpoint yet
  (mirrors invoice-generator.com's "Save & Send" button, which is also not
  replicated yet).
- **UBL/e-invoice XML export** — invoice-generator.com has a `/ubl` endpoint
  for this; not replicated yet.

## Key non-obvious decisions

Full reasoning for each of these lives in `docs/decisions-log.md` — this is
just the "what," not the "why."

- Monetary fields use the `MonetaryAmount` schema (decimal, not float/double
  — see `docs/conventions.md`).
- Tax is rounded per line item, then summed (not rounded once on the
  subtotal).
- `number` is optional free text; the API never generates or validates it.
- `GET /documents` is ordered `created_at` descending by default — the
  frontend's number-suggestion behavior depends on this.

## Known gaps / things to decide before this is "production ready"

Carried over from spec review, not yet resolved:

- No password reset flow.
- No email verification on registration.
- No logo file upload endpoint (`logo_url` assumes the user already has a
  hosted URL, which isn't realistic).
- No idempotency key on `POST /documents` (a double-submit currently creates
  two documents).
- No `failed` status on `Document` — if PDF rendering errors, there's
  currently no way to represent that.
