using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nogginbox.MailForwarder.Server;
using Nogginbox.MailForwarder.Server.Configuration;

const decimal Version = 1.0m;

Console.WriteLine($"NogginForward Server {Version}");

var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
var configuration = new ConfigurationBuilder()
	.SetBasePath(Directory.GetCurrentDirectory())
	.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
	.AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: true)
	.AddEnvironmentVariables()
	.Build();


var services = new ServiceCollection()
	.AddOptions()
	.AddLogging(loggingBuilder =>
	{
		loggingBuilder.AddSimpleConsole(options =>
		{
			options.IncludeScopes = true;
			options.SingleLine = true;
			options.TimestampFormat = "HH:mm:ss ";
		});
	})
	.Configure<ForwardConfiguration>(configuration.GetRequiredSection("MailForwarder"))
	.BuildServiceProvider();

var log = services.GetRequiredService<ILogger<Program>>();
log.LogInformation("Starting NogginForward {Version}", Version);

var server = new MailForwardServer(services);

try
{
	await server.StartAsync(CancellationToken.None);
	Console.WriteLine("SMTP server started successfully.");
}
catch (Exception ex)
{
	log.LogCritical(ex, "An error occurred while starting the SMTP server: {message}", ex.Message);
}


Console.WriteLine("SMTP server started. Press any key to stop...");
Console.ReadKey();
