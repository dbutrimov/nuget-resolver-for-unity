using System;
using System.Threading;
using UnityEditor;

namespace NuGetResolver.Editor {
  internal struct ProgressReport {
    public string Info { get; set; }
    public float Progress { get; set; }
  }

  internal sealed class ProgressDialog : IDisposable {
    private readonly string _title;
    private readonly CancellationTokenSource _cancellation;

    private ProgressReport _progressReport;
    private bool _isDisposed;

    public ProgressDialog(string title, ProgressReport initialReport, CancellationTokenSource cancellation) {
      _title = title;
      _progressReport = initialReport;
      _cancellation = cancellation;

      EditorApplication.update += OnUpdate;
      OnUpdate();
    }

    public void Update(ProgressReport report) {
      _progressReport = report;
    }

    private void OnUpdate() {
      if (_isDisposed || _cancellation.IsCancellationRequested) {
        EditorApplication.update -= OnUpdate;
        EditorUtility.ClearProgressBar();
        return;
      }

      if (!EditorUtility.DisplayCancelableProgressBar(_title, _progressReport.Info, _progressReport.Progress)) {
        return;
      }

      EditorApplication.update -= OnUpdate;
      EditorUtility.ClearProgressBar();

      _cancellation.Cancel();
    }

    public void Dispose() {
      _isDisposed = true;
    }
  }
}
