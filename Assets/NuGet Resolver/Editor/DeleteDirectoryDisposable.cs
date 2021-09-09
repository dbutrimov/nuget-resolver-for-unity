using System;
using System.IO;
using System.Threading;

namespace NuGetResolver.Editor {
  internal sealed class DeleteDirectoryDisposable : IDisposable {
    private readonly string _path;
    private int _disposeCallCount = -1;

    public DeleteDirectoryDisposable(string path) {
      _path = path;
    }

    public void Dispose() {
      if (Interlocked.Increment(ref _disposeCallCount) > 0) {
        return;
      }

      Directory.Delete(_path, true);
    }
  }
}
