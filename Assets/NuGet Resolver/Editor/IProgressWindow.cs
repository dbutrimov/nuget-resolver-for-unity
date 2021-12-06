using System;

namespace NuGetResolver.Editor {
  internal struct ProgressReport {
    public string Info { get; set; }
    public float Progress { get; set; }
  }

  internal interface IProgressWindow : IProgress<ProgressReport>, IDisposable {
  }
}
