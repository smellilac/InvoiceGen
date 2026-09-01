# Data Layer

**Status: mostly undecided.** This file is a placeholder more than a
reference right now — fill it in as real choices get made, and delete this
warning once it's no longer accurate.

## Decided

- ORM assumption: EF Core (standard choice for ASP.NET Core; not formally
  confirmed, just the default assumption until stated otherwise).
- Monetary columns must be a fixed-point decimal column type (e.g. SQL
  Server/PostgreSQL `decimal(18,2)` or similar precision) — never a
  floating-point column type (`float`/`real`). Mirrors the `decimal`-in-C#
  rule in `docs/conventions.md`; the two must agree, or precision is lost at
  the ORM boundary regardless of what the C# type says.

## Not yet decided

- Database engine: SQL Server vs. PostgreSQL vs. something else.
- Migration strategy (EF Core Migrations vs. something else).
- Whether `Document.items` (line items) is a child table or a JSON column —
  a child table is more consistent with treating each line item as
  addressable data (e.g. if per-line rounding details ever need to be
  queried/audited); a JSON column is simpler if line items are always
  read/written as a whole with the parent document. Revisit once querying
  needs are clearer.
- Indexing strategy for `GET /documents` (needs to support: filter by
  `user_id` + `type`, sort by `created_at` descending, paginate).
