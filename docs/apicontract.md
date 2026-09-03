# API Contract

Source of truth: `docs/openapi.yaml` (OpenAPI 3.0.3). This file is a summary
and index — when the two disagree, the YAML wins; update this file to match
it, not the other way around.

## Phase 1 scope (current)

- **Auth** (`/auth/register`, `/auth/login`, `/auth/refresh`, `/auth/logout`,
  `/auth/me`) — JWT access + refresh token pair, not bare API keys. Chosen
  because this is a multi-user web app people log into, not a single-tenant
  integration tool. Brute-force protection is two-layered: Identity account
  lockout (5 fails → 5-min) + per-IP rate limiting on the whole `/auth` group
  (20 req/min → `429`). See `x-security-policy` and the key decisions below.
- **Document types** (`/document-types`) — public, unauthenticated. Lets the
  main page render its "what do you want to create?" choices without
  hardcoding the list client-side. The `DocumentType` set tracks
  invoice-generator.com's generators; as of 0.5.0 there are **12 types**
  (`sales_order` added — see `docs/decisions-log.md`). The enum in
  `openapi.yaml` is the authoritative list.
- **Customers** (`/customers`, `/customers/{id}`) — create, list (search +
  paginate), get, update (PATCH), soft-delete. Saved customer records a
  document can reference by `customer_id` instead of retyping the `to` field.
  Referencing a customer auto-fills `to` at creation time only — the document
  stores a **frozen snapshot**, never a live link (see key decisions below).
- **Documents** (`/documents`, `/documents/{id}`, `/documents/{id}/pdf`,
  `/documents/{id}/send`) — create, list (history), get, delete, download PDF,
  and email the PDF to a customer. Creating a document persists it first and
  returns JSON with a `pdf_url`, rather than streaming the PDF directly back —
  this is a deliberate difference from invoice-generator.com's API, made
  because documents belong to a logged-in user's account and need to show up in
  their history.
- **Email delivery** (`POST /documents/{id}/send`, added 0.8.0) — emails the
  rendered PDF. **Asynchronous**: validates, resolves the recipient, enqueues,
  and returns **202 Accepted** (not a delivery confirmation). Recipient is a
  live lookup (`to_email` wins, else the linked customer's current `email`;
  none resolvable → 422). Rate-limited with the same `429` as `/auth`. Delivery
  outcome is tracked on `Document` (`last_sent_at`, `send_count`,
  `last_send_status`, `last_send_error`). See `x-email-delivery-policy`.

## Explicitly deferred (see `x-future-phases` in the YAML)

- **Payments & webhooks** — no payment tracking or external notifications yet.
  As of 0.3.0 this is explicitly scoped down to "not needed yet" (not
  designed): there's no near-term need to collect money through this system.
  When revisited, decide first whether it means "record that a payment
  happened" (small) or "collect via a processor like Stripe" (much bigger —
  PCI scope, webhook signature verification, idempotency, refunds).
- **UBL/e-invoice XML export** — invoice-generator.com has a `/ubl` endpoint
  for this; not replicated yet.

(Email delivery moved into scope in 0.8.0 — see the Documents section above.)

## Key non-obvious decisions

Full reasoning for each of these lives in `docs/decisions-log.md` — this is
just the "what," not the "why."

- Monetary fields use the `MonetaryAmount` schema (decimal, not float/double
  — see `docs/conventions.md`).
- Tax is rounded per line item, then summed (not rounded once on the
  subtotal).
- Discount is applied before tax — tax is computed on the post-discount
  amount, not the raw subtotal (`x-rounding-policy.discount_tax_ordering`).
- Rendered amounts are formatted per the document's own `currency`, not a
  fixed host locale (`x-rendering-policy`).
- A type's display label always comes from `DocumentTypeInfo.name`; it's never
  re-derived from the raw `DocumentType` enum (picker, PDF title, and any
  future surface all reuse it).
- `number` is optional free text; the API never generates or validates it.
  `related_document_number` (e.g. the invoice a `credit_note` credits) follows
  the same rule — free text, never an FK/ID link (0.6.0).
- Settlement fields are one shared pair, `amount_settled`/`balance_remaining`
  (renamed from `amount_paid`/`balance_due` in 0.6.0). Meaning depends on
  `type`: paid/owed for most types, refunded/unrefunded for `credit_note`
  (`x-settlement-policy`).
- `GET /documents` is ordered `created_at` descending by default — the
  frontend's number-suggestion behavior depends on this.
- A document's `to` is a **frozen snapshot** of the customer's info at
  creation time, not a live view of the Customer record; `customer_id` is
  kept only for filtering/lookup. Editing a customer never rewrites past
  documents' `to`. (`GET /customers` is ordered by `name` ascending, since
  it backs a picker while filling out a form.)
- Customer deletion is a **soft delete** (`deleted_at`). A soft-deleted
  customer vanishes from `GET /customers` and can no longer be referenced by
  new documents (`POST /documents` with its `customer_id` → **422**), but
  existing documents and `GET /documents?customer_id=...` filtering are
  unaffected.
- Brute-force protection is two layers: Identity account lockout (per account)
  + per-IP rate limiting on `/auth` (and `POST /documents/{id}/send`). A locked
  account currently returns `401`, indistinguishable from bad credentials
  (`x-security-policy`).
- Email send is **async** — `POST /documents/{id}/send` returns `202`, not a
  delivery confirmation; clients re-fetch the document for up-to-date
  `last_send_status`. Recipient resolution is a **live** email lookup (a
  deliberate exception to the `to` snapshot rule). Subject is auto-generated
  from `DocumentTypeInfo.name` + `number` (`x-email-delivery-policy`).

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
- A locked account returns `401`, indistinguishable from bad credentials — a
  future `423 Locked` (a code change) would separate them (`x-security-policy`).
