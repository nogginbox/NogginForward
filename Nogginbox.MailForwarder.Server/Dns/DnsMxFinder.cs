using DnsClient;

namespace Nogginbox.MailForwarder.Server.Dns;

/// <summary>
/// Looks up the DNS Mail Exchange (MX) records for a domain name.
/// </summary>
public class DnsMxFinder : IDnsMxFinder
{
    private readonly LookupClient _dnsClient;

    public DnsMxFinder()
    {
        var options = new LookupClientOptions
        {
            UseCache = true
        };
        _dnsClient = new (options);
    }

    /// <summary>
    /// Looks up the exchange names for a given host address.
    /// </summary>
    /// <param name="hostAddress">The host address of the email recipient.</param>
    /// <returns>A list of mail exchanges ordered by preference.</returns>
    /// <exception cref="Exception"></exception>
    public async Task<IList<string>> LookupMxServers(string hostAddress)
    {
        var queryResponse = await _dnsClient.QueryAsync(hostAddress, QueryType.MX);
        if (queryResponse.HasError)
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
