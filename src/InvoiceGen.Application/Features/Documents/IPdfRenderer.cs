using InvoiceGen.Domain.Entities;

namespace InvoiceGen.Application.Features.Documents;

public interface IPdfRenderer
{
    // `logo` is the decoded image bytes for document.LogoUrl (resolved by the caller), or null
    // when the document has no frozen logo or its bytes are no longer available.
    byte[] Render(Document document, byte[]? logo = null);
}

public sealed record PdfFile(byte[] Content, string FileName);
