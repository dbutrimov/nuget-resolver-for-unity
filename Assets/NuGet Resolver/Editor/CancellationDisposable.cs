using System;
using System.Threading;

namespace NuGetResolver.Editor {
  internal sealed class CancellationDisposable : IDisposable {
    private CancellationTokenSource _cancellation;

    public CancellationDisposable(CancellationTokenSource cancellation) {
      _cancellation = cancellation;
    }

    ~CancellationDisposable() {
      Dispose(false);
    }

    private void Dispose(bool disposing) {
      var cancellation = Interlocked.Exchange(ref _cancellation, null);
      if (!disposing) {
        return;
      }

      cancellation?.Cancel();
    }

    public void Dispose() {
      Dispose(true);
      GC.SuppressFinalize(this);
    }
  }
}
