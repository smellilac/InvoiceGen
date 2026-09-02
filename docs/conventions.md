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

## General

- This file grows as real conventions get established during
  implementation — a fresh repo won't have much here yet. Don't leave
  inconsistent patterns undocumented once a second instance of the same
  pattern appears; write the rule down here.
