using DnsClient.Internal;
using MailKit.Net.Smtp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nogginbox.MailForwarder.Server.Configuration;
using Nogginbox.MailForwarder.Server.Dns;
using Nogginbox.MailForwarder.Server.MailboxFilters;
using Nogginbox.MailForwarder.Server.MessageStores;
using SmtpServer;
using SmtpServer.Authentication;
using Logging = Microsoft.Extensions.Logging;
using SmtpServiceProvider = SmtpServer.ComponentModel.ServiceProvider;

namespace Nogginbox.MailForwarder.Server;

public class MailForwardServer
{
	private readonly List<ForwardRule> _rules = new ();
	private readonly DnsMxFinder _dnsFinder = new();
	private readonly SmtpClient _smtpClient = new();
	private SmtpServer.SmtpServer _server;

	public MailForwardServer(IServiceProvider services)
	{
		var config = services.GetRequiredService<IOptions<ForwardConfiguration>>().Value;

		var options = new SmtpServerOptionsBuilder()
			.ServerName(config.ServerName)
			.Port(25, 587)
			.Port(465, isSecure: true)
			//.Certificate(CreateX509Certificate2())
			.Build();

		var loggerFactory = services.GetRequiredService<Logging.ILoggerFactory>();
		Init(config, options, loggerFactory);
	}

	private void Init(ForwardConfiguration configuration, ISmtpServerOptions smtpOptions, Logging.ILoggerFactory loggerFactory)
	{
		if(configuration.Rules?.Any() != true)
		{
			throw new Exception("No rules have been set in the configuration.");
		}

		var log = loggerFactory.CreateLogger<MailForwardServer>();
		LoadRules(configuration, log);

		var serviceProvider = new SmtpServiceProvider();
		serviceProvider.Add(new IsExpectedRecipientMailboxFilter(_rules, loggerFactory.CreateLogger<IsExpectedRecipientMailboxFilter>()));
		serviceProvider.Add(new SampleUserAuthenticator());
		serviceProvider.Add(new ForwardingMessageStore(_rules, _dnsFinder, _smtpClient, loggerFactory.CreateLogger<ForwardingMessageStore>()));
		//serviceProvider.Add(new SampleUserAuthenticator());
		_server = new SmtpServer.SmtpServer(smtpOptions, serviceProvider);
		RegisterSmtpEvents(_server, log);
	}

	private void LoadRules(ForwardConfiguration configuration, Logging.ILogger log)
	{	
		foreach (var configRule in configuration.Rules)
		{
			var rule = new ForwardRule(configRule.Alias, configRule.Address);
			_rules.Add(rule);
			log.LogInformation("Registered rule (pattern: {pattern}, forward: {forward}", rule.AliasPattern, rule.ForwardAddress);
		}
		log.LogInformation("{rulecount} rules completed registering.", _rules.Count);
	}

	private static void RegisterSmtpEvents(SmtpServer.SmtpServer server, Logging.ILogger log)
	{
		server.SessionCreated += (s, e) =>
		{
			log.LogInformation("SMTP Session started.");

			e.Context.CommandExecuting += (sender, args) =>
			{
				log.LogInformation("Cmd executing: {command}", args.Command);
			};

			e.Context.CommandExecuted += (sender, args) =>
			{
				log.LogInformation("Cmd executed: {command}", args.Command);
			};

			e.Context.SessionAuthenticated += (sender, args) =>
			{
				log.LogInformation("Session authenticated");
			};
		};
	}

	public Task StartAsync(CancellationToken token = default)
	{
		return _server.StartAsync(token);
	}
}

/// <summary>
/// Just for testing so far. Checking if the default one is stopping stuff.
/// </summary>
public class SampleUserAuthenticator : IUserAuthenticator
{
	public Task<bool> AuthenticateAsync(ISessionContext context, string user, string password, CancellationToken token)
	{
		Console.WriteLine("User={0} Password={1}", user, password);

		return Task.FromResult(user.Length > 4);
	}
}


//await smtpServer.StopAsync();