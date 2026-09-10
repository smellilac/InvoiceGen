using System.Security.Claims;
using InvoiceGen.Api.Common;
using InvoiceGen.Application.Features.Documents;

namespace InvoiceGen.Api.Features.Documents;

public static class DocumentEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/documents").WithTags("documents").RequireAuthorization();

        MapCreate(group);
        MapList(group);
        MapGet(group);
        MapDelete(group);
        MapGetPdf(group);
        MapSend(group);
        MapRecordSettlement(group);
    }

    private static void MapCreate(RouteGroupBuilder group)
    {
        group.MapPost("/", async (
            CreateDocumentRequest request,
            ClaimsPrincipal principal,
            CreateDocumentHandler handler,
            IProblemDetailsService pds,
            HttpContext http,
            CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await handler.HandleAsync(userId.Value, request, ct);
            return result.IsError
                ? await result.ToProblemDetails(pds, http)
                : Results.Created($"/documents/{result.Value.Id}", result.Value);
        })
        .AddEndpointFilter<ValidationFilter<CreateDocumentRequest>>()
        .WithName("CreateDocument")
        .WithSummary("Create a document from the submitted form");
    }

    private static void MapList(RouteGroupBuilder group)
    {
        group.MapGet("/", async (
            string? type,
            Guid? customer_id,
            int? page,
            int? per_page,
            ClaimsPrincipal principal,
            ListDocumentsHandler handler,
            IProblemDetailsService pds,
            HttpContext http,
            CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            Domain.Enums.DocumentType? typeFilter = null;
            if (!string.IsNullOrWhiteSpace(type))
            {
                if (!DocumentTypeApi.TryParse(type, out var parsed))
                    return Results.Problem(statusCode: 422, title: "invalid_type",
                        detail: $"Unknown document type '{type}'.");
                typeFilter = parsed;
            }

            var result = await handler.HandleAsync(userId.Value, typeFilter, customer_id, page ?? 1, per_page ?? 10, ct);
            return result.IsError
                ? await result.ToProblemDetails(pds, http)
                : Results.Ok(result.Value);
        })
        .WithName("ListDocuments")
        .WithSummary("List the current user's documents (newest first, paginated)");
    }

    private static void MapGet(RouteGroupBuilder group)
    {
        group.MapGet("/{documentId:guid}", async (
            Guid documentId,
            ClaimsPrincipal principal,
            GetDocumentHandler handler,
            IProblemDetailsService pds,
            HttpContext http,
            CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await handler.HandleAsync(userId.Value, documentId, ct);
            return result.IsError
                ? await result.ToProblemDetails(pds, http)
                : Results.Ok(result.Value);
        })
        .WithName("GetDocument")
        .WithSummary("Get one document by id");
    }

    private static void MapDelete(RouteGroupBuilder group)
    {
        group.MapDelete("/{documentId:guid}", async (
            Guid documentId,
            ClaimsPrincipal principal,
            DeleteDocumentHandler handler,
            IProblemDetailsService pds,
            HttpContext http,
            CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await handler.HandleAsync(userId.Value, documentId, ct);
            return result.IsError
                ? await result.ToProblemDetails(pds, http)
                : Results.NoContent();
        })
        .WithName("DeleteDocument")
        .WithSummary("Delete a document from history");
    }

    private static void MapGetPdf(RouteGroupBuilder group)
    {
        group.MapGet("/{documentId:guid}/pdf", async (
            Guid documentId,
            ClaimsPrincipal principal,
            GetDocumentPdfHandler handler,
            IProblemDetailsService pds,
            HttpContext http,
            CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await handler.HandleAsync(userId.Value, documentId, ct);
            return result.IsError
                ? await result.ToProblemDetails(pds, http)
                : Results.File(result.Value.Content, "application/pdf", result.Value.FileName);
        })
        .WithName("DownloadDocumentPdf")
        .WithSummary("Download the document as a PDF (rendered on demand)");
    }

    private static void MapSend(RouteGroupBuilder group)
    {
        // Body is optional (empty is valid when the document has a customer with an email),
        // so the parameter is nullable → an empty body binds to null.
        group.MapPost("/{documentId:guid}/send", async (
            Guid documentId,
            SendDocumentRequest? request,
            ClaimsPrincipal principal,
            SendDocumentHandler handler,
            IProblemDetailsService pds,
            HttpContext http,
            CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await handler.HandleAsync(userId.Value, documentId, request ?? new SendDocumentRequest(null, null), ct);
            return result.IsError
                ? await result.ToProblemDetails(pds, http)
                : Results.Accepted($"/documents/{result.Value.Id}", result.Value);
        })
        .AddEndpointFilter<ValidationFilter<SendDocumentRequest>>()
        .RequireRateLimiting("auth") // same per-IP limiter as /auth (x-email-delivery-policy)
        .WithName("SendDocument")
        .WithSummary("Email the document's PDF to a customer (async — returns 202)");
    }

    private static void MapRecordSettlement(RouteGroupBuilder group)
    {
        // Distinct sub-resource, not a general PATCH on /documents/{id} (which deliberately
        // has none). Adds `amount` (may be negative) to amount_settled, clamped to [0, total].
        group.MapPost("/{documentId:guid}/settlement", async (
            Guid documentId,
            RecordSettlementRequest request,
            ClaimsPrincipal principal,
            RecordSettlementHandler handler,
            IProblemDetailsService pds,
            HttpContext http,
            CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await handler.HandleAsync(userId.Value, documentId, request, ct);
            return result.IsError
                ? await result.ToProblemDetails(pds, http)
                : Results.Ok(result.Value);
        })
        .WithName("RecordSettlement")
        .WithSummary("Record a payment or refund against a document (adjusts amount_settled)");
    }
}
