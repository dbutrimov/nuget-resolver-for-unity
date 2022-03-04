using System;
using UnityEditor;

namespace NuGetResolver.Editor {
  public class PluginAssetPostprocessor : AssetPostprocessor {
    private const StringComparison PathComparison = StringComparison.OrdinalIgnoreCase;

    public void OnPreprocessAsset() {
      if (!assetImporter.importSettingsMissing) {
        return;
      }

      var pluginImporter = assetImporter as PluginImporter;
      if (pluginImporter == null) {
        return;
      }

      var isValid = assetPath.StartsWith(NuGetEditor.PackageRuntimePath, PathComparison) ||
                    assetPath.StartsWith(NuGetEditor.PackageEditorPath, PathComparison);
      if (!isValid) {
        return;
      }

      var validateReferences = pluginImporter.GetValidateReferences();
      if (!validateReferences) {
        return;
      }

      pluginImporter.SetValidateReferences(false);
      EditorUtility.SetDirty(pluginImporter);
    }
  }
}
