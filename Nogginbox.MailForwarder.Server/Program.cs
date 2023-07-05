using MailKit.Net.Smtp;
using Nogginbox.MailForwarder.Server;
using Nogginbox.MailForwarder.Server.Dns;
using Nogginbox.MailForwarder.Server.MailboxFilters;
using Nogginbox.MailForwarder.Server.MessageStores;
using SmtpServer;
using SmtpServer.ComponentModel;


Console.WriteLine("Starting Nogginbox Mailforwarding Server ...");

var options = new SmtpServerOptionsBuilder()
	.ServerName("localhost")
	.Port(25, 587)
	.Port(465, isSecure: true)
	//.Certificate(CreateX509Certificate2())
	.Build();

var rules = new List<ForwardRule>();
var dnsFinder = new DnsMxFinder();
var smtpClient = new SmtpClient();

var serviceProvider = new ServiceProvider();
serviceProvider.Add(new IsExpectedRecipientMailboxFilter(rules));
serviceProvider.Add(new ForwardingMessageStore(rules, dnsFinder, smtpClient));
//serviceProvider.Add(new SampleUserAuthenticator());

var smtpServer = new SmtpServer.SmtpServer(options, serviceProvider);


// Register the error event handler
smtpServer.SessionCreated += (s, e) =>
{
	Console.WriteLine("SMTP Session started.");

	e.Context.CommandExecuting += (sender, args) =>
	{
		Console.WriteLine($"Command executing: {args.Command}");
	};

	e.Context.CommandExecuted += (sender, args) =>
	{
		Console.WriteLine($"Command executed: {args.Command}");
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


//await smtpServer.StartAsync(CancellationToken.None);


try
{
	await smtpServer.StartAsync(CancellationToken.None);
	Console.WriteLine("SMTP server started successfully.");
}
catch (Exception ex)
{
	Console.WriteLine($"An error occurred while starting the SMTP server: {ex.Message}");
}


Console.WriteLine("SMTP server started. Press any key to stop...");
Console.ReadKey();

//await smtpServer.StopAsync();