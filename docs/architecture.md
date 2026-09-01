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
   form. The backend validates the request, persists a `Document` record,
   renders it to PDF, stores the PDF (location TBD — see below), and
   returns the `Document` JSON (including a `pdf_url`) — it does not stream
   the PDF back directly in this response.
4. **History**: `GET /documents` (paginated, filterable by `type`, ordered
   newest-first) backs the user's document list/history view.
5. **Download**: `GET /documents/{id}/pdf` streams the actual PDF bytes,
   fetched separately from the create/list/get calls that return JSON.

## Not yet decided

- Where rendered PDFs are stored (local disk, blob storage, etc.) and how
  `pdf_url` is served from there.
- Whether PDF rendering happens synchronously inside `POST /documents` or is
  offloaded to a background job (relevant if rendering ever becomes slow
  enough to matter — not expected to be an issue for simple documents, but
  worth revisiting if templates get complex).
- Frontend framework/hosting.
- Deployment/infrastructure — nothing here yet; `docs/infrastructure.md`
  doesn't exist yet because there's nothing to document.
