# Data Layer

**Status: Auth + Documents + Customers schema decided.** This file tracks real schema
decisions as they're made — update it the same day a decision lands, don't
let it drift from what's actually implemented.

## Decided

- Database engine: **PostgreSQL** — chosen for low-cost hosting (free/cheap
  serverless tiers on Neon or Supabase), first-class EF Core support via
  Npgsql, and no licensing cost as usage grows.
- ORM: **EF Core**, via `Npgsql.EntityFrameworkCore.PostgreSQL`. Confirmed
  when Auth scaffolding started.
- Migration strategy: **EF Core Migrations** (`dotnet ef migrations`).
- User store: **ASP.NET Core Identity** (see `docs/decisions-log.md`). The
  `DbContext` is an `IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>`,
  so Identity's tables (`AspNetUsers`, `AspNetRoles`, `AspNetUserClaims`,
  `AspNetUserLogins`, `AspNetUserTokens`, `AspNetUserRoles`,
  `AspNetRoleClaims`) are created by its migration. Roles are unused for now
  but the tables exist.
- Password hashing: **Identity's built-in `PasswordHasher<AppUser>`
  (PBKDF2)** — resolved by the decision to adopt full Identity. BCrypt/Argon2
  are no longer under consideration.
- Monetary columns must be a fixed-point decimal column type — Postgres
  `numeric` (EF Core maps C# `decimal` to this by default). Never `float`/
  `real`/`double precision`. Mirrors the `decimal`-in-C# rule in
  `docs/conventions.md`; the two must agree, or precision is lost at the ORM
  boundary regardless of what the C# type says.

### `AspNetUsers` (Identity `AppUser`)

`AppUser : IdentityUser<Guid>`. Identity supplies the identity/credential
columns; we add the business-profile columns from the OpenAPI `User` schema.
The `password_hash`, `email`, and `id` columns are **provided by Identity**
(as `PasswordHash`, `Email`/`NormalizedEmail`, `Id`), not hand-defined.

| Column | Source | Notes |
|---|---|---|
| `Id` | Identity | `uuid` PK (`IdentityUser<Guid>`) |
| `Email` / `NormalizedEmail` | Identity | unique index via `NormalizedEmail`; backs login and the 409-on-duplicate-register behavior |
| `UserName` / `NormalizedUserName` | Identity | set equal to email at registration (no separate username concept) |
| `PasswordHash` | Identity | PBKDF2 hash; never returned via the API — deliberately absent from the OpenAPI `User` schema |
| `LockoutEnd`, `AccessFailedCount`, `LockoutEnabled` | Identity | **in use** — login enforces lockout (5 failed attempts → 5-minute lockout); new users get `LockoutEnabled = true` |
| `SecurityStamp`, `ConcurrencyStamp`, `TwoFactorEnabled`, … | Identity | standard Identity columns; unused now, groundwork for 2FA/email later |
| `business_name` | **added** | `text`, nullable |
| `business_address` | **added** | `text`, nullable, multiline |
| `logo_url` | **added** | `text`, nullable |
| `default_currency` | **added** | `char(3)`, nullable, ISO 4217 |
| `created_at` | **added** | `timestamptz` |

### `refresh_tokens`

A **dedicated custom table**, not Identity's `AspNetUserTokens` (which is
meant for external-login/2FA tokens). Our refresh-token rotation and
revocation model needs its own `expires_at`/`revoked_at` columns, so it's
kept separate.

