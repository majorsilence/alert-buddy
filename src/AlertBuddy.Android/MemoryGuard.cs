namespace AlertBuddy.Android
{
    /// <summary>
    /// An animated control (the beacon, at 30 frames a second) makes the framework's Android host pile up native memory that only the
    /// garbage collector's finalizers release, and the .NET GC never runs because the managed heap stays small. On a tablet-sized screen
    /// the process was killed for low memory within minutes (rss 2.3 GB). Collecting once a second keeps the native heap between about 130
    /// and 340 MB (measured on the emulator; every 3 seconds it still reached 670 MB). majorsilence/Majorsilence.Forms#371.
    /// // TEMP-SHIM (F27)
    /// </summary>
    internal static class MemoryGuard
    {
        private static System.Threading.Timer? timer;

        public static void Start ()
        {
            timer ??= new System.Threading.Timer (_ => {
                GC.Collect ();
                GC.WaitForPendingFinalizers ();
            }, null, TimeSpan.FromSeconds (1), TimeSpan.FromSeconds (1));
        }
    }
}
