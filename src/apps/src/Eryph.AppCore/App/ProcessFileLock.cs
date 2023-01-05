using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Eryph.App
{
    public sealed class ProcessFileLock : IDisposable, IAsyncDisposable
    {
        private readonly string _filePath;
        private readonly FileStream _lockStream;


        public ProcessFileLock(string filePath, IDictionary<string, object> metadata = null)
        {
            _filePath = filePath;

            var fileDir = Path.GetDirectoryName(filePath);

            if (fileDir != null)
            {
                if (!Directory.Exists(fileDir))
                    Directory.CreateDirectory(fileDir);
            }

            _lockStream = File.Open(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
            SetMetadata(metadata);
        }

        public ValueTask DisposeAsync()
        {
            try
            {
                Dispose();
                return default;
            }
            catch (Exception exception)
            {
                return new ValueTask(Task.FromException(exception));
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void ReleaseUnmanagedResources()
        {
            File.Delete(_filePath);
        }

        private void Dispose(bool disposing)
        {
            if (disposing) _lockStream?.Dispose();
            ReleaseUnmanagedResources();
        }

        ~ProcessFileLock()
        {
            Dispose(false);
        }

        public void SetMetadata(IDictionary<string, object> metadata)
        {
            _lockStream.Seek(0, SeekOrigin.Begin);

            var lockData = new Dictionary<string, object>();

            using var process = Process.GetCurrentProcess();
            lockData.Add("processName", process.ProcessName);
            lockData.Add("processId", process.Id);

            if (metadata != null)
                foreach (var kv in metadata
                    .Where(kv => kv.Key != "processName " && kv.Key != "processId"))
                    lockData.Add(kv.Key, kv.Value);

            var dataJson = JsonSerializer.Serialize(lockData);

            using var textWriter = new StreamWriter(_lockStream, Encoding.UTF8, 4096, true);
            textWriter.Write(dataJson);
            textWriter.Flush();
        }
    }


}