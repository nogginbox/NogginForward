using DnsClient;

namespace NogginMailForwarder.Server;

public class DnsMxFinder
{
	private readonly LookupClient _dnsClient;

	public DnsMxFinder()
    {
		_dnsClient = new ()
		{
			UseCache = true
		};
	}

	/// <summary>
	/// Looks up the exchange names for a given host address.
	/// </summary>
	/// <param name="hostAddress">The host address of the email recipient.</param>
	/// <returns>A list of mail exchanges ordered by preference.</returns>
	/// <exception cref="Exception"></exception>
    public async IList<string> LookupMxServers(string hostAddress)
	{
		var queryResponse = await _dnsClient.QueryAsync(hostAddress, QueryType.MX);
		if(queryResponse.HasError)
		{
			throw new Exception(queryResponse.ErrorMessage);
		}

		var mxRecords = queryResponse.AllRecords.MxRecords()
			.OrderBy(mx => mx.Preference)
			.Select(mx => mx.Exchange.Value)
			.ToList();

		// Check ordering by preference is right

		return mxRecords;
	}
}
