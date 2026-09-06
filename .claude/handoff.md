# Handoff — fix/get-document-id-contract (Document response round-trip)

## Completed
- [x] **`DocumentDto` now round-trips `CreateDocumentRequest` inputs** so a GET
      returns enough to prefill the create form. Added, reusing POST's exact
      names/shapes: `Date`, `DueDate`, `Items` (as `IReadOnlyList<CreateLineItemRequest>`),
      `TaxPercent`, `DiscountPercent`, `ShippingAmount`, `Notes`, `Terms`.
      `CreateDocumentRequest` itself is untouched (additive to the response only).
- [x] **Loaded line items where the shared DTO needed them** — added
      `.Include(d => d.Items)` to `GetDocumentHandler`, `ListDocumentsHandler`,
      `SendDocumentHandler` (Create already had items in memory). Without this
      those responses would have returned an empty `items` array.
- [x] **OpenAPI `Document` schema updated to match** (`docs/openapi.yaml`):
      added the 8 input fields + documented `discount_amount`/`tax_amount`
      (already returned by the impl, never previously in the spec). Version
      bumped `0.8.0 → 0.9.0` with a changelog entry.
- [x] **Tests green** — added `Get_RoundTripsCreateInputs` and
      `List_IncludesLineItems`. DocumentsTests 13/13, EmailSendTests 7/7 pass
      (built + run via isolated `-o` dir to dodge the WSL bin lock).

## Pending / next steps
- [ ] **Regenerate the frontend's API types against `docs/openapi.yaml` (0.9.0).**
      BLOCKED: no frontend in this repo (backend-only: `src`/`docs`/`tests`, no
      codegen tooling). Need the Angular repo path, or Dima regenerates it there.
      The Document-schema diff was surfaced to Dima for the frontend spec copy.
- [ ] (separate thread) PDF restyle in `src/InvoiceGen.Infrastructure/Pdf/PdfRenderer.cs`
      is still modified/uncommitted — not part of this change; see git stash/diff.
- [ ] (separate thread) CRLF-only churn in `docs/apicontract.md` + `docs/conventions.md`
      left unstaged on purpose (`git diff --ignore-all-space` = empty).

## Learned / gotchas
- **`DocumentDto` is shared by Get/List/Create/Send** — enriching it changes all
  four responses at once, and any handler not `.Include`-ing `Items` would emit
  an empty `items` array. Kept a single `Document` schema in OpenAPI to match.
- **In-place `dotnet build`/`test` fails with MSB3021 "Access to the path ...
  denied"** (WSL bin lock on /mnt/c). Build/test to an isolated `-o <dir>`.

## Context
- Branch: fix/get-document-id-contract | Checkpoint: <this commit>
