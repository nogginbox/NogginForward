using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nogginbox.MailForwarder.Server;
using Nogginbox.MailForwarder.Server.Configuration;

Console.WriteLine("Starting Nogginbox Mailforwarding Server ...");

var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
var configuration = new ConfigurationBuilder()
	.SetBasePath(Directory.GetCurrentDirectory())
	.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
	.AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: true)
	.AddEnvironmentVariables()
	.Build();

var services = new ServiceCollection()
	.AddOptions()
	.Configure<ForwardConfiguration>(configuration.GetRequiredSection("MailForwarder"))
	.BuildServiceProvider();


var server = new MailForwardServer(services);

try
{
	await server.StartAsync(CancellationToken.None);
	Console.WriteLine("SMTP server started successfully.");
}
catch (Exception ex)
{
	Console.WriteLine($"An error occurred while starting the SMTP server: {ex.Message}");
}


Console.WriteLine("SMTP server started. Press any key to stop...");
Console.ReadKey();
