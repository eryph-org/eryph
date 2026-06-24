namespace Eryph.Messages.Components;

/// <summary>
/// Sent directly to a component's inbound queue to make it renew its mTLS certificate now, in-process,
/// rather than waiting for the scheduled renewal window. An operator triggers it (e.g. to roll
/// certificates onto a new CA, or to verify renewal); the component renews against the identity renew
/// endpoint using its current certificate. It carries no payload — the recipient queue identifies the
/// component, and the renewed identity is taken from the presented certificate.
/// </summary>
public class RenewComponentCertificateCommand
{
}
