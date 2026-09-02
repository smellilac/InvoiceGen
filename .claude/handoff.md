# Handoff — feature/saled-order-generation-implementation

## Completed
- [x] **Spec 0.6.0 code alignment:** renamed amount_paid->amount_settled,
      balance_due->balance_remaining (entity/DTOs/handler/EF/Recalculate); added
      related_document_number (free-text, like number). PDF: type-aware settlement labels
      (credit_note = Refunded/Balance remaining) + "Re: <related>" header. Migration regenerated.
      Domain unit tests 3/3 pass (no Docker).
- [x] **per_page spec fixed:** openapi max 100 -> 30 (matches code).
- [x] **SalesOrder type added (spec 0.5.0):** DocumentType.SalesOrder + "Sales Order" label
      (DocumentTypeApi.ToDisplayName), flows through /document-types (id sales_order). Enum-only,
      no new fields; billing-style so existing priced PDF layout applies. Test asserts 12 types.
- [x] **CRITICAL resolved:** Document.Recalculate() now discounts per line + rounds, then taxes
      the discounted amount + rounds, per-line summed (openapi x-rounding-policy.discount_application).
      Subtotal stays PRE-discount.
- [x] Domain unit tests (run WITHOUT Docker — first test-verified thing since Testcontainers switch):
      worked example (subtotal 4520.00, total 5195.74 = 4294.00 + 901.74), per-line rounding (0.18),
      no-discount baseline — 3/3 passing.
- [x] Currency-aware PDF formatting (MoneyFormatter) + spec 0.4.0 policies (earlier commits)
- [x] Build: GREEN. Checkpoint: 348801e
- [x] **Integration suite: RUN and green** (2026-09-02, first real execution since the
      Testcontainers switch). `dotnet test InvoiceGen.slnx` on master (7247f52, Docker):
      **31/31 passed**, 0 failed/skipped, ~20s. postgres:16-alpine via Testcontainers,
      real EF migrations applied cleanly. No fixes needed — the 0.6.0 renames
      (amount_settled/balance_remaining, related_document_number), the /document-types
      count (12), and Postgres-vs-SQLite behavior all held. Auth (~11) + Documents (~11,
      incl. PDF smoke + packing-slip) + DocumentTypes (6) + Domain (3) = 31.

## Pending / next steps
- [ ] **Follow-up — expose the breakdown in Document response.** Returns subtotal + total but not
      discount/tax. Percents are insufficient (per-line tax + no line items in response = client
      can't recompute). Add computed `discount_amount` + `tax_amount` (stored, rounded) to the
      Document schema + entity. Consider storing them in Recalculate() while it already computes them.
- [ ] Customers feature (0.3.0 docs) — not yet implemented in code (soft-delete, frozen `to`).
- [ ] Follow-up still open: expose discount_amount/tax_amount in Document response (per-line
      tax + no line items in response = client can't recompute; return computed amounts).
- [ ] Deferred hardening: refresh-token reuse detection, expired-token cleanup, ExecuteDelete
      for delete, PDF text-extraction test to assert packing slip hides pricing.

## Learned / non-obvious
- Domain math is unit-testable with zero infra (new Document{...}; Recalculate()) — no Docker.
  Prefer this for calculation rules over the Testcontainers integration path.
- openapi.yaml carries persistent EOL churn; verify real changes with `git diff --ignore-all-space`.
- Testcontainers 4.14 ctor + xUnit v2 IAsyncLifetime disposal gotchas (see 45a6ea8).

## Context
- Branch: feature/saled-order-generation-implementation
- Checkpoint: f180bce  (prev: 064e758 sales_order, 348801e recalc fix, 45a6ea8 documents feature)
