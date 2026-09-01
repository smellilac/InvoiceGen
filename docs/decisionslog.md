# Decisions Log

Why things are the way they are. Read this before "fixing" something that
looks like an oversight — it might be deliberate. Add a new entry any time a
non-obvious design decision gets made; don't let this go stale.

---

## Money is `decimal`, never `double`/`float`

**Decision:** Every monetary field (`unit_cost`, `subtotal`, `total`,
`amount_paid`, `balance_due`, `shipping_amount`) is documented as the
`MonetaryAmount` schema and must map to C#'s `decimal` type everywhere —
models, EF Core entities/columns, DTOs.

**Why:** Binary floating point (`double`/`float`) cannot represent most
decimal fractions exactly. Verified concretely, not assumed:

```csharp
double a = 0.1, b = 0.2;
Console.WriteLine(a + b);       // 0.30000000000000004

decimal da = 0.1m, db = 0.2m;
Console.WriteLine(da + db);     // 0.3 (exact)
```

`decimal` is a first-class .NET type built for exactly this (128-bit,
base-10 internally), so — unlike in JavaScript or Python, which lack a
built-in fixed-point decimal type — there was no need to fall back to
integer minor units (storing cents as integers, à la Stripe). `decimal`
round-trips exactly through JSON on the .NET side (its serializer parses
JSON number tokens directly into `decimal`, not via `double`), so a plain
JSON number on the wire is fine as long as the server-side type is right.

**Rejected alternative:** Integer minor units (cents). Would have worked,
but `decimal` is simpler for a .NET-only stack and avoids needing a
per-currency lookup table for currencies with non-2 decimal places (JPY has
0, BHD has 3).

---

## Tax rounds per line item, then sums — not once on the subtotal

**Decision:** For a document with tax applied, each line item's tax is
calculated and rounded to 2 decimal places individually
(`MidpointRounding.AwayFromZero`), and the total tax is the sum of those
already-rounded per-line amounts.

**Why:** Verified with a real example (subtotal $191.78, tax 8.5%):

| Method | Result |
|---|---|
| Round once, on the subtotal | `$191.78 × 8.5% = $16.3013` → **$16.30** |
| Round per line item, then sum | `$5.10 + $3.87 + $7.34` → **$16.31** |

Both are legitimate methods and they disagree by 1 cent on this exact input.
Per-line was chosen because a document is read line-by-line by a human, and
each line should look individually correct — the way someone calculating it
by hand would present it. This is a product decision, not a technical
default; if it's ever revisited, update this entry and
`x-rounding-policy` in `docs/openapi.yaml` together.

---

## Document `number` is free text; the API never generates it

**Decision:** `number` on `CreateDocumentRequest`/`Document` is optional,
stored exactly as submitted, never auto-generated, never checked for
uniqueness, never mutated by the backend.

**Why:** The obvious alternative — auto-increment the last number — breaks
on real-world formats. Tested against actual examples:

| Previous number | Naive "increment trailing digits" result |
|---|---|
| `INV-0042` | `INV-0043` — works, digits are at the end |
| `NBL-1321-DW` | **fails** — the digits aren't at the end; no algorithm can safely guess what "next" means here |
| `invoice` | **fails** — no digits at all |

Since there's no reliable general algorithm, the backend doesn't try. If the
frontend wants to suggest a next number as a convenience, it should use a
conservative regex (`^(.*?)(\d+)$`) against the user's most recently created
document of that type (`GET /documents?type=...&per_page=1`, which is
ordered newest-first for exactly this reason) — and leave the field blank
when the pattern doesn't match, rather than inserting a guess that might be
wrong. A wrong auto-filled value is worse than an empty field.

---

## Auth is full JWT login, not bare API keys

**Decision:** `/auth/register` + `/auth/login` issue an access/refresh token
pair; there's no invoice-generator.com-style single static API key per
account.

**Why:** This is a multi-user web app with a UI people log into, not a
single-tenant integration/API-only tool — sessions need to expire, be
revocable (`/auth/logout`), and be renewable without re-entering a password
(`/auth/refresh`). A bare API key doesn't give you any of that.

**Not yet decided:** Password reset and email verification flows are
missing — flagged in `docs/api-contract.md` as a known gap, not designed yet.

---

## User store uses ASP.NET Core Identity, not a custom user table

**Decision:** User management (the user store, password hashing, and later
account features) is built on **ASP.NET Core Identity**. Our user entity
extends Identity's base user (`IdentityUser<Guid>`) with the business fields
from the OpenAPI `User` schema (`business_name`, `business_address`,
`logo_url`, `default_currency`, `created_at`).

**Why:** Email confirmation and 2FA are on the roadmap (not "maybe someday" —
a real plan). Those are exactly what Identity provides out of the box, along
with password reset and account lockout. Adopting Identity now means those
features are mostly a matter of turning them on later, and avoids a painful
schema/data migration from a hand-rolled store to Identity down the line.