| Column | Type | Notes |
|---|---|---|
| `id` | `uuid` | PK |
| `user_id` | `uuid` | FK → `users.id` |
| `token_hash` | `text`, unique index | **the hash of the token, never the raw value.** A refresh token is a long-lived bearer credential — storing it in plaintext means a database read (backup leak, injection, etc.) hands out valid logins directly. SHA-256 is sufficient here (unlike passwords, refresh tokens are already high-entropy random values, so slow hashing like bcrypt/Argon2 isn't needed). |
| `expires_at` | `timestamptz` | |
| `revoked_at` | `timestamptz`, nullable | set on `/auth/logout`; a non-null value means the token no longer works even if `expires_at` hasn't passed |
| `created_at` | `timestamptz` | |

Index `user_id` too — not required for Phase 1, but cheap to add now and
needed the moment a "log out everywhere" feature exists.

### `customers`

Added in 0.3.0. Saved customer records a document can reference by
`customer_id` instead of retyping `to`. **Soft-deleted, never hard-deleted**
(see `docs/decisions-log.md`): a `deleted_at` timestamp hides the row from
`GET /customers` and blocks new references, but keeps historical documents'
`customer_id` valid.

| Column | Type | Notes |
|---|---|---|
| `id` | `uuid` | PK |
| `user_id` | `uuid` | FK → `AspNetUsers.Id`; every query filters on this (ownership) |
| `name` | `text` | required |
| `email` | `text`, nullable | |
| `address` | `text`, nullable | multiline |
| `phone` | `text`, nullable | |
| `notes` | `text`, nullable | private, never shown on documents |
| `created_at` / `updated_at` | `timestamptz` | `GET /customers` is ordered by `name` ASC |
| `deleted_at` | `timestamptz`, nullable | soft-delete flag; non-null → excluded from listing and unusable for new documents |

Filtered index / query filter on `deleted_at IS NULL` for the common
"list active customers" path.

### `documents`

Line items live in a **child table** (`line_items`), not a JSON column —
see `docs/decisions-log.md`. In EF Core, `Document` has an
`ICollection<LineItem>` navigation. Monetary totals are stored as computed
`numeric` values (see the money/tax rules in `docs/conventions.md`).

| Column | Type | Notes |
|---|---|---|
| `id` | `uuid` | PK |
| `user_id` | `uuid` | FK → `AspNetUsers.Id`; every query filters on this (ownership) |
| `type` | `text`/enum | invoice, receipt, quote, … |
| `status` | `text` | `draft`/`generated`; largely nominal (PDF is on-demand) |
| `number` | `text`, nullable | free text, never generated/validated server-side |
| `related_document_number` | `text`, nullable | free text (same never-validated policy as `number`); a reference to another document, e.g. the invoice a `credit_note` credits. Not an FK — may point outside this system |
| `customer_id` | `uuid`, nullable | FK → `customers.id`; a **reference only**, kept even after the customer is soft-deleted. Does *not* keep `to` in sync — see the snapshot rule in `docs/decisions-log.md`. Index it (backs `GET /documents?customer_id=...`) |
| `from` / `to` | `text` | billing org / customer (multiline free text). `to` is a **frozen snapshot** — auto-filled from the customer at creation if omitted, never rewritten afterward |
| `currency` | `text` | e.g. USD |
| `subtotal`, `discount_amount`, `tax_amount`, `total`, `amount_settled`, `balance_remaining` | `numeric(18,2)` | decimal money — never float. All computed + stored by `Document.Recalculate()`. `subtotal` is PRE-discount; `discount_amount` = subtotal − discounted; `tax_amount` = sum of per-line tax; totals computed discount-first, then per-line tax on the discounted amount (see `docs/decisions-log.md`). `discount_amount`/`tax_amount` are stored so a client can reconcile the response without the line items (which the response omits). `amount_settled`/`balance_remaining` mean paid/owed for most types, refunded/unrefunded for `credit_note` — see settlement policy |
| `tax_percent`, `discount_percent`, `shipping_amount` | `numeric(18,2)` | inputs to the totals |
| `notes` / `terms` | `text`, nullable | |
| `created_at` / `updated_at` | `timestamptz` | list is ordered `created_at` DESC |

### `line_items`

A child table (one-to-many from `documents`), configured via
`LineItemConfiguration` + `DocumentConfiguration.HasMany(x => x.Items)`.
`line_total` (`quantity × unit_cost`) is **computed in code and NOT stored** —
it's a C# expression on the entity, mapped out with `Ignore(...)`. Totals are
derived from these rows by `Document.Recalculate()`.

| Column | Type | Notes |
|---|---|---|
| `id` | `uuid` | PK |
| `document_id` | `uuid` | FK → `documents.id`, **cascade delete** (delete a document → its line items go) |
| `name` | `varchar(500)` | required |
| `description` | `varchar(2000)`, nullable | |
| `quantity` | `numeric(18,4)` | supports fractional units (e.g. hours) |
| `unit_cost` | `numeric(18,2)` | decimal money — never float |
| `reference` | `varchar(200)`, nullable | |

Not exposed in the `Document` API response (matches the OpenAPI schema); the
PDF renderer loads them explicitly with `Include(d => d.Items)`.

## Not yet decided

- Indexing strategy for `GET /documents` (needs to support: filter by
  `user_id` + `type`, sort by `created_at` descending, paginate). Also
  doesn't block Auth work.
