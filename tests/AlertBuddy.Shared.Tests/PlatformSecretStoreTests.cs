using AlertBuddy.Core.Settings;
using AlertBuddy.Shared.Platform;
using Xunit;

namespace AlertBuddy.Shared.Tests
{
    public class PlatformSecretStoreTests
    {
        private sealed class Backing
        {
            public readonly Dictionary<string, string> Values = [];
            public int Reads;

            public PlatformSecretStore Store (bool supported = true)
                => new (supported, key => { Reads++; return Values.GetValueOrDefault (key); }, (k, v) => Values[k] = v, k => Values.Remove (k));
        }

        [Fact]
        public void Set_PersistsThroughTheBacking_AndAFreshStoreReadsItBack ()
        {
            var backing = new Backing ();
            backing.Store ().Set (SecretKeys.Token, "tk_example");

            Assert.Equal ("tk_example", backing.Values[SecretKeys.Token]);
            Assert.Equal ("tk_example", backing.Store ().Get (SecretKeys.Token));
        }

        [Fact]
        public void Get_ReadsTheBackingOnce_ThenServesTheCache ()
        {
            var backing = new Backing ();
            backing.Values[SecretKeys.Password] = "hunter2";
            var store = backing.Store ();

            Assert.Equal ("hunter2", store.Get (SecretKeys.Password));
            Assert.Equal ("hunter2", store.Get (SecretKeys.Password));
            Assert.Equal (1, backing.Reads);
        }

        [Fact]
        public void Remove_ClearsBothTheCacheAndTheBacking ()
        {
            var backing = new Backing ();
            var store = backing.Store ();
            store.Set (SecretKeys.Token, "tk_example");

            store.Remove (SecretKeys.Token);

            Assert.Null (store.Get (SecretKeys.Token));
            Assert.Empty (backing.Values);
        }

        [Fact]
        public void WhereThePlatformHasNoStore_SecretsLiveInMemoryOnly_AndNothingIsWritten ()
        {
            var backing = new Backing ();
            var store = backing.Store (supported: false);

            store.Set (SecretKeys.Password, "hunter2");

            Assert.Equal ("hunter2", store.Get (SecretKeys.Password));
            Assert.Empty (backing.Values);
            Assert.Null (backing.Store (supported: false).Get (SecretKeys.Password));
        }
    }
}
