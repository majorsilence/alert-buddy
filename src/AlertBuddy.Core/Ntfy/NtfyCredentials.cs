using System.Text;

namespace AlertBuddy.Core.Ntfy
{
    /// <summary>
    /// How the app signs in to the server: nothing, a user name and password, or an access token. The secret is held only long enough to
    /// build a request header. <see cref="ToString"/> never shows it, so no log line, exception message or debugger view can leak it
    /// (PLAN.md section 5.2: never log secrets; redact Authorization everywhere).
    /// </summary>
    public sealed class NtfyCredentials
    {
        private NtfyCredentials (string? headerValue) => AuthorizationHeaderValue = headerValue;

        /// <summary>No sign-in: a server that allows anonymous reads.</summary>
        public static NtfyCredentials None { get; } = new (null);

        /// <summary>A user name and password (HTTP basic authentication).</summary>
        public static NtfyCredentials Basic (string username, string password)
        {
            ArgumentException.ThrowIfNullOrEmpty (username);
            ArgumentNullException.ThrowIfNull (password);
            return new ("Basic " + Convert.ToBase64String (Encoding.UTF8.GetBytes ($"{username}:{password}")));
        }

        /// <summary>An access token (bearer authentication).</summary>
        public static NtfyCredentials Bearer (string token)
        {
            ArgumentException.ThrowIfNullOrEmpty (token);
            return new ("Bearer " + token);
        }

        /// <summary>The value for the <c>Authorization</c> header, or null when there is no sign-in. Use it to build the request and nowhere else.</summary>
        public string? AuthorizationHeaderValue { get; }

        /// <inheritdoc />
        public override string ToString () => AuthorizationHeaderValue is null ? "no sign-in" : "sign-in [redacted]";
    }
}
