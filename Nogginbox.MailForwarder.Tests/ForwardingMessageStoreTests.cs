﻿using MailKit.Net.Smtp;
using MimeKit;
using Nogginbox.MailForwarder.Server;
using Nogginbox.MailForwarder.Server.Dns;
using Nogginbox.MailForwarder.Server.MessageStores;
using NSubstitute;
using SmtpServer;
using SmtpServer.Mail;
using System.Buffers;
using Logging = Microsoft.Extensions.Logging;
using SmtpServerResponse = SmtpServer.Protocol.SmtpResponse;

namespace Nogginbox.MailForwarder.Tests;

public class ForwardingMessageStoreTests
{
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
			new ("someone.*@alias-domain.com", targetEmail)
		};
		var dnsFinder = CreateMockFinder();
		var storedMailKitResponses = new List<MailKitClientResponse>();
		var smtpClient = CreateMockSmtpClient(storedMailKitResponses);
		var smtpClientFactory = () => smtpClient;
		var log = Substitute.For<Logging.ILogger>();
		var store = new ForwardingMessageStore(rules, dnsFinder, smtpClientFactory, log);
		var context = CreateMockSessionContext();
		var transaction = CreateMockEmailTransaction(incomingRecipientAddress);
		var buffer = CreateMessageInBuffer();

		// Act
		var response = await store.SaveAsync(context, transaction, buffer, CancellationToken.None);

		// Assert
		Assert.Equal(SmtpServerResponse.Ok, response);

		await smtpClient.Received()
			.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<MailboxAddress>(), Arg.Any<IEnumerable<MailboxAddress>>(), Arg.Any<CancellationToken>());
		
		var storedMailKitResponse = Assert.Single(storedMailKitResponses);
		Assert.NotNull(storedMailKitResponse.Recipients);
		var recipientAddresses = storedMailKitResponse.Recipients.Select(r => r.Address);
		Assert.Contains(targetEmail, recipientAddresses);
		
		await smtpClient.Received().DisconnectAsync(true, Arg.Any<CancellationToken>());
	}

	//[Theory]
	//[InlineData("someone.to@alias-domain.com")]
	//[InlineData("someone.cc@alias-domain.com")]
	//[InlineData("someone.bcc@alias-domain.com")]
	//public async Task ForwardsEmailIfRuleMatchesAnyRecipientField(string incomingRecipientAddress)

	[Fact]
	public async Task IncomingAddressMatchingTwoRulesWillForwardToTopRule()
	{
		// Arrange
		const string incomingRecipientAddress = "someone.testing@alias-domain.com";
		const string targetEmail1 = "target1@target-domain.com";
		const string targetEmail2 = "target2@target-domain.com";
		var rules = new List<ForwardRule>
		{
			new ("someone.*@alias-domain.com", targetEmail1),
			new ("*@alias-domain.com", targetEmail2)
		};
		var dnsFinder = CreateMockFinder();
		var storedMailKitResponses = new List<MailKitClientResponse>();
		var log = Substitute.For<Logging.ILogger>();
		var smtpClient = CreateMockSmtpClient(storedMailKitResponses);
		var smtpClientFactory = () => smtpClient;
		var store = new ForwardingMessageStore(rules, dnsFinder, smtpClientFactory, log);
		var context = CreateMockSessionContext();
		var transaction = CreateMockEmailTransaction(incomingRecipientAddress);
		var buffer = CreateMessageInBuffer();

		// Act
		var response = await store.SaveAsync(context, transaction, buffer, CancellationToken.None);

		// Assert
		Assert.Equal(SmtpServerResponse.Ok, response);

		await smtpClient.Received()
			.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<MailboxAddress>(), Arg.Any<IEnumerable<MailboxAddress>>(), Arg.Any<CancellationToken>());

		var storedMailKitResponse = Assert.Single(storedMailKitResponses);
		Assert.NotNull(storedMailKitResponse.Recipients);
		var recipientAddresses = storedMailKitResponse.Recipients.Select(r => r.Address);
		var outgoingRecipient = Assert.Single(recipientAddresses);
		Assert.Equal(targetEmail1, outgoingRecipient);

		await smtpClient.Received().DisconnectAsync(true, Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task MultipleOutgoingEmailToSameAddressWillOnlySendOnce()
	{
		// Arrange
		const string targetEmail = "target@target-domain.com";
		var rules = new List<ForwardRule>
		{
			new ("*@alias-domain.com", targetEmail)
		};
		var dnsFinder = CreateMockFinder();
		var storedMailKitResponses = new List<MailKitClientResponse>();
		var log = Substitute.For<Logging.ILogger>();
		var smtpClient = CreateMockSmtpClient(storedMailKitResponses);
		var smtpClientFactory = () => smtpClient;
		var store = new ForwardingMessageStore(rules, dnsFinder, smtpClientFactory, log);
		var context = CreateMockSessionContext();
		var transaction = CreateMockEmailTransaction(
			"alias1@alias-domain.com",
			"alias2@alias-domain.com",
			"alias3@alias-domain.com");
		var buffer = CreateMessageInBuffer();

		// Act
		var response = await store.SaveAsync(context, transaction, buffer, CancellationToken.None);

		// Assert
		Assert.Equal(SmtpServerResponse.Ok, response);

		await smtpClient.Received()
			.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<MailboxAddress>(), Arg.Any<IEnumerable<MailboxAddress>>(), Arg.Any<CancellationToken>());

		var storedMailKitResponse = Assert.Single(storedMailKitResponses);
		Assert.NotNull(storedMailKitResponse.Recipients);
		var recipientAddresses = storedMailKitResponse.Recipients.Select(r => r.Address);
		var outgoingRecipient = Assert.Single(recipientAddresses);
		Assert.Equal(targetEmail, outgoingRecipient);

		await smtpClient.Received().DisconnectAsync(true, Arg.Any<CancellationToken>());
	}

	[Theory]
	[InlineData("noone@nowhere.com")]
	[InlineData("someone.nice@somewhere.com")]
	[InlineData("noone@alias-domain.com")]
	[InlineData("issomeone.almost@alias-domain.com")]
	public async Task IgnoresEmailIfNoRuleMatches(string incomingRecipientAddress)
	{
		// Arrange
		var rules = new List<ForwardRule>
		{
			new ForwardRule("someone.*@alias-domain.com", "target@target-domain.com")
		};
		var dnsFinder = CreateMockFinder();
		var storedMailKitResponses = new List<MailKitClientResponse>();
		var log = Substitute.For<Logging.ILogger>();
		var smtpClient = CreateMockSmtpClient(storedMailKitResponses);
		var smtpClientFactory = () => smtpClient;
		var store = new ForwardingMessageStore(rules, dnsFinder, smtpClientFactory, log);
		var context = CreateMockSessionContext();
		var transaction = CreateMockEmailTransaction(incomingRecipientAddress);
		var buffer = CreateMessageInBuffer();

		// Act
		var response = await store.SaveAsync(context, transaction, buffer, CancellationToken.None);

		// Assert
		Assert.Equal(SmtpServerResponse.MailboxNameNotAllowed, response);
		Assert.Empty(storedMailKitResponses);
	}

	[Theory]
	[InlineData(1, "test1@target1.com", "test2@target1.com")]
	[InlineData(2, "test1@target1.com", "test2@target2.com", "test3@target1.com")]
	public async Task GroupsEmailsByTargetHost(int domainCount, params string[] forwardRuleRecipients)
	{
		// Arrange
		var rules = forwardRuleRecipients.Select((x) => {
			var parts = x.Split('@');
			return new ForwardRule($"{parts[0]}@alias-domain.com", x);
		}).ToList();
		var incomingRecipientAddress = rules.Select(r => r.AliasPattern).ToList();

		rules.Add(new ForwardRule("never-match@alias-domain.com", "noone@bad-domain.com"));
		

		var dnsFinder = CreateMockFinder();
		var storedMailKitResponses = new List<MailKitClientResponse>();
		var log = Substitute.For<Logging.ILogger>();
		var smtpClient = CreateMockSmtpClient(storedMailKitResponses);
		var smtpClientFactory = () => smtpClient;
		var store = new ForwardingMessageStore(rules, dnsFinder, smtpClientFactory, log);
		var context = CreateMockSessionContext();
		var transaction = CreateMockEmailTransaction(incomingRecipientAddress);
		var buffer = CreateMessageInBuffer();

		// Act
		var storeResponse = await store.SaveAsync(context, transaction, buffer, CancellationToken.None);

		// Assert
		Assert.Equal(SmtpServerResponse.Ok, storeResponse);

		await smtpClient.Received()
			.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<MailboxAddress>(), Arg.Any<IEnumerable<MailboxAddress>>(), Arg.Any<CancellationToken>());

		Assert.Equal(domainCount, storedMailKitResponses.Count); // One response per domain

		// Assert - Each response sending to only one domain
		Assert.All(storedMailKitResponses, response => {
			var domains = response.Recipients?.Select(r => r.Domain).Distinct();
			var domain = Assert.Single(domains!);
			// Todo: Check response is being sent to correct MX
		});

		var allMailkitRecipients = storedMailKitResponses
			.SelectMany(r => r.Recipients!)
			.Select(r => r.Address)
			.ToList();

		Assert.NotEmpty(allMailkitRecipients);
		Assert.All(forwardRuleRecipients, item =>
		{
			Assert.Contains(item, allMailkitRecipients);
		});

		await smtpClient
			.Received(domainCount).DisconnectAsync(true, Arg.Any<CancellationToken>());
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

	private static ISmtpClient CreateMockSmtpClient(IList<MailKitClientResponse> storedResponses)
	{
		var sub = Substitute.For<ISmtpClient>();
		sub.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<MailboxAddress>(), Arg.Any<IEnumerable<MailboxAddress>>(), Arg.Any<CancellationToken>())
			.Returns(x => {
				storedResponses.Add(new MailKitClientResponse
				{
					Recipients = ((IEnumerable<MailboxAddress>)x[2])
				});
				return Task.FromResult("OK");
			});
		return sub;
	}

	private static IMessageTransaction CreateMockEmailTransaction(params string[] recipientAddresses)
		=> CreateMockEmailTransaction(recipientAddresses as IEnumerable<string>);

	private static IMessageTransaction CreateMockEmailTransaction(IEnumerable<string> recipientAddresses)
	{
		var emailTransaction = Substitute.For<IMessageTransaction>();
		var recipients = recipientAddresses.Select(r => new Mailbox(r) as IMailbox).ToList();
		var sender = new Mailbox("test.sender", "localhost");
		emailTransaction.To.Returns(recipients);
		emailTransaction.From.Returns(sender);

		return emailTransaction;
	}


	private class MailKitClientResponse
	{
		public IEnumerable<MailboxAddress>? Recipients { get; set; }
	}
}