using System;
using System.Reflection;
using UnityEditor;

namespace NuGetResolver.Editor {
  internal static class NuGetEditorUtility {
    private static readonly Lazy<PropertyInfo> ValidateReferencesProperty =
      new Lazy<PropertyInfo>(
        () => {
          var pluginImporterType = typeof(PluginImporter);
          var propertyInfo = pluginImporterType.GetProperty(
            "ValidateReferences",
            BindingFlags.Instance | BindingFlags.NonPublic);

          return propertyInfo;
        });

    public static bool GetValidateReferences(this PluginImporter importer) {
      var propertyInfo = ValidateReferencesProperty.Value;
      return (bool)propertyInfo.GetValue(importer);
    }

    public static void SetValidateReferences(this PluginImporter importer, bool value) {
      var propertyInfo = ValidateReferencesProperty.Value;
      propertyInfo.SetValue(importer, value);
    }
  }
}
