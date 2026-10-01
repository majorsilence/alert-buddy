namespace AlertBuddy.ViewModels.Services
{
    /// <summary>Runs an action once when disposed: how a screen hands the screen base class an event it subscribed to.</summary>
    internal sealed class Unsubscribe (Action undo) : IDisposable
    {
        private bool done;

        public void Dispose ()
        {
            if (done)
                return;

            done = true;
            undo ();
        }
    }
}
