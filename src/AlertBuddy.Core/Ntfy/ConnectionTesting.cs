using AlertBuddy.Core.Localization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Authentication;

namespace AlertBuddy.Core.Ntfy
{
    /// <summary>The outcome of trying a server, in words a grown-up can act on.</summary>
    /// <param name="Success">Whether the server accepted the sign-in and knows the topic.</param>
    /// <param name="Problem">What went wrong, when it did.</param>
    /// <param name="Message">A sentence that says what happened and how to fix it.</param>
    public sealed record ConnectionTestResult (bool Success, ConnectionProblem Problem, string Message);

    /// <summary>Tries a server, a topic and a sign-in once, without starting to listen. The "Test connection" button of settings and first run.</summary>
    public interface IConnectionTester
    {
        /// <summary>Makes one short request and reports what it found.</summary>
        Task<ConnectionTestResult> TestAsync (string serverUrl, string topic, NtfyCredentials credentials, CancellationToken cancellationToken);
    }

    /// <summary>What stops the listener or a test: the same classification of a failed request, so both say the same thing about the same failure.</summary>
    internal static class ConnectionClassifier
    {
        /// <summary>The state and problem for an HTTP status that is not a success. Null for 200.</summary>
        public static (ConnectionState State, ConnectionProblem Problem, string Detail)? FromStatus (HttpStatusCode status)
        {
            var code = (int)status;
            return status switch {
                HttpStatusCode.OK => null,
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => (ConnectionState.AuthFailed, ConnectionProblem.Unauthorized, $"HTTP {code}"),
                HttpStatusCode.NotFound => (ConnectionState.Misconfigured, ConnectionProblem.TopicNotFound, Loc.T ("the topic was not found")),
                _ when code is >= 300 and < 400 => (ConnectionState.Misconfigured, ConnectionProblem.InvalidAddress, Loc.T ("the server redirected the request")),
                HttpStatusCode.TooManyRequests => (ConnectionState.Reconnecting, ConnectionProblem.RateLimited, Loc.T ("the server asked the app to slow down")),
                _ => (ConnectionState.Reconnecting, ConnectionProblem.ServerError, $"HTTP {code}"),
            };
        }

        /// <summary>The state and problem for a request that failed before any response.</summary>
        public static (ConnectionState State, ConnectionProblem Problem, string Detail) FromException (HttpRequestException ex)
        {
            // A bad certificate is a settings problem, not an outage: it will not fix itself until a grown-up acts.
            if (ex.HttpRequestError == HttpRequestError.SecureConnectionError || ex.InnerException is AuthenticationException || ex.InnerException?.InnerException is AuthenticationException) {
                var inner = ex.InnerException?.Message ?? ex.Message;
                return (ConnectionState.Misconfigured, ConnectionProblem.Certificate, Loc.F ("the secure connection failed: {0}", inner));
            }

            // No route at all is "offline"; a refused or reset connection is a server that is down or restarting.
            var offline = ex.InnerException is SocketException { SocketErrorCode: SocketError.NetworkUnreachable or SocketError.HostUnreachable or SocketError.HostNotFound or SocketError.TryAgain or SocketError.NetworkDown }
                || ex.HttpRequestError is HttpRequestError.NameResolutionError;

            return (offline ? ConnectionState.Offline : ConnectionState.Reconnecting, ConnectionProblem.Unreachable, Describe (ex));
        }

        /// <summary>A short factual description that is safe to show: the HTTP stack's message names the failure, never the request.</summary>
        public static string Describe (Exception ex)
            => ex is HttpRequestException { HttpRequestError: not HttpRequestError.Unknown } h ? h.HttpRequestError.ToString () : ex.GetType ().Name;
    }

    /// <summary>The real <see cref="IConnectionTester"/>: one request for the last minute of history, with a deadline.</summary>
    public sealed class NtfyConnectionTester : IConnectionTester
    {
        private static readonly TimeSpan Deadline = TimeSpan.FromSeconds (15);

        private readonly HttpMessageInvoker http;
        private readonly TimeProvider time;

        /// <summary>Creates a tester. Tests pass a scripted handler and a fake clock.</summary>
        public NtfyConnectionTester (HttpMessageHandler? handler = null, TimeProvider? timeProvider = null)
        {
            http = new HttpMessageInvoker (handler ?? NtfySubscription.CreateDefaultHandler (), disposeHandler: handler is null);
            time = timeProvider ?? TimeProvider.System;
        }

        /// <inheritdoc />
        public async Task<ConnectionTestResult> TestAsync (string serverUrl, string topic, NtfyCredentials credentials, CancellationToken cancellationToken)
        {
            var endpoint = NtfyEndpoint.Check (serverUrl);
            if (!endpoint.IsValid)
                return new ConnectionTestResult (false, ConnectionProblem.InvalidAddress, endpoint.Problem!);

            if (!NtfyTopic.IsValid (topic))
                return new ConnectionTestResult (false, ConnectionProblem.InvalidAddress, Loc.T ("A topic is letters, numbers, - and _, up to 64 characters."));

            var url = new Uri ($"{endpoint.BaseUri!.GetLeftPart (UriPartial.Path).TrimEnd ('/')}/{Uri.EscapeDataString (topic)}/json?poll=1&since=1m");

            try {
                using var request = new HttpRequestMessage (HttpMethod.Get, url);
                request.Headers.UserAgent.ParseAdd ("AlertBuddy/1");
                if (credentials.AuthorizationHeaderValue is { } header)
                    request.Headers.TryAddWithoutValidation ("Authorization", header);

                using var deadline = new CancellationTokenSource (Deadline, time);
                using var linked = CancellationTokenSource.CreateLinkedTokenSource (cancellationToken, deadline.Token);
                using var response = await http.SendAsync (request, linked.Token).ConfigureAwait (false);

                if (ConnectionClassifier.FromStatus (response.StatusCode) is not { } failure) {
                    return new ConnectionTestResult (true, ConnectionProblem.None,
                        endpoint.Unencrypted ? Loc.T ("Connected. The server accepted the sign-in and knows the topic. The connection is not encrypted.") : Loc.T ("Connected. The server accepted the sign-in and knows the topic."));
                }

                return new ConnectionTestResult (false, failure.Problem, failure.Problem switch {
                    ConnectionProblem.Unauthorized => Loc.T ("The server didn't accept the sign-in. Check the user name and password, or the token."),
                    ConnectionProblem.TopicNotFound => Loc.T ("The server doesn't know this topic. Check the topic name."),
                    ConnectionProblem.InvalidAddress => Loc.T ("The server redirected the request. Use the address it redirects to."),
                    ConnectionProblem.RateLimited => Loc.T ("The server asked the app to slow down. Try again in a moment."),
                    _ => Loc.F ("The server answered with {0}. Try again in a moment.", failure.Detail),
                });
            } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
                return new ConnectionTestResult (false, ConnectionProblem.Silent, Loc.T ("The server did not answer. Check the address, and that the server is running."));
            } catch (HttpRequestException ex) {
                var failure = ConnectionClassifier.FromException (ex);
                return new ConnectionTestResult (false, failure.Problem, failure.Problem == ConnectionProblem.Certificate
                    ? Loc.F ("The secure connection failed ({0}). Check the server's certificate.", failure.Detail)
                    : Loc.F ("Could not reach the server ({0}). Check the address and the network.", failure.Detail));
            }
        }
    }
}
