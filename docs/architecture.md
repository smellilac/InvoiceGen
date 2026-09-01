# Architecture

**Status: pre-implementation.** This describes the intended request flow
based on the API contract, not an as-built system — update it once real
code/infrastructure exists, and note where reality diverges from the plan.

## High-level flow

1. **Auth**: client obtains a JWT access/refresh pair via `/auth/login` or
   `/auth/register`, sends `Authorization: Bearer <access_token>` on every
   subsequent request, and refreshes via `/auth/refresh` before it expires.
2. **Main page**: calls `GET /document-types` (public, no auth) to render
   the "what do you want to create?" picker.
3. **Create document**: authenticated `POST /documents` with the filled-out
   form. The backend validates the request, computes the totals (per-line
   tax then summed — see `docs/conventions.md`), persists a `Document` plus
   its `line_items`, and returns the `Document` JSON (including a `pdf_url`).
   No PDF is rendered or stored at create time — `pdf_url` just points at the
   download endpoint, which renders on demand.
4. **History**: `GET /documents` (paginated, filterable by `type`, ordered
   newest-first) backs the user's document list/history view.
5. **Download**: `GET /documents/{id}/pdf` renders the PDF **on demand** with
   **QuestPDF** and streams the bytes. Nothing is stored — the PDF is a fresh
   projection of the document's data on every request. Rendering is
   **synchronous** (inline in the request), no background job.

## PDF rendering (decided — see `docs/decisions-log.md`)

- **Library:** QuestPDF (code-first, free at this scale).
- **Storage:** none — rendered on demand, never persisted. `pdf_url` points at
  `GET /documents/{id}/pdf`.
- **Timing:** synchronous. Revisit a background job only if rendering ever
  becomes slow enough to hurt latency (complex templates) — not a Phase 1 need.
- **`status`:** `draft`/`generated` is largely nominal now, since a PDF is
  always available on demand.

## Not yet decided

- Frontend framework/hosting.
- Deployment/infrastructure — nothing here yet; `docs/infrastructure.md`
  doesn't exist yet because there's nothing to document.
