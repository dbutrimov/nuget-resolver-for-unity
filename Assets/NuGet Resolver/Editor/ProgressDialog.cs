using System;
using System.Threading;
using UnityEditor;

namespace NuGetResolver.Editor {
  internal struct ProgressReport {
    public string Info { get; set; }
    public float Progress { get; set; }
  }

  internal sealed class ProgressDialog : IDisposable {
    private readonly string title;
    private readonly CancellationTokenSource cancellation;

    private ProgressReport progressReport;
    private bool isDisposed;

    public ProgressDialog(string title, ProgressReport initialReport, CancellationTokenSource cancellation) {
      this.title = title;
      progressReport = initialReport;
      this.cancellation = cancellation;

      EditorApplication.update += OnUpdate;
      OnUpdate();
    }

    public void Update(ProgressReport report) {
      progressReport = report;
    }

    private void OnUpdate() {
      if (isDisposed || cancellation.IsCancellationRequested) {
        EditorApplication.update -= OnUpdate;
        EditorUtility.ClearProgressBar();
        return;
      }

      if (!EditorUtility.DisplayCancelableProgressBar(title, progressReport.Info, progressReport.Progress)) {
        return;
      }

      EditorApplication.update -= OnUpdate;
      EditorUtility.ClearProgressBar();

      cancellation.Cancel();
    }

    public void Dispose() {
      isDisposed = true;
    }
  }
}
