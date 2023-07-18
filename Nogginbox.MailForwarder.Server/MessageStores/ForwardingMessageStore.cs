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

	/// <summary>
	/// The SMTP port used for server to server communication.
	/// </summary>
	private const int SmtpPort = 25;

	public ForwardingMessageStore(IReadOnlyList<ForwardRule> rules, IDnsMxFinder dnsFinder, ISmtpClient smtpClient, Logging.ILogger log)
	{
		_dnsFinder = dnsFinder;
		_log = log;
		_rules = rules;
		_smtpClient = smtpClient;
	}

	public override async Task<SmtpServerResponse> SaveAsync(ISessionContext context, IMessageTransaction transaction, ReadOnlySequence<byte> buffer, CancellationToken cancellationToken)
	{
		_log.LogInformation("Begin send attempt");
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
			_log.LogWarning("No rules in rulset({rulesetcount}) matched {recipients},",
				_rules.Count,
				string.Join(", ", transaction.To.Select(t => t.AsAddress())));
			return SmtpServerResponse.MailboxNameNotAllowed;
		}

		try
		{
			var message = await GetMessageAsync(buffer, cancellationToken);
			var sender = new MailboxAddress(transaction.From.User, transaction.From.AsAddress());
			var hostGroups = matchedRules.GroupBy(m => m.rule.ForwardAddress.Domain);

			var sendTasks = hostGroups.Select(async group =>
			{
				var domain = group.Key;
				var forwardAddresses = group.Select(g => g.rule.ForwardAddress).ToList();

				return await ForwardEmailAsync(domain, sender, forwardAddresses, context, transaction, message, cancellationToken);
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

	private async Task<SmtpServerResponse> ForwardEmailAsync(string mailserverDomain, MailboxAddress sender, List<MailboxAddress> forwardRecipients, ISessionContext context, IMessageTransaction transaction, MimeMessage message, CancellationToken cancellationToken)
	{
		var mailServer = (await _dnsFinder.LookupMxServers(mailserverDomain)).FirstOrDefault()
			?? throw new Exception($"No mailserver found for domain '{mailserverDomain}'");

		_log.LogInformation("Completed DNS MX lookup of '{mailserver-domain}' and found address:'{mailserver-address}'", mailserverDomain, mailServer);

		try
		{
			await _smtpClient.ConnectAsync(mailServer, SmtpPort, SecureSocketOptions.Auto, cancellationToken);
			_log.LogInformation("Connected to {mailServer}:{SmtpPost}", mailServer, SmtpPort);
		}
		catch (Exception ex)
		{
			throw new Exception($"Failed to connect to mailserver:{mailServer} on port {SmtpPort}", ex);
		}

		// Note: only needed if the SMTP server requires authentication
		//client.Authenticate("joey", "password");

		try
		{
			var response = await _smtpClient.SendAsync(message, sender, recipients: forwardRecipients, cancellationToken);
			_log.LogInformation("Forward server responded with {response}", response);
		}
		catch (SmtpCommandException ex)
		{
			_log.LogError(ex, "Failed to send: {response}", ex.Message);
			return ex.StatusCode switch
			{
				_ => SmtpServerResponse.MailboxUnavailable
			}; ;
		}
		catch (Exception ex)
		{
			throw new Exception($"Failed to send", ex);
		}

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