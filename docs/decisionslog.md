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
