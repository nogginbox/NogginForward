namespace Nogginbox.MailForwarder.Server.Configuration;

public class ForwardConfiguration
{
	public RuleConfiguration[] Rules { get; set; } = Array.Empty<RuleConfiguration>();

	public string ServerName { get; set; } = "localhost";
}
