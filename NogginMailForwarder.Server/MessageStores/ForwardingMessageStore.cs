using SmtpServer;
using SmtpServer.Mail;
using SmtpServer.Protocol;
using SmtpServer.Storage;
using System.Buffers;

namespace NogginMailForwarder.Server.MessageStores;

/// <summary>
/// Forwards the email if the recipient matched an alias in one of the configured forward rules.
/// </summary>
public class ForwardingMessageStore : MessageStore
{
	private readonly IReadOnlyList<ForwardRule> _rules;

	public ForwardingMessageStore(IReadOnlyList<ForwardRule> rules)
	{
		_rules = rules;
	}

	public override async Task<SmtpResponse> SaveAsync(ISessionContext context, IMessageTransaction transaction, ReadOnlySequence<byte> buffer, CancellationToken cancellationToken)
	{
		var rules = transaction.To
			.Select(t => _rules.FirstOrDefault(r => r.IsMatch(t.AsAddress())))
			.Where(t => t != null)
			.Select(t => t!)
			.ToList();

		if(rules?.Any() != true)
		{
			// Log this
			return SmtpResponse.MailboxNameNotAllowed;
		}

		await using var stream = new MemoryStream();

		var position = buffer.GetPosition(0);
		while (buffer.TryGet(ref position, out var memory))
		{
			await stream.WriteAsync(memory, cancellationToken);
		}

		stream.Position = 0;

		var message = await MimeKit.MimeMessage.LoadAsync(stream, cancellationToken);
		Console.WriteLine(message.TextBody);

		return SmtpResponse.Ok;
	}
}