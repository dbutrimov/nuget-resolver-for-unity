using System;
using System.Threading;
using UnityEditor;

namespace NuGetResolver.Editor {
  internal sealed class StartAssetEditingDisposable : IDisposable {
    private int _disposeCallCount = -1;

    public StartAssetEditingDisposable() {
      AssetDatabase.StartAssetEditing();
    }

    public void Dispose() {
      if (Interlocked.Increment(ref _disposeCallCount) > 0) {
        return;
      }

      AssetDatabase.StopAssetEditing();
    }
  }
}
