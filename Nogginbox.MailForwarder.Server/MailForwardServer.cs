using MailKit.Net.Smtp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nogginbox.MailForwarder.Server.Configuration;
using Nogginbox.MailForwarder.Server.Dns;
using Nogginbox.MailForwarder.Server.MailboxFilters;
using Nogginbox.MailForwarder.Server.MessageStores;
using SmtpServer;
using Logging = Microsoft.Extensions.Logging;
using SmtpServiceProvider = SmtpServer.ComponentModel.ServiceProvider;

namespace Nogginbox.MailForwarder.Server;

public class MailForwardServer
{
	private List<ForwardRule> _rules;
	private DnsMxFinder _dnsFinder = new();
	private SmtpClient _smtpClient = new();
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

		var loggerFactory = services.GetRequiredService<ILoggerFactory> ();
		Init(config, options, loggerFactory);
	}

	private void Init(ForwardConfiguration configuration, ISmtpServerOptions smtpOptions, ILoggerFactory loggerFactory)
	{
		if(configuration.Rules?.Any() != true)
		{
			throw new Exception("No rules have been set in the configuration.");
		}
		
		_rules = new List<ForwardRule>();
		var log = loggerFactory.CreateLogger<MailForwardServer>();
		foreach(var configRule in configuration.Rules)
		{
			var rule = new ForwardRule(configRule.Alias, configRule.Address);
			_rules.Add(rule);
			log.LogInformation("Registered rule (pattern: {pattern}, forward: {forward}", rule.AliasPattern, rule.ForwardAddress);
		}
		log.LogInformation("{rulecount} rules finised registering.", _rules.Count);

		var serviceProvider = new SmtpServiceProvider();
		serviceProvider.Add(new IsExpectedRecipientMailboxFilter(_rules, loggerFactory.CreateLogger<IsExpectedRecipientMailboxFilter>()));
		serviceProvider.Add(new ForwardingMessageStore(_rules, _dnsFinder, _smtpClient, loggerFactory.CreateLogger<ForwardingMessageStore>()));
		//serviceProvider.Add(new SampleUserAuthenticator());
		_server = new SmtpServer.SmtpServer(smtpOptions, serviceProvider);
		RegisterSmtpEvents(_server, log);

		// todo - make sure you get the rules from config
	}

	private static void RegisterSmtpEvents(SmtpServer.SmtpServer server, Logging.ILogger log)
	{
		server.SessionCreated += (s, e) =>
		{
			log.LogInformation("SMTP Session started.");

			e.Context.CommandExecuting += (sender, args) =>
			{
				log.LogInformation("Command executing: {command}", args.Command);
			};

			e.Context.CommandExecuted += (sender, args) =>
			{
				log.LogInformation("Command executed: {command}", args.Command);
			};

			/*e.Context.ResponseSending += (sender, args) =>
			{
				Console.WriteLine($"Response sending: {args.Response}");
			};

			e.Context.ResponseSent += (sender, args) =>
			{
				Console.WriteLine($"Response sent: {args.Response}");
			};*/
		};
	}

	public Task StartAsync(CancellationToken token = default)
	{
		return _server.StartAsync(token);
	}
}


//await smtpServer.StopAsync();