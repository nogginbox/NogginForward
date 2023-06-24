using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Nogginbox.MailForwarder.Server.Dns;
using Org.BouncyCastle.Asn1.Esf;
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
		var matchedRules = transaction.To
			.Where(t => t != null)
			.Select(t => (email: t, rule:_rules.FirstOrDefault(r => r.IsMatch(t.AsAddress()))))
			.Where(t => t.rule != null && t.rule?.ForwardAddress != null)
			.DistinctBy(t => t.email.AsAddress(), StringComparer.OrdinalIgnoreCase)
			.DistinctBy(r => r.rule?.ForwardAddress.Address, StringComparer.OrdinalIgnoreCase)
			.Select(t => (t.email, rule:t.rule!))
			.ToList();

		if (matchedRules?.Any() != true)
		{
			// Log this
			return SmtpServerResponse.MailboxNameNotAllowed;
		}

		try
		{
			var message = await GetMessageAsync(buffer, cancellationToken);
			var hostGroups = matchedRules.GroupBy(m => m.rule.ForwardAddress.Domain);
		
			foreach (var group in hostGroups)
			{
				var domain = group.Key;
				var forwardAddresses = group.Select(g => g.rule.ForwardAddress).ToList();

				await ForwardEmailAsync(domain, forwardAddresses, context, transaction, message, cancellationToken);
			}
			return SmtpServerResponse.Ok;
		}
		catch(Exception ex)
		{
			// Log this
			return SmtpServerResponse.TransactionFailed;
		}
	}

	private async Task<SmtpServerResponse> ForwardEmailAsync(string mailserverDomain, List<MailboxAddress> forwardRecipients, ISessionContext context, IMessageTransaction transaction, MimeMessage message, CancellationToken cancellationToken)
	{
		var mailServer = (await _dnsFinder.LookupMxServers(mailserverDomain)).FirstOrDefault()
			?? throw new Exception($"No mailserver found for domain '{mailserverDomain}'");
		
		await _smtpClient.ConnectAsync(mailServer, 587, SecureSocketOptions.Auto, cancellationToken);

		// Note: only needed if the SMTP server requires authentication
		//client.Authenticate("joey", "password");
			
		await _smtpClient.SendAsync(message, null, recipients: forwardRecipients, cancellationToken);
		await _smtpClient.DisconnectAsync(true, cancellationToken);
	

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