# Handoff — feature/swagger (Scalar API docs)

## Completed
- [x] **Added Scalar API reference UI** over .NET 10 built-in OpenAPI (commit b2241f4):
      - Added `Scalar.AspNetCore` 2.17.2 package to InvoiceGen.Api.
      - New `Common/OpenApiExtensions.cs`: `AddInvoiceGenOpenApi()` registers the OpenAPI doc
        plus a `BearerSecuritySchemeTransformer` (Http/bearer/JWT) so the doc advertises auth and
        Scalar's "Try It" gets an Authorize affordance.
      - `Program.cs`: `AddInvoiceGenOpenApi()` replaces bare `AddOpenApi()`; `MapScalarApiReference`
        mounted at `/scalar`, **development-only** (same guard as `MapOpenApi`), title "InvoiceGen API",
        `AddPreferredSecuritySchemes("Bearer")`.
- [x] Fixed 3 build errors the user hit (skill examples were for an older Scalar API):
      - CS8602: init `Components.SecuritySchemes ??= new Dictionary<...>()` before indexing.
      - CS0618: `WithPreferredScheme` → `AddPreferredSecuritySchemes`.
      - CS8625: dropped `WithProxy(null)` — proxy is off by default in 2.17.2.
- [x] Verified: `dotnet build` = **Build succeeded, 0 errors** (built to isolated `-o` dir to
      bypass a bin file-lock; see below).

## Pending / next steps
- [ ] Run the app and eyeball `/scalar` in a browser (dev env). Confirm Authorize→Bearer flow works
      against a real login token.
- [ ] Optional: update `docs/development.md` (currently a placeholder) to point at `/scalar` for the
      manual contract check it references. Left untouched for now.
- [ ] Consider whether docs should be reachable in non-dev with `.RequireAuthorization()` — currently
      dev-only by design.

## Learned / gotchas
- **In-place `dotnet build` on this repo fails with MSB3021 "Access to the path ... is denied"** when
  the API is running / bin DLLs are locked (WSL building onto /mnt/c while a Windows process holds
  them). It is NOT a code error — build to an isolated `-o <dir>` to verify compilation, or stop the
  running app first.
- Scalar 2.17.2 API differs from the kit's `scalar` skill examples (`WithPreferredScheme` obsolete,
  `WithProxy(null)` won't compile).

## Context
- Branch: feature/swagger | Checkpoint: b2241f4
- Unrelated pre-existing working-tree changes (docs/apicontract.md, docs/conventions.md, appsettings*,
  DependencyInjection.cs) were left unstaged — not part of this feature.
