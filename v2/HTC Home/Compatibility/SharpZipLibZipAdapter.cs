using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

// HTC Home 2.x used a very old SharpZipLib package only for sequentially
// reading .hhskin/.hhext ZIP archives. The package is no longer available
// from the configured NuGet feeds. Keep the tiny API surface the legacy host
// expects, but implement it with the ZIP support built into .NET Framework 4.8.
namespace ICSharpCode.SharpZipLib.Zip
{
    internal sealed class ZipEntry
    {
        internal ZipEntry(ZipArchiveEntry entry)
        {
            Name = ValidateEntryName(entry.FullName);
            IsDirectory = string.IsNullOrEmpty(entry.Name);
        }

        public string Name { get; private set; }
        public bool IsDirectory { get; private set; }

        private static string ValidateEntryName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            string normalized = name.Replace('/', Path.DirectorySeparatorChar);

            if (Path.IsPathRooted(normalized))
                throw new InvalidDataException("Archive entry contains an absolute path.");

            string[] parts = normalized.Split(Path.DirectorySeparatorChar);
            foreach (string part in parts)
            {
                if (part == "..")
                    throw new InvalidDataException("Archive entry escapes the destination directory.");
            }

            return normalized;
        }
    }

    internal sealed class ZipInputStream : IDisposable
    {
        private readonly ZipArchive archive;
        private readonly IEnumerator<ZipArchiveEntry> entries;
        private Stream currentEntryStream;
        private bool disposed;

        public ZipInputStream(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException("stream");

            archive = new ZipArchive(stream, ZipArchiveMode.Read, true);
            entries = archive.Entries.GetEnumerator();
        }

        public ZipEntry GetNextEntry()
        {
            ThrowIfDisposed();
            CloseCurrentEntry();

            if (!entries.MoveNext())
                return null;

            ZipArchiveEntry entry = entries.Current;
            ZipEntry result = new ZipEntry(entry);

            if (!result.IsDirectory)
                currentEntryStream = entry.Open();

            return result;
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            ThrowIfDisposed();

            if (currentEntryStream == null)
                return 0;

            return currentEntryStream.Read(buffer, offset, count);
        }

        public void Close()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (disposed)
                return;

            CloseCurrentEntry();
            entries.Dispose();
            archive.Dispose();
            disposed = true;
        }

        private void CloseCurrentEntry()
        {
            if (currentEntryStream == null)
                return;

            currentEntryStream.Dispose();
            currentEntryStream = null;
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException("ZipInputStream");
        }
    }
}
