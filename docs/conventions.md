# Conventions

## Money

- Use `decimal` for every monetary value. Never `double`/`float`. This
  applies to C# model properties, EF Core entity properties/column types,
  and request/response DTOs.
- Percentages (`tax_percent`, `discount_percent`) are plain decimals too
  (e.g. `8.5m` for 8.5%), for consistency with the rest of the money math,
  even though they aren't currency amounts themselves.
- Tax rounding: per line item, `MidpointRounding.AwayFromZero`, to 2 decimal
  places, then sum. See `docs/decisions-log.md` for why. Don't round once on
  the subtotal — it will produce totals that don't match the per-line
  breakdown shown on the document.
- Discount before tax: subtract `discount_percent` from the subtotal first,
  then compute tax on the discounted amount — never tax the raw subtotal.
  See `docs/decisions-log.md` for the worked example.

## Rendering (PDF and any future surface)

- Format monetary amounts using the number-formatting convention of the
  document's own `currency` (thousands/decimal separator, symbol placement),
  not a single fixed host locale. A USD document must render `$5,220.74`, not
  `5 220,74`. Drive it off `currency` explicitly — this bug class is invisible
  until a document is generated in a currency whose formatting differs from
  the renderer's default locale.
- A document type's human-readable label is `DocumentTypeInfo.name` — the
  single source of truth. Reuse it anywhere a type is shown (main-page picker,
  PDF titles, future email subjects). Never re-derive a label from the raw
  `DocumentType` enum (`.ToString()` on `CreditNote` yields `CreditNote`, not
  a display label).

## Document numbers

- Never auto-generate, validate for uniqueness, or mutate the `number`
  field server-side. It's the user's field to control.
- If implementing a frontend "suggest next number" convenience, use the
  conservative trailing-digits regex described in `docs/decisions-log.md`,
  and fail silently (leave the field blank) rather than guessing when the
  previous number doesn't match a clean pattern.
- `related_document_number` (e.g. the invoice a `credit_note` credits) follows
  the exact same rule: free text, never validated, never an FK/ID link to a
  document in this system.

## Settlement (`amount_settled` / `balance_remaining`)

- These are one shared pair reused across all 12 document types; their meaning
  depends on `type`. For money-owed-to-you types they mean paid / still owed;
  for `credit_note` they mean refunded / not-yet-refunded. `balance_remaining`
  is always `total − amount_settled`. See `docs/decisions-log.md`.
- Don't reintroduce the old `amount_paid` / `balance_due` names, and don't
  split into separate paid/refunded fields — the single pair keeps the
  request/response shape uniform across every type.

## API error shape

Every error response uses the shape defined in `openapi.yaml`'s
`ErrorResponse`/`ValidationErrorResponse` schemas:

```json
{ "error": { "code": "invalid_credentials", "message": "..." } }
```

Validation errors additionally include a `fields` array pointing at the
specific invalid field(s). Keep this consistent across every endpoint —
don't let individual controllers invent their own error shapes.

## Auth

- Access tokens are short-lived (see `expires_in` in `TokenPair`); use
  `/auth/refresh` rather than forcing re-login.
- `/auth/logout` must actually revoke the refresh token server-side (not
  just tell the client to discard it) — a refresh token that still works
  after logout is a real bug, not a minor detail.
- Brute-force protection is two layers: Identity account lockout (5 fails →
  5-min, per account) + per-IP rate limiting on the `/auth` group (20
  req/min → `429`). Keep the limiter on the whole group, before auth, and
  disabled in the test env. A locked account currently returns `401` (a
  future `423` would distinguish it). See `docs/decisions-log.md`.

## Email delivery

- `POST /documents/{id}/send` is **asynchronous** — enqueue and return `202`,
  never block on the provider. Don't treat `202` as "delivered."
- `send_count` counts attempts (bump at enqueue); `last_sent_at` updates only
  on a real success; `last_send_status`/`last_send_error` reflect the most
  recent attempt and must surface a failure to the client, not swallow it.
- Recipient resolution is a **live** lookup (`to_email` wins, else the linked
  customer's current `email`) — a deliberate exception to the `to` snapshot
  rule; none resolvable → `422`.
- The email subject is auto-generated from `DocumentTypeInfo.name` + `number`
  (same canonical-label rule as PDF titles) — don't hand-build it per call.
- Rate-limit it with the same `429` limiter as `/auth` — it emails a third
  party on the caller's behalf.

## General

- This file grows as real conventions get established during
  implementation — a fresh repo won't have much here yet. Don't leave
  inconsistent patterns undocumented once a second instance of the same
  pattern appears; write the rule down here.
