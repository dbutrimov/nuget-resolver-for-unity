using System.Collections.Generic;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace NuGetResolver.Editor {
  internal sealed class PackageEntry {
    private IList<IgnoreEntry> _ignores;

    public string Id { get; set; }
    public NuGetVersion Version { get; set; }
    public NuGetFramework TargetFramework { get; set; }
    public bool IsDevelopmentDependency { get; set; }
    public VersionRange AllowedVersions { get; set; }

    public IList<IgnoreEntry> Ignores {
      get => _ignores ??= new List<IgnoreEntry>();
      set => _ignores = value;
    }
  }
}
