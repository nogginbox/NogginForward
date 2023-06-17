using System.Text.RegularExpressions;

namespace NogginMailForwarder.Server;

public record ForwardRule(string AliasPattern, string ForwardAddress)
{
	private readonly Regex _aliasRegex = new (WildCardToRegular(AliasPattern), RegexOptions.IgnoreCase);

	public bool IsMatch(string address) => _aliasRegex.IsMatch(address);

	private static string WildCardToRegular(string value) => $"^{Regex.Escape(value).Replace("\\*", ".*")}$";
}
