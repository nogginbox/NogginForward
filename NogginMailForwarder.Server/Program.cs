using NogginMailForwarder.Server.MailboxFilters;
using NogginMailForwarder.Server.MessageStores;
using SmtpServer;
using SmtpServer.ComponentModel;

using NogginMailForwarder.Server;

var options = new SmtpServerOptionsBuilder()
	.ServerName("localhost")
	.Port(25, 587)
	.Port(465, isSecure: true)
	//.Certificate(CreateX509Certificate2())
	.Build();

var rules = new List<ForwardRule>();
var dns 

var serviceProvider = new ServiceProvider();
serviceProvider.Add(new IsExpectedRecipientMailboxFilter(rules));
serviceProvider.Add(new ForwardingMessageStore(rules));
//serviceProvider.Add(new SampleUserAuthenticator());

var smtpServer = new SmtpServer.SmtpServer(options, serviceProvider);
await smtpServer.StartAsync(CancellationToken.None);