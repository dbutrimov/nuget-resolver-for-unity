// Copyright (c) 2021 White Sharx (https://whitesharx.com) - All Rights Reserved.
// Unauthorized copying of this file, via any medium is strictly prohibited.
// Proprietary and confidential.

using System;
using System.IO;
using System.Threading;

namespace NuGetResolver.Editor {
  internal sealed class DeleteDirectoryDisposable : IDisposable {
    private readonly string path;
    private int disposeCallCount = -1;

    public DeleteDirectoryDisposable(string path) {
      this.path = path;
    }

    public void Dispose() {
      if (Interlocked.Increment(ref disposeCallCount) > 0) {
        return;
      }

      Directory.Delete(path, true);
    }
  }
}
