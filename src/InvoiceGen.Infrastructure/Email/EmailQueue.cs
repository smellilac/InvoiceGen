using System.Threading.Channels;
using InvoiceGen.Application.Features.Documents;

namespace InvoiceGen.Infrastructure.Email;

// In-process send queue: an unbounded Channel. The endpoint writes (EnqueueAsync) and
// the EmailSendingWorker reads. Singleton. Swap for a broker if you outgrow one instance.
public sealed class EmailQueue : IEmailQueue
{
    private readonly Channel<EmailSendJob> _channel = Channel.CreateUnbounded<EmailSendJob>();

    public ValueTask EnqueueAsync(EmailSendJob job, CancellationToken cancellationToken) =>
        _channel.Writer.WriteAsync(job, cancellationToken);

    public ChannelReader<EmailSendJob> Reader => _channel.Reader;
}
