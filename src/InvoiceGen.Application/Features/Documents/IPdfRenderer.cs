using InvoiceGen.Domain.Entities;

namespace InvoiceGen.Application.Features.Documents;

public interface IPdfRenderer
{
    byte[] Render(Document document);
}

public sealed record PdfFile(byte[] Content, string FileName);
