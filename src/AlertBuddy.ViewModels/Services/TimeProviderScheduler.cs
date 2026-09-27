namespace AlertBuddy.ViewModels.Services
{
    /// <summary>
    /// An <see cref="IScheduler"/> over a <see cref="TimeProvider"/>. A timer fires on some pool thread, so the action is posted to the UI
    /// thread through the dispatcher before it runs: a view model's timers never touch it from anywhere else.
    /// </summary>
    public sealed class TimeProviderScheduler (TimeProvider time, IUiDispatcher dispatcher) : IScheduler
    {
        /// <inheritdoc />
        public IDisposable Schedule (TimeSpan delay, Action action)
        {
            ArgumentNullException.ThrowIfNull (action);
            return time.CreateTimer (_ => dispatcher.Post (action), null, delay, Timeout.InfiniteTimeSpan);
        }

        /// <inheritdoc />
        public IDisposable Every (TimeSpan period, Action action)
        {
            ArgumentNullException.ThrowIfNull (action);
            return time.CreateTimer (_ => dispatcher.Post (action), null, period, period);
        }
    }
}
