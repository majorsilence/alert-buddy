using AlertBuddy.Core.Settings;

namespace AlertBuddy.Shared.Platform
{
    /// <summary>
    /// A password and token held only for the process's lifetime, because no desktop OS credential store exists in the framework yet
    /// (register item F16). Never persisted, so it is safe on a machine other tools might read, but a grown-up has to re-enter the
    /// password or token every launch until F16 ships a real one.
    /// // TEMP-SHIM (F16)
    /// </summary>
    public sealed class InMemorySecretStore : ISecretStore
    {
        private readonly Dictionary<string, string> secrets = [];

        /// <inheritdoc/>
        public string? Get (string key) => secrets.GetValueOrDefault (key);

        /// <inheritdoc/>
        public void Set (string key, string value) => secrets[key] = value;

        /// <inheritdoc/>
        public void Remove (string key) => secrets.Remove (key);
    }
}
