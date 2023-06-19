using SmtpServer;
using SmtpServer.Mail;
using SmtpServer.Storage;

namespace Nogginbox.MailForwarder.Server.MailboxFilters;

/// <summary>
/// Checks to see if the intended recipient of this email is in the configured forward rules.
/// </summary>
public class IsExpectedRecipientMailboxFilter : IMailboxFilter
{
	private readonly IReadOnlyList<ForwardRule> _rules;

	public IsExpectedRecipientMailboxFilter(IReadOnlyList<ForwardRule> rules)
	{
		_rules = rules;
	}

	public Task<MailboxFilterResult> CanAcceptFromAsync(ISessionContext context, IMailbox @from, int size, CancellationToken cancellationToken)
	{
		return Task.FromResult(MailboxFilterResult.Yes);
	}

	public Task<MailboxFilterResult> CanDeliverToAsync(ISessionContext context, IMailbox to, IMailbox @from, CancellationToken token)
	{
		if (!_rules.Any(r => r.IsMatch(to.AsAddress())))
		{
			// Log this
			return Task.FromResult(MailboxFilterResult.NoPermanently);
		}

		return Task.FromResult(MailboxFilterResult.Yes);
	}
}