**Rejected alternative — custom user store + borrowed `PasswordHasher<T>`:**
This was the earlier lean (see the previous note in `docs/data-layer.md`),
and it's genuinely lighter *when none of Identity's features are wanted* —
2 clean tables instead of Identity's ~7. It was rejected specifically
because the deferred features (2FA, email) are actually planned; building
them by hand later is security-sensitive work (safe email-confirm links,
2FA codes) and Identity does it for us. If those plans were dropped, the
custom store would become the better choice again.

**Consequence:** Identity brings its own tables (`AspNetUsers`, `AspNetRoles`,
etc.). We won't use roles yet (see the authorization note below), but the
tables exist. This supersedes the custom `users` table sketch that an
earlier version of `docs/data-layer.md` described.

---

## Tokens: short-lived JWT access token + DB-backed refresh token

**Decision:** Login issues two tokens:

- **Access token** — a JWT, short-lived (~15 min), **not stored server-side**,
  validated by signature on every request (stateless).
- **Refresh token** — long-lived (days), **stored (hashed) in PostgreSQL**,
  sent only to `/auth/refresh` to mint a new access token.

ASP.NET Identity handles the user store and passwords; the JWT layer sits on
top of it. Identity + JWT is a deliberate pairing here, not a conflict —
Identity is not used for cookie sessions.

**Why JWT alone isn't enough:** a stateless JWT can't be cancelled — there's
no server record to delete, so it stays valid until it expires. But the API
contract requires `/auth/logout` to *actually* revoke a session (a refresh
token that still works after logout is a real bug, per `docs/conventions.md`).
The refresh token is the deliberately-stateful piece that makes revocation
possible: logout marks its row revoked and it stops working immediately.

The short access-token lifetime keeps the stateless, uncancellable token's
exposure window small; the refresh token keeps the user logged in without
re-entering a password, and keeps logout real. See `docs/data-layer.md`
`refresh_tokens` for why the token is stored hashed, not in plaintext.

---

## Authorization is claims-based on `user_id`, no roles

**Decision:** Authorization is (1) "is the request authenticated?" via
`.RequireAuthorization()`, and (2) "does this row belong to the caller?" via
a `user_id` filter inside handlers. The `user_id` travels as a claim in the
access-token JWT. No roles, no policies, no `AuthorizationHandler`.

**Why:** The app has exactly one access rule — a user may only touch their
own documents. That's a data-filtering concern (`WHERE user_id = @caller`),
not a permission-matrix concern. A non-owner gets **404**, not 403 — matching
the API contract, which hides whether the resource exists at all. Roles and
policies would be ceremony for a system with a single rule and no privilege
levels; add them only if an admin/multi-tier concept ever appears.

---

## Line items are a child table, not a JSON column

**Decision:** `Document.items` is persisted as a separate `line_items` table
with a foreign key to `documents`. In EF Core, `Document` has a normal
`ICollection<LineItem>` navigation property.

**Why:** It's the idiomatic EF Core relational pattern and actually *less*
setup than a JSON column — EF Core's JSON mapping needs extra configuration,
which you'd only take on if you had a concrete reason to want it. We don't:
there's no evidence yet that line items need to be queried independently of
their parent document. A child table also keeps each line individually
addressable if per-line rounding ever needs auditing. Revisit only if line
items turn out to always be read/written as one opaque blob *and* the JSON
form measurably simplifies something.

---

## PDF library is QuestPDF

**Decision:** PDFs are generated with **QuestPDF** (built in C# code, not an
HTML-to-PDF converter).

**Why:** It's free under its Community License at this project's scale,
modern, and code-first (the document layout is plain C#, easy to version and
test). No decision on external templates/engines needed.

---

## PDFs are rendered on demand, never stored

**Decision:** The PDF is generated fresh each time `GET /documents/{id}/pdf`
is called. Nothing is persisted to disk or blob storage. `pdf_url` on the
`Document` response simply points at that endpoint.

**Why:** It removes the entire "where do we store files / how is `pdf_url`
served" question — there is no file to store. The source data lives in the
DB; the PDF is a pure projection of it, so regenerating is always correct and
never stale. Cost is a little CPU per download, which is negligible at this
scale. If rendering ever gets expensive, add caching then — don't pre-store
now.

**Consequence:** the `Document.status` enum (`draft`/`generated`) loses most
of its meaning, since a PDF is always available on demand. Treat documents as
effectively always renderable (set `status = generated`); the distinction is
kept in the contract only for a possible future where rendering is deferred.

---

## PDF generation is synchronous

**Decision:** The PDF is produced inline during the `GET .../pdf` request —
no background job or queue.

**Why:** Simple billing documents render fast; there's nothing to gain from
async infrastructure yet. Offload to a background job only if/when rendering
becomes slow enough to hurt request latency (e.g. very complex templates) —
noted as a future revisit, not a Phase 1 need.
