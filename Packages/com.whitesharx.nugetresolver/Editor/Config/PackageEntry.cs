using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace NuGetResolver.Editor {
  internal sealed class PackageEntry {
    private static readonly IReadOnlyList<IgnoreEntry> EmptyIgnores = Array.Empty<IgnoreEntry>();

    public string Id { get; }
    public NuGetVersion Version { get; }
    public NuGetFramework TargetFramework { get; }
    public bool IsDevelopmentDependency { get; }
    public VersionRange AllowedVersions { get; }
    public IReadOnlyList<IgnoreEntry> Ignores { get; }

    public PackageEntry(
      string id,
      NuGetVersion version = null,
      NuGetFramework targetFramework = null,
      bool isDevelopmentDependency = false,
      VersionRange allowedVersions = null,
      IEnumerable<IgnoreEntry> ignores = null) {
      Id = id;
      Version = version;
      TargetFramework = targetFramework;
      IsDevelopmentDependency = isDevelopmentDependency;
      AllowedVersions = allowedVersions;
      Ignores = ignores?.ToList() ?? EmptyIgnores;
    }
  }
}
