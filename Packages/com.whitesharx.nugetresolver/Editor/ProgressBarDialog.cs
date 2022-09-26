using System;
using UnityEditor;

namespace NuGetResolver.Editor {
  internal sealed class ProgressBarDialog : IDisposable {
    private readonly string _title;
    private readonly IDisposable _cancel;
    private readonly bool _canCancel;

    private bool _isDisposed;

    public float Progress { get; set; }
    public string Info { get; set; }

    public ProgressBarDialog(string title, IDisposable cancel = null) {
      _title = title;
      _cancel = cancel;
      _canCancel = _cancel != null;

      EditorApplication.update += OnEditorUpdate;
      OnEditorUpdate();
    }

    private void OnEditorUpdate() {
      if (_isDisposed) {
        EditorApplication.update -= OnEditorUpdate;
        EditorUtility.ClearProgressBar();
        return;
      }

      if (_canCancel) {
        if (EditorUtility.DisplayCancelableProgressBar(_title, Info, Progress)) {
          _cancel.Dispose();
        }
      } else {
        EditorUtility.DisplayProgressBar(_title, Info, Progress);
      }
    }

    public void Dispose() {
      _isDisposed = true;
    }
  }
}
