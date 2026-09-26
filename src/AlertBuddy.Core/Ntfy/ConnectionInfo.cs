namespace AlertBuddy.Core.Ntfy
{
    /// <summary>The listener's state (PLAN.md section 5.2). Each state has a sentence in the buddy's voice, written in the view models.</summary>
    public enum ConnectionState
    {
        /// <summary>Opening the first connection.</summary>
        Connecting,

        /// <summary>Connected and listening.</summary>
        Live,

        /// <summary>The connection dropped and the app is trying again, with backoff.</summary>
        Reconnecting,

        /// <summary>There is no network path to the server.</summary>
        Offline,

        /// <summary>The server refused the sign-in (401 or 403). Retried slowly, not in a fast loop.</summary>
        AuthFailed,

        /// <summary>The settings cannot work: a topic that does not exist, an unusable address, a certificate problem.</summary>
        Misconfigured,
    }

    /// <summary>Why a connection is not live, in terms a grown-up can act on.</summary>
    public enum ConnectionProblem
    {
        /// <summary>Nothing is wrong.</summary>
        None,

        /// <summary>The server could not be reached.</summary>
        Unreachable,

        /// <summary>Nothing, not even a keepalive, arrived for too long, so the connection was dropped.</summary>
        Silent,

        /// <summary>The server answered with an error.</summary>
        ServerError,

        /// <summary>The server asked the app to slow down (429).</summary>
        RateLimited,

        /// <summary>The sign-in was refused (401 or 403).</summary>
        Unauthorized,

        /// <summary>The topic was not found (404).</summary>
        TopicNotFound,

        /// <summary>The secure connection failed: an expired or untrusted certificate, or a name that does not match.</summary>
        Certificate,

        /// <summary>The server address is not usable, for example plain http to a public host.</summary>
        InvalidAddress,
    }

    /// <summary>A snapshot of the listener. Never carries credentials.</summary>
    /// <param name="State">The state.</param>
    /// <param name="Problem">Why it is not live, when it is not.</param>
    /// <param name="Detail">A short factual detail such as "HTTP 503" or "the certificate has expired", safe to show.</param>
    /// <param name="LastHeard">When anything, including a keepalive, last arrived. Drives "Last heard 3 min ago".</param>
    /// <param name="Unencrypted">The address is plain http on a private network: shown with an "unencrypted" badge.</param>
    public sealed record ConnectionInfo (
        ConnectionState State,
        ConnectionProblem Problem = ConnectionProblem.None,
        string? Detail = null,
        DateTimeOffset? LastHeard = null,
        bool Unencrypted = false)
    {
        /// <summary>The state before anything has happened.</summary>
        public static ConnectionInfo Initial { get; } = new (ConnectionState.Connecting);
    }
}
