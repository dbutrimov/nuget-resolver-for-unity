using System;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NuGetResolver.Editor {
  internal sealed class ProgressWindow : EditorWindow, IProgressWindow {
    public static IProgressWindow Show(string title, IDisposable cancel) {
      var window = CreateInstance<ProgressWindow>();
      window.titleContent = new GUIContent(title);
      window.maxSize = window.minSize = new Vector2(640, 120);

      window._cancel = cancel;
      window.ShowUtility();

      return window;
    }


    private IDisposable _cancel;

    private ProgressBar _progressBar;
    private Label _descriptionLabel;
    private Button _cancelButton;

    private bool _isDirty;
    private ProgressReport _progressReport;
    private bool _isDisposed;
    private bool _isCancellationRequested;

    public void Report(ProgressReport report) {
      _progressReport = report;
      _isDirty = true;
    }

    public void CreateGUI() {
      var root = rootVisualElement;

      var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
        Path.Combine(Package.BasePath, "Editor/Content/Stylesheets/ResolveEditorWindow.uss"));
      root.styleSheets.Add(styleSheet);

      var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
        Path.Combine(Package.BasePath, "Editor/Content/Templates/ResolveEditorWindow.uxml"));
      var container = visualTree.Instantiate();

      container.AddToClassList("root");

      root.Add(container);

      _progressBar = container.Query<ProgressBar>("progressBar");
      _descriptionLabel = container.Query<Label>("descriptionLabel");
      _cancelButton = container.Query<Button>("cancelButton");

      _cancelButton.clicked += OnCancelButtonClicked;
    }

    private void OnCancelButtonClicked() {
      _isCancellationRequested = true;
      Close();
    }

    private void OnInspectorUpdate() {
      if (_isDisposed) {
        Close();
        return;
      }

      if (!_isDirty) {
        return;
      }

      var progress = _progressReport.Progress * 100;
      _progressBar.value = progress;
      _progressBar.title = $"{progress:0}%";
      _descriptionLabel.text = _progressReport.Info;
    }

    private void OnDestroy() {
      if (_isCancellationRequested) {
        _cancel.Dispose();
      }
    }

    public void Dispose() {
      _isDisposed = true;
    }
  }
}
