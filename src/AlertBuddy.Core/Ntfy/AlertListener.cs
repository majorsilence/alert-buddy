using AlertBuddy.Core.Localization;
using AlertBuddy.Core.Settings;
using AlertBuddy.Core.Store;

namespace AlertBuddy.Core.Ntfy
{
    /// <summary>Something that can be told to start over with the settings as they are now.</summary>
    public interface IListenerControl
    {
        /// <summary>Whether a subscription is running.</summary>
        bool IsRunning { get; }

        /// <summary>Stops the subscription and starts it again from the current settings. Called after the server, topic or sign-in changes.</summary>
        Task RestartAsync ();
    }

    /// <summary>
    /// Owns the running subscription: reads the settings, opens the stream, feeds every event to the engine, and reports the connection
    /// state to it. The Android foreground service and the desktop head each own one. No UI, no platform code.
    /// </summary>
    public sealed class AlertListener : IListenerControl, IAsyncDisposable
    {
        private readonly object gate = new ();
        private readonly AlertEngine engine;
        private readonly SettingsService settings;
        private readonly ISecretStore secrets;
        private readonly HttpMessageHandler? handler;
        private readonly TimeProvider? time;
        private readonly Action<Exception>? failed;
        private CancellationTokenSource? cancellation;
        private Task? running;

        /// <summary>Creates a listener. It does nothing until <see cref="Start"/>.</summary>
        /// <param name="engine">Where events go.</param>
        /// <param name="settings">What to connect to.</param>
        /// <param name="secrets">The password or token.</param>
        /// <param name="handler">The HTTP handler; null uses the production one. Tests pass a scripted one.</param>
        /// <param name="timeProvider">For fake time in tests.</param>
        /// <param name="failed">Told when the engine throws on an event. The listener carries on: one bad message must not end it.</param>
        public AlertListener (AlertEngine engine, SettingsService settings, ISecretStore secrets, HttpMessageHandler? handler = null, TimeProvider? timeProvider = null, Action<Exception>? failed = null)
        {
            this.engine = engine ?? throw new ArgumentNullException (nameof (engine));
            this.settings = settings ?? throw new ArgumentNullException (nameof (settings));
            this.secrets = secrets ?? throw new ArgumentNullException (nameof (secrets));
            this.handler = handler;
            time = timeProvider;
            this.failed = failed;
        }

        /// <inheritdoc />
        public bool IsRunning {
            get {
                lock (gate)
                    return running is { IsCompleted: false };
            }
        }

        /// <summary>Starts listening from the current settings. Does nothing if already running, or if the server is not set up yet.</summary>
        public void Start ()
        {
            lock (gate) {
                if (running is { IsCompleted: false })
                    return;

                var current = settings.Current;
                if (string.IsNullOrWhiteSpace (current.ServerUrl) || string.IsNullOrWhiteSpace (current.Topic))
                    return;   // not set up yet: Home says so, and nothing is contacted

                var endpoint = NtfyEndpoint.Check (current.ServerUrl);
                if (!endpoint.IsValid || !NtfyTopic.IsValid (current.Topic)) {
                    engine.SetConnection (new ConnectionInfo (ConnectionState.Misconfigured, ConnectionProblem.InvalidAddress, endpoint.Problem ?? Loc.T ("the topic is not valid")));
                    return;
                }

                var subscription = new NtfySubscription (
                    new NtfySubscriptionOptions (endpoint.BaseUri!, current.Topic) {
                        // Read on every attempt, so a changed password applies at the next reconnect without a restart.
                        Credentials = CurrentCredentials,
                        LastMessageId = () => engine.LastMessageId,
                        ReplayWindow = current.ReplayWindow,
                        Unencrypted = endpoint.Unencrypted,
                    },
                    handler,
                    time);

                subscription.StateChanged += engine.SetConnection;

                cancellation = new CancellationTokenSource ();
                var token = cancellation.Token;
                running = Task.Run (() => PumpAsync (subscription, token), CancellationToken.None);
            }
        }

        /// <summary>Stops listening and waits for the subscription to finish.</summary>
        public async Task StopAsync ()
        {
            Task? toWait;
            lock (gate) {
                cancellation?.Cancel ();
                toWait = running;
            }

            if (toWait is not null) {
                try {
                    await toWait.ConfigureAwait (false);
                } catch (OperationCanceledException) {
                }
            }

            lock (gate) {
                cancellation?.Dispose ();
                cancellation = null;
                running = null;
            }
        }

        /// <inheritdoc />
        public async Task RestartAsync ()
        {
            await StopAsync ().ConfigureAwait (false);
            Start ();
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync () => await StopAsync ().ConfigureAwait (false);

        private async Task PumpAsync (NtfySubscription subscription, CancellationToken token)
        {
            try {
                await foreach (var evt in subscription.ReadAsync (token).ConfigureAwait (false)) {
                    try {
                        engine.Handle (evt);
                    } catch (Exception ex) {
                        // A bug handling one message must not end the listener: the next message is still worth hearing.
                        failed?.Invoke (ex);
                    }
                }
            } catch (OperationCanceledException) when (token.IsCancellationRequested) {
            }
        }

        private NtfyCredentials CurrentCredentials ()
        {
            var current = settings.Current;
            return current.Auth switch {
                AuthMode.Basic when current.Username.Length > 0 => NtfyCredentials.Basic (current.Username, secrets.Get (SecretKeys.Password) ?? ""),
                AuthMode.Token when secrets.Get (SecretKeys.Token) is { Length: > 0 } token => NtfyCredentials.Bearer (token),
                _ => NtfyCredentials.None,
            };
        }
    }
}
