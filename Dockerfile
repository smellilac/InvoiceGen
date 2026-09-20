# syntax=docker/dockerfile:1

# ---- Build stage -----------------------------------------------------------
# The project targets net10.0 (see Directory.Build.props), so the SDK/runtime
# images are 10.0 — NOT 8.0. An 8.0 SDK cannot restore or build a net10.0 app.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only the manifests first so `dotnet restore` is cached independently of
# source changes. Directory.Build.props is imported by every project (it sets
# TargetFramework), so it must be present before restore.
COPY Directory.Build.props ./
COPY src/InvoiceGen.Api/InvoiceGen.Api.csproj                     src/InvoiceGen.Api/
COPY src/InvoiceGen.Application/InvoiceGen.Application.csproj     src/InvoiceGen.Application/
COPY src/InvoiceGen.Infrastructure/InvoiceGen.Infrastructure.csproj src/InvoiceGen.Infrastructure/
COPY src/InvoiceGen.Domain/InvoiceGen.Domain.csproj              src/InvoiceGen.Domain/

# Restore the API and everything it transitively references (Application,
# Infrastructure, Domain). The test project is intentionally excluded.
RUN dotnet restore src/InvoiceGen.Api/InvoiceGen.Api.csproj

# Now copy the rest of the source. Changes here don't invalidate the restore layer.
COPY src/ src/

RUN dotnet publish src/InvoiceGen.Api/InvoiceGen.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ---- Runtime stage ---------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# QuestPDF renders PDFs via SkiaSharp, which needs libfontconfig1 at runtime.
# The slim aspnet image doesn't ship it, so /documents/{id}/pdf would throw
# without this. Clean up apt lists to keep the layer small.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libfontconfig1 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish ./

# Run as the non-root user baked into the aspnet image.
USER app

# Render injects the listen port via $PORT at runtime (not fixed). Bind Kestrel
# to it via a shell-form entrypoint so the variable is expanded at start time;
# default to 8080 when PORT is unset (e.g. local `docker run`).
ENTRYPOINT ["/bin/sh", "-c", "ASPNETCORE_URLS=http://+:${PORT:-8080} exec dotnet InvoiceGen.Api.dll"]
