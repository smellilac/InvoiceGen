using InvoiceGen.Api.Common;
using InvoiceGen.Application.Common;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddInvoiceGenApplicationLayer();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.MapEndpoints();

app.Run();

public partial class Program { }
