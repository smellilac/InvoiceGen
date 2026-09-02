using System.Security.Claims;
using InvoiceGen.Api.Common;
using InvoiceGen.Application.Features.Customers;

namespace InvoiceGen.Api.Features.Customers;

public static class CustomerEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/customers").WithTags("customers").RequireAuthorization();

        MapCreate(group);
        MapList(group);
        MapGet(group);
        MapUpdate(group);
        MapDelete(group);
    }

    private static void MapCreate(RouteGroupBuilder group)
    {
        group.MapPost("/", async (
            CreateCustomerRequest request,
            ClaimsPrincipal principal,
            CreateCustomerHandler handler,
            IProblemDetailsService pds,
            HttpContext http,
            CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await handler.HandleAsync(userId.Value, request, ct);
            return result.IsError
                ? await result.ToProblemDetails(pds, http)
                : Results.Created($"/customers/{result.Value.Id}", result.Value);
        })
        .AddEndpointFilter<ValidationFilter<CreateCustomerRequest>>()
        .WithName("CreateCustomer")
        .WithSummary("Create a saved customer");
    }

    private static void MapList(RouteGroupBuilder group)
    {
        group.MapGet("/", async (
            ClaimsPrincipal principal,
            ListCustomersHandler handler,
            IProblemDetailsService pds,
            HttpContext http,
            CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await handler.HandleAsync(userId.Value, ct);
            return result.IsError
                ? await result.ToProblemDetails(pds, http)
                : Results.Ok(result.Value);
        })
        .WithName("ListCustomers")
        .WithSummary("List the current user's active customers (ordered by name)");
    }

    private static void MapGet(RouteGroupBuilder group)
    {
        group.MapGet("/{customerId:guid}", async (
            Guid customerId,
            ClaimsPrincipal principal,
            GetCustomerHandler handler,
            IProblemDetailsService pds,
            HttpContext http,
            CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await handler.HandleAsync(userId.Value, customerId, ct);
            return result.IsError
                ? await result.ToProblemDetails(pds, http)
                : Results.Ok(result.Value);
        })
        .WithName("GetCustomer")
        .WithSummary("Get one customer by id");
    }

    private static void MapUpdate(RouteGroupBuilder group)
    {
        group.MapPatch("/{customerId:guid}", async (
            Guid customerId,
            UpdateCustomerRequest request,
            ClaimsPrincipal principal,
            UpdateCustomerHandler handler,
            IProblemDetailsService pds,
            HttpContext http,
            CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await handler.HandleAsync(userId.Value, customerId, request, ct);
            return result.IsError
                ? await result.ToProblemDetails(pds, http)
                : Results.Ok(result.Value);
        })
        .AddEndpointFilter<ValidationFilter<UpdateCustomerRequest>>()
        .WithName("UpdateCustomer")
        .WithSummary("Update a customer");
    }

    private static void MapDelete(RouteGroupBuilder group)
    {
        group.MapDelete("/{customerId:guid}", async (
            Guid customerId,
            ClaimsPrincipal principal,
            DeleteCustomerHandler handler,
            IProblemDetailsService pds,
            HttpContext http,
            CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await handler.HandleAsync(userId.Value, customerId, ct);
            return result.IsError
                ? await result.ToProblemDetails(pds, http)
                : Results.NoContent();
        })
        .WithName("DeleteCustomer")
        .WithSummary("Soft-delete a customer");
    }
}
