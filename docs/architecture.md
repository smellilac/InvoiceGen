# Architecture

**Status: pre-implementation.** This describes the intended request flow
based on the API contract, not an as-built system — update it once real
code/infrastructure exists, and note where reality diverges from the plan.

## High-level flow

1. **Auth**: client obtains a JWT access/refresh pair via `/auth/login` or
   `/auth/register`, sends `Authorization: Bearer <access_token>` on every
   subsequent request, and refreshes via `/auth/refresh` before it expires.
   The `/auth` group is protected by account lockout (5 fails → 5-min) and
   per-IP rate limiting (20 req/min → `429`) — see `docs/decisions-log.md`.
2. **Main page**: calls `GET /document-types` (public, no auth) to render
   the "what do you want to create?" picker.
3. **Create document**: authenticated `POST /documents` with the filled-out
   form. The backend validates the request, computes the totals (discount
   first, then per-line tax on the discounted amount, then summed — see
   `docs/conventions.md`), persists a `Document` plus its `line_items`, and
   returns the `Document` JSON (including a `pdf_url`).
   If the request carries a `customer_id`, the handler validates it belongs
   to the caller and isn't soft-deleted (else **422**), and — when `to` is
   omitted — snapshots the customer's current name/address into `to`. That
   text is then frozen: later customer edits never touch it (see
   `docs/decisions-log.md`). No PDF is rendered or stored at create time —
   `pdf_url` just points at the download endpoint, which renders on demand.
   - **Guest create (ephemeral):** unauthenticated `POST /documents/guest`
     serves a "try before you sign up" flow. It shares the *exact* validation
     and totals code (`DocumentBuilder` + `Document.Recalculate`) with step 3,
     but **persists nothing** — no `Document` row, no `line_items`, no `id` —
     and renders the PDF inline, streaming the bytes straight back as
     `200 application/pdf` (no create-then-download two-step). It carries no
     `customer_id` and requires `from` (there's no saved profile to fall back
     on). It's per-IP rate-limited on its own tighter window (10/min → `429`),
     because rendering a PDF is heavier than an auth check. See
     `x-guest-document-policy`.
4. **History**: `GET /documents` (paginated, filterable by `type` and
   `customer_id`, ordered newest-first) backs the user's document
   list/history view.
5. **Customers**: authenticated CRUD at `/customers` (`GET` ordered by `name`
   for the document-form picker). Delete is soft — the row stays so historical
   documents keep resolving. See `docs/decisions-log.md`.
6. **Download**: `GET /documents/{id}/pdf` renders the PDF **on demand** with
   **QuestPDF** and streams the bytes. Nothing is stored — the PDF is a fresh
   projection of the document's data on every request. Rendering is
   **synchronous** (inline in the request), no background job.
7. **Send**: `POST /documents/{id}/send` emails the rendered PDF. Unlike
   download, this is **asynchronous** — the endpoint validates, resolves the
   recipient (live lookup: `to_email`, else the linked customer's current
   `email`; none → **422**), enqueues the send, and returns **202 Accepted**.
   A background worker performs the actual send and updates `last_sent_at` /
   `last_send_status` / `last_send_error` on the `Document`; `send_count`
   bumps at enqueue. Rate-limited like `/auth`. See `docs/decisions-log.md`.

## PDF rendering (decided — see `docs/decisions-log.md`)

- **Library:** QuestPDF (code-first, free at this scale).
- **Storage:** none — rendered on demand, never persisted. `pdf_url` points at
  `GET /documents/{id}/pdf`.
- **Timing:** synchronous. Revisit a background job only if rendering ever
  becomes slow enough to hurt latency (complex templates) — not a Phase 1 need.
- **`status`:** `draft`/`generated` is largely nominal now, since a PDF is
  always available on demand.
- **Number formatting:** monetary amounts are formatted per the document's own
  `currency` (`$5,220.74`, not `5 220,74`), driven off `currency` explicitly —
  never a fixed host locale. See `x-rendering-policy` / `docs/conventions.md`.
- **Type label:** the document title reuses `DocumentTypeInfo.name`, never a
  value re-derived from the raw `DocumentType` enum (source of the PDF title
  bug). See `docs/decisions-log.md`.

## Email delivery (decided — see `docs/decisions-log.md`)

The implementation side of `x-email-delivery-policy` (the contract side).

- **Provider:** Brevo, over its **SMTP relay** (not the REST SDK).
- **Library:** MailKit, behind our own `IEmailSender` port — so switching
  provider is a credentials change, not a code change.
- **Async mechanism:** in-process — an unbounded `Channel<EmailSendJob>`
  (`EmailQueue`) written by the endpoint and drained by a single
  `BackgroundService` (`EmailSendingWorker`). No external broker.

Send flow:

1. `POST /documents/{id}/send` → `SendDocumentHandler` resolves the recipient
   (`to_email` → else the linked customer's **live** `email` → else `422`),
   calls `document.MarkSendEnqueued()` (`send_count++`, `last_send_status =
   queued`), saves, enqueues an `EmailSendJob`, and returns **202** at once.
2. `EmailSendingWorker` reads the job in a **per-job DI scope**, loads the
   document (+ line items), renders the PDF (`IPdfRenderer`), and invokes
   `IEmailSender`.
3. `SmtpEmailSender` (MailKit) opens a **fresh `SmtpClient` per send**
   (stateless → safe singleton), STARTTLS-connects to Brevo, authenticates,
   and sends the PDF as an attachment. Success → `document.MarkSent()`; any
   exception → `document.MarkSendFailed(reason)`. The row is saved either way.

- **Sender selection is by environment:** Development/Testing use a no-op
  `LoggingEmailSender` (never sends); Staging/Production use `SmtpEmailSender`.
- **Known limits (deferred):** the in-process Channel loses queued jobs if the
  process crashes, and there's no retry/backoff on a transient SMTP failure.
  Acceptable at this scale — upgrade to a durable outbox/broker + retry if
  delivery guarantees ever matter.

## Not yet decided

- **Frontend hosting/deployment** — the framework is **Angular** (decided, see
  `docs/decisions-log.md`), but where/how it's hosted is open.
- Deployment/infrastructure for the API — nothing here yet;
  `docs/infrastructure.md` doesn't exist yet because there's nothing to document.
