namespace AlertBuddy.Core.Store
{
    /// <summary>
    /// Writes a file so that a crash, a full disk or a killed process leaves either the old contents or the new, never half of each
    /// (PLAN.md section 5.3: write a temporary file, then rename). An Android service can be killed at any moment.
    /// </summary>
    public static class AtomicFile
    {
        /// <summary>Writes <paramref name="contents"/> to <paramref name="path"/> atomically, creating the directory if needed.</summary>
        public static void WriteAllBytes (string path, ReadOnlySpan<byte> contents)
        {
            ArgumentException.ThrowIfNullOrEmpty (path);

            var directory = Path.GetDirectoryName (Path.GetFullPath (path));
            if (!string.IsNullOrEmpty (directory))
                Directory.CreateDirectory (directory);

            var temporary = path + ".tmp";
            try {
                using (var stream = new FileStream (temporary, FileMode.Create, FileAccess.Write, FileShare.None)) {
                    stream.Write (contents);
                    stream.Flush (flushToDisk: true);   // the bytes must be on disk before the rename makes them the file
                }

                File.Move (temporary, path, overwrite: true);
            } catch {
                TryDelete (temporary);
                throw;
            }
        }

        private static void TryDelete (string path)
        {
            try {
                if (File.Exists (path))
                    File.Delete (path);
            } catch (IOException) {
                // Nothing more can be done about a leftover temporary file; the next write replaces it.
            } catch (UnauthorizedAccessException) {
            }
        }

        /// <summary>Moves an unreadable file aside so it can be examined, and so the next save does not trip over it.</summary>
        public static void QuarantineCorrupt (string path)
        {
            try {
                File.Move (path, path + ".corrupt", overwrite: true);
            } catch (IOException) {
            } catch (UnauthorizedAccessException) {
            }
        }
    }
}
