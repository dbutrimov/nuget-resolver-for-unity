using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGetResolver.Editor {
  internal sealed class ResolveConfig {
    private static readonly IReadOnlyList<PackageEntry> EmptyPackages = Array.Empty<PackageEntry>();
    private static readonly IReadOnlyList<IgnoreEntry> EmptyIgnores = Array.Empty<IgnoreEntry>();

    public IReadOnlyList<PackageEntry> Packages { get; }
    public IReadOnlyList<IgnoreEntry> Ignores { get; }

    public ResolveConfig(IEnumerable<PackageEntry> packages, IEnumerable<IgnoreEntry> ignores) {
      Packages = packages?.ToList() ?? EmptyPackages;
      Ignores = ignores?.ToList() ?? EmptyIgnores;
    }

    public ResolveConfig() : this(null, null) {
    }
  }
}
