using MailKit.Net.Smtp;
using NogginMailForwarder.Server;
using NogginMailForwarder.Server.Dns;
using NogginMailForwarder.Server.MessageStores;
using NSubstitute;
using SmtpServer;
using SmtpServerResponse = SmtpServer.Protocol.SmtpResponse;
using System.Buffers;
using SmtpServer.Mail;
using MimeKit;

namespace Nogginbox.MailForwarder.Tests;

public class ForwardingMessageStoreTests
{
	[Fact]
	public async Task IgnoresEmailIfNoRuleMatches()
	{
		// Arrange
		var rules = new List<ForwardRule>
		{
			new ForwardRule("someone.*@nowhere.com", "target@somewhere.com")
		};
		var dnsFinder = CreateMockFinder();
		var smtpClient = CreateMockSmtpClient();
		var store = new ForwardingMessageStore(rules, dnsFinder, smtpClient);
		var context = CreateMockSessionContext();
		var transaction = CreateMockTransaction("noone@nowhere.com");
		var buffer = CreateMessageInBuffer();

		// Act
		var response = await store.SaveAsync(context, transaction, buffer, CancellationToken.None);

		// Assert
		Assert.Equal(SmtpServerResponse.MailboxNameNotAllowed, response);
	}

	[Fact]
	public async Task ForwardsEmailIfRuleMatches()
	{
		// Arrange
		var rules = new List<ForwardRule>
		{
			new ForwardRule("someone.*@nowhere.com", "target@somewhere.com")
		};
		var dnsFinder = CreateMockFinder();
		var smtpClient = CreateMockSmtpClient();
		var store = new ForwardingMessageStore(rules, dnsFinder, smtpClient);
		var context = CreateMockSessionContext();
		var transaction = CreateMockTransaction("someone.awesome@nowhere.com");
		var buffer = CreateMessageInBuffer();

		// Act
		var response = await store.SaveAsync(context, transaction, buffer, CancellationToken.None);

		// Assert
		Assert.Equal(SmtpServerResponse.Ok, response);
	}

	private static ReadOnlySequence<byte> CreateMessageInBuffer()
	{
		var message = new MimeMessage();

		using var memoryStream = new MemoryStream();
		message.WriteTo(memoryStream);
		var bytes = memoryStream.ToArray();
		return new ReadOnlySequence<byte>(bytes);
	}

	private static IDnsMxFinder CreateMockFinder()
	{
		var	sub = Substitute.For<IDnsMxFinder>();
		return sub;
	}

	private static ISessionContext CreateMockSessionContext()
	{
		var sub = Substitute.For<ISessionContext>();
		return sub;
	}

	private static ISmtpClient CreateMockSmtpClient()
	{
		var sub = Substitute.For<ISmtpClient>();
		return sub;
	}

	private static IMessageTransaction CreateMockTransaction(params string[] recipientAddresses)
	{
		var sub = Substitute.For<IMessageTransaction>();
		var recipients = recipientAddresses.Select(r => new Mailbox(r) as IMailbox).ToList();
		sub.To.Returns(recipients);
		return sub;
	}
}