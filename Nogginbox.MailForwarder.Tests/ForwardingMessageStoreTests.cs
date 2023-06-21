using MailKit.Net.Smtp;
using MimeKit;
using Nogginbox.MailForwarder.Server;
using Nogginbox.MailForwarder.Server.Dns;
using Nogginbox.MailForwarder.Server.MessageStores;
using NSubstitute;
using SmtpServer;
using SmtpServer.Mail;
using System.Buffers;
using SmtpServerResponse = SmtpServer.Protocol.SmtpResponse;

namespace Nogginbox.MailForwarder.Tests;

public class ForwardingMessageStoreTests
{
	[Theory]
	[InlineData("noone@nowhere.com")]
	[InlineData("someone.nice@somewhere.com")]
	[InlineData("noone@alias-domain.com")]
	public async Task IgnoresEmailIfNoRuleMatches(string incomingRecipientAddress)
	{
		// Arrange
		var rules = new List<ForwardRule>
		{
			new ForwardRule("someone.*@alias-domain.com", "target@target-domain.com")
		};
		var dnsFinder = CreateMockFinder();
		var storedMailKitResponses = new MailKitClientResponses();
		var smtpClient = CreateMockSmtpClient(storedMailKitResponses);
		var store = new ForwardingMessageStore(rules, dnsFinder, smtpClient);
		var context = CreateMockSessionContext();
		var transaction = CreateMockTransaction(incomingRecipientAddress);
		var buffer = CreateMessageInBuffer();

		// Act
		var response = await store.SaveAsync(context, transaction, buffer, CancellationToken.None);

		// Assert
		Assert.Equal(SmtpServerResponse.MailboxNameNotAllowed, response);
	}

	[Theory]
	[InlineData("someone.awesome@alias-domain.com")]
	[InlineData("someone.terrible@alias-domain.com")]
	[InlineData("someone.田上@alias-domain.com")]
	public async Task ForwardsEmailIfRuleMatches(string incomingRecipientAddress)
	{
		// Arrange
		const string targetEmail = "target@target-domain.com";
		var rules = new List<ForwardRule>
		{
			new ForwardRule("someone.*@alias-domain.com", targetEmail)
		};
		var dnsFinder = CreateMockFinder();
		var storedMailKitResponses = new MailKitClientResponses();
		var smtpClient = CreateMockSmtpClient(storedMailKitResponses);
		var store = new ForwardingMessageStore(rules, dnsFinder, smtpClient);
		var context = CreateMockSessionContext();
		var transaction = CreateMockTransaction(incomingRecipientAddress);
		var buffer = CreateMessageInBuffer();

		// Act
		var response = await store.SaveAsync(context, transaction, buffer, CancellationToken.None);

		// Assert
		Assert.Equal(SmtpServerResponse.Ok, response);

		await smtpClient.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<MailboxAddress>(), Arg.Any<IEnumerable<MailboxAddress>>(), Arg.Any<CancellationToken>());
		Assert.Contains(targetEmail, storedMailKitResponses.Recipients?.Select(r => r.Address) ?? Enumerable.Empty<string>());
		
		await smtpClient.Received().DisconnectAsync(true, Arg.Any<CancellationToken>());
	}

	private static ReadOnlySequence<byte> CreateMessageInBuffer()
	{
		var message = new MimeMessage();

		using var memoryStream = new MemoryStream();
		message.WriteTo(memoryStream);
		var bytes = memoryStream.ToArray();
		return new ReadOnlySequence<byte>(bytes);
	}

	private static IDnsMxFinder CreateMockFinder(string? domain = null)
	{
		const string targetMxServer = "mail.local";
		var	sub = Substitute.For<IDnsMxFinder>();
		if(domain == null)
		{
			sub.LookupMxServers(Arg.Any<string>()).Returns(new[] { targetMxServer });
		}
		else
		{
			sub.LookupMxServers(domain).Returns(new[] { targetMxServer });
		}
		return sub;
	}

	private static ISessionContext CreateMockSessionContext()
	{
		var sub = Substitute.For<ISessionContext>();
		return sub;
	}

	private static ISmtpClient CreateMockSmtpClient(MailKitClientResponses storedResponses)
	{
		var sub = Substitute.For<ISmtpClient>();
		sub.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<MailboxAddress>(), Arg.Any<IEnumerable<MailboxAddress>>(), Arg.Any<CancellationToken>())
			.Returns(x => {
				storedResponses.Recipients = (IEnumerable<MailboxAddress>)x[2];
				return Task.FromResult("OK");
			});
		return sub;
	}

	private static IMessageTransaction CreateMockTransaction(params string[] recipientAddresses)
	{
		var sub = Substitute.For<IMessageTransaction>();
		var recipients = recipientAddresses.Select(r => new Mailbox(r) as IMailbox).ToList();
		sub.To.Returns(recipients);
		return sub;
	}

	private class MailKitClientResponses
	{
		public IEnumerable<MailboxAddress>? Recipients { get; set; }
	}
}