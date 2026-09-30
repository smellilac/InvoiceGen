# Handoff — release/0.2.0 (Search endpoint wired + pgvector WIP)

## Completed
- [x] **Search endpoint wired**, committed as `8138029`.
  - New `src/InvoiceGen.Api/Features/Search/SearchEndpoints.cs` — `POST /api/search`,
    `RequireAuthorization()`. Thin proxy over the Python `InvoiceGen.Search` service:
    pulls the caller's raw bearer token and forwards it verbatim so the service
    authorizes as the same user (results scoped to their docs). No token -> 401.
  - Transport-failure mapping: service 401 -> 401 (pass-through), unreachable
    (`StatusCode is null`) -> 503, non-success status -> 502, our timeout
    (caller didn't cancel) -> 503.
  - Registered via `SearchEndpoints.Map(app)` in `Common/EndpointExtensions.cs`.
  - `SearchServiceClient`: expanded `SearchRequest` with optional filters
    (`CustomerId`, `Status`, `MinAmount`/`MaxAmount` as **decimal**, `DateFrom`/`DateTo`).
    Explicit `JsonSerializerOptions` — snake_case naming, omit-null, snake_case enum
    converter — because ASP.NET's global JSON opts don't apply to `HttpClient`.
- [x] **SearchServiceClient** base (`2a5208c`) — typed `HttpClient`, DI via
  `AddHttpClient<SearchServiceClient>`, `Search:BaseUrl` (default `http://localhost:8000`).
- [x] **pgvector support** (`b87ec24`) — embedding columns on
  `Document`/`LineItem`/`Customer`, migration generated **NOT applied**.
- [x] **Vector dim -> 1024** (`132d3cc`) — switched from Qwen3-Embedding-8B (4096)
  to **Qwen3-Embedding-0.6B (1024)**. Migration + designer + snapshot + all three
  entity configs updated in lockstep so the EF model matches the schema.
- [x] **DocumentTypes refactor** (`d6560fe`) — static `DocumentTypesExtensions.GetAll()`.

## Pending
- [ ] **Extract `ISearchClient` into Application** when search grows beyond the
  proxy (kept concrete for now — idiomatic for `AddHttpClient<T>`). Confirm the
  Python `/search` request/response contract matches the current DTOs.
- [ ] **Apply the migration when ready** — target Postgres needs pgvector
  (`CREATE EXTENSION vector`). Do NOT auto-apply blindly on Render.
- [ ] **Design-time connection string** — `appsettings*.json` `DefaultConnection`
  is empty; `dotnet ef` needs a real one (user-secrets/env).
- [ ] **Triage 202 analyzer warnings** — `TreatWarningsAsErrors=true` will fail builds.
- [ ] **Build + run integration tests** — endpoint not yet compiled/tested here.

## Context
- Branch: `release/0.2.0`
- Latest checkpoint: `132d3cc` (vector dim -> 1024)
- Working tree: clean apart from this handoff note.
