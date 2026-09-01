# Data Layer

**Status: Auth tables decided; Documents tables still open.** This file
tracks real schema decisions as they're made — update it the same day a
decision lands, don't let it drift from what's actually implemented.

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
| `SecurityStamp`, `ConcurrencyStamp`, `TwoFactorEnabled`, `LockoutEnd`, … | Identity | standard Identity columns; unused now, groundwork for 2FA/lockout later |
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

## Not yet decided

- Whether `Document.items` (line items) is a child table or a JSON column —
  a child table is more consistent with treating each line item as
  addressable data (e.g. if per-line rounding details ever need to be
  queried/audited); a JSON column is simpler if line items are always
  read/written as a whole with the parent document. Revisit once querying
  needs are clearer. Doesn't block Auth work.
- Indexing strategy for `GET /documents` (needs to support: filter by
  `user_id` + `type`, sort by `created_at` descending, paginate). Also
  doesn't block Auth work.
