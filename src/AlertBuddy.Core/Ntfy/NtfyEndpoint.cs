using AlertBuddy.Core.Localization;
using System.Net;
using System.Net.Sockets;

namespace AlertBuddy.Core.Ntfy
{
    /// <summary>The result of checking a server address a grown-up typed.</summary>
    /// <param name="IsValid">Whether the app can use it.</param>
    /// <param name="BaseUri">The address without a trailing slash, when valid.</param>
    /// <param name="Unencrypted">Plain http on a private network: allowed, shown with an "unencrypted" badge.</param>
    /// <param name="Problem">What is wrong and how to fix it, written for a grown-up, when not valid.</param>
    public sealed record EndpointCheck (bool IsValid, Uri? BaseUri, bool Unencrypted, string? Problem);

    /// <summary>
    /// The rule for which server addresses the app will talk to (PLAN.md section 5.2). Https is the default. Plain http is allowed only
    /// where nothing crosses the internet: this device, a home network's private addresses, and <c>.local</c> names. The credentials
    /// go in the request; an address that carries them is refused so they cannot end up in a log or a screenshot.
    /// </summary>
    public static class NtfyEndpoint
    {
        /// <summary>Checks an address.</summary>
        public static EndpointCheck Check (string? address)
        {
            if (string.IsNullOrWhiteSpace (address))
                return Invalid (Loc.T ("Enter the server address, for example https://ntfy.example.com."));

            if (!Uri.TryCreate (address.Trim (), UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) || string.IsNullOrEmpty (uri.Host))
                return Invalid (Loc.T ("The address must start with https:// (or http:// for a server on the home network) and name the server."));

            if (!string.IsNullOrEmpty (uri.UserInfo))
                return Invalid (Loc.T ("Leave the user name and password out of the address. Enter them under sign-in."));

            if (!string.IsNullOrEmpty (uri.Query) || !string.IsNullOrEmpty (uri.Fragment))
                return Invalid (Loc.T ("The address should be the server only, without anything after a ? or a #."));

            var unencrypted = uri.Scheme == Uri.UriSchemeHttp;
            if (unencrypted && !IsPrivateHost (uri.Host))
                return Invalid (Loc.T ("Plain http is only allowed for this device, the home network and .local names. Use https for a server on the internet."));

            // A server may live under a path such as /ntfy; keep it, drop the trailing slash so joining is uniform.
            var baseUri = new Uri (uri.GetLeftPart (UriPartial.Path).TrimEnd ('/'));
            return new EndpointCheck (true, baseUri, unencrypted, null);
        }

        /// <summary>Whether a host is the local machine, a private (RFC 1918) address or a <c>.local</c> name.</summary>
        public static bool IsPrivateHost (string host)
        {
            if (host.Equals ("localhost", StringComparison.OrdinalIgnoreCase) || host.EndsWith (".local", StringComparison.OrdinalIgnoreCase))
                return true;

            // IPv6 hosts come with brackets from Uri.Host.
            if (!IPAddress.TryParse (host.Trim ('[', ']'), out var address))
                return false;

            if (IPAddress.IsLoopback (address))
                return true;

            if (address.AddressFamily != AddressFamily.InterNetwork)
                return false;

            var b = address.GetAddressBytes ();
            return b[0] == 10
                || (b[0] == 172 && b[1] is >= 16 and <= 31)
                || (b[0] == 192 && b[1] == 168);
        }

        private static EndpointCheck Invalid (string problem) => new (false, null, false, problem);
    }

    /// <summary>The rule for topic names ntfy accepts.</summary>
    public static class NtfyTopic
    {
        /// <summary>ntfy topics are 1 to 64 letters, digits, underscores and hyphens. They act as a password, so a topic is never logged.</summary>
        public static bool IsValid (string? topic)
            => topic is { Length: >= 1 and <= 64 } && topic.All (c => char.IsAsciiLetterOrDigit (c) || c is '_' or '-');
    }
}
