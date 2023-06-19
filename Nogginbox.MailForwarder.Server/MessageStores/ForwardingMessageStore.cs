using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Nogginbox.MailForwarder.Server.Dns;
using SmtpServer;
using SmtpServer.Mail;
using SmtpServer.Storage;
using System.Buffers;
using SmtpServerResponse = SmtpServer.Protocol.SmtpResponse;

namespace Nogginbox.MailForwarder.Server.MessageStores;

/// <summary>
/// Forwards the email if the recipient matched an alias in one of the configured forward rules.
/// </summary>
public class ForwardingMessageStore : MessageStore
{
	private readonly IDnsMxFinder _dnsFinder;
	private readonly IReadOnlyList<ForwardRule> _rules;
	private readonly ISmtpClient _smtpClient;

	public ForwardingMessageStore(IReadOnlyList<ForwardRule> rules, IDnsMxFinder dnsFinder, ISmtpClient smtpClient)
	{
		_dnsFinder = dnsFinder;
		_rules = rules;
		_smtpClient = smtpClient;
	}

	public override async Task<SmtpServerResponse> SaveAsync(ISessionContext context, IMessageTransaction transaction, ReadOnlySequence<byte> buffer, CancellationToken cancellationToken)
	{
		var rules = transaction.To
			.Select(t => (email: t, rule:_rules.FirstOrDefault(r => r.IsMatch(t.AsAddress()))))
			.Where(t => t.rule != null)
			.Select(t => (t.email, rule:t.rule!))
			.ToList();

		if (rules?.Any() != true)
		{
			// Log this
			return SmtpServerResponse.MailboxNameNotAllowed;
		}
		
		var emails = rules.DistinctBy(r => r.rule.ForwardAddress, StringComparer.OrdinalIgnoreCase);
		var message = await GetMessageAsync(buffer, cancellationToken);
		foreach (var email in emails)
		{
			await ForwardEmailAsync(email.email, email.rule, context, transaction, message, cancellationToken);
		}

		return SmtpServerResponse.SizeLimitExceeded;
	}

	private async Task<SmtpServerResponse> ForwardEmailAsync(IMailbox mailbox, ForwardRule rule, ISessionContext context, IMessageTransaction transaction, MimeMessage message, CancellationToken cancellationToken)
	{
		var toHost = rule.ForwardAddress.Split('@')[1];
		var mailServer = (await _dnsFinder.LookupMxServers(toHost)).FirstOrDefault()
			?? throw new Exception($"No mailserver found for domain '{toHost}'");
		
		_smtpClient.Connect(mailServer, 587, SecureSocketOptions.Auto, cancellationToken);

		// Note: only needed if the SMTP server requires authentication
		//client.Authenticate("joey", "password");

		var forwardRecipients = new List<MailboxAddress>
		{
			new MailboxAddress($"Aliased from {mailbox.AsAddress()}", rule.ForwardAddress)
		};
			
		_smtpClient.Send(message, null, recipients: forwardRecipients, cancellationToken);
		_smtpClient.Disconnect(true, cancellationToken);
	

		return SmtpServerResponse.SizeLimitExceeded;
	}

	private async static Task<MimeMessage> GetMessageAsync(ReadOnlySequence<byte> buffer, CancellationToken cancellationToken)
	{
		using var stream = new MemoryStream();

		var position = buffer.GetPosition(0);
		while (buffer.TryGet(ref position, out var memory))
		{
			await stream.WriteAsync(memory, cancellationToken);
		}
		stream.Position = 0;

		var message = await MimeMessage.LoadAsync(stream, cancellationToken);

		return message;
	}
}