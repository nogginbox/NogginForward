using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;
using Nogginbox.MailForwarder.Server.Dns;
using SmtpServer;
using SmtpServer.Mail;
using SmtpServer.Storage;
using System.Buffers;
using Logging = Microsoft.Extensions.Logging;
using SmtpServerResponse = SmtpServer.Protocol.SmtpResponse;

namespace Nogginbox.MailForwarder.Server.MessageStores;

/// <summary>
/// Forwards the email if the recipient matched an alias in one of the configured forward rules.
/// </summary>
public class ForwardingMessageStore : MessageStore
{
	private readonly IDnsMxFinder _dnsFinder;
	private readonly Logging.ILogger _log;
	private readonly IReadOnlyList<ForwardRule> _rules;
	private readonly ISmtpClient _smtpClient;

	public ForwardingMessageStore(IReadOnlyList<ForwardRule> rules, IDnsMxFinder dnsFinder, ISmtpClient smtpClient, Logging.ILogger log)
	{
		_dnsFinder = dnsFinder;
		_log = log;
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
			_log.LogInformation("No rules in rulset({rulesetcount}) matched {recipients},",
				_rules.Count,
				string.Join(", ", transaction.To.Select(t => t.AsAddress())));
			return SmtpServerResponse.MailboxNameNotAllowed;
		}

		try
		{
			var message = await GetMessageAsync(buffer, cancellationToken);
			var hostGroups = matchedRules.GroupBy(m => m.rule.ForwardAddress.Domain);

			var sendTasks = hostGroups.Select(async group =>
			{
				var domain = group.Key;
				var forwardAddresses = group.Select(g => g.rule.ForwardAddress).ToList();

				return await ForwardEmailAsync(domain, forwardAddresses, context, transaction, message, cancellationToken);
			});
			var sendResponses = await Task.WhenAll(sendTasks);

			if(sendResponses.Any(r => r == SmtpServerResponse.Ok))
			{
				// Todo: Better logging if not all emails are sent ok
				_log.LogInformation("Email forwarded");
				return SmtpServerResponse.Ok;
			}
		}
		catch(Exception ex)
		{
			_log.LogError(ex, "Error forwarding email - {message}", ex.Message);	
		}
		
		return SmtpServerResponse.TransactionFailed;
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

		return SmtpServerResponse.Ok;
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