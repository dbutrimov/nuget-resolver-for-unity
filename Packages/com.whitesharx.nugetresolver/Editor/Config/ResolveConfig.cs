using System.Collections.Generic;

namespace NuGetResolver.Editor {
  internal sealed class ResolveConfig {
    private IList<PackageEntry> _packages;
    private IList<IgnoreEntry> _ignores;

    public IList<PackageEntry> Packages {
      get => _packages ??= new List<PackageEntry>();
      set => _packages = value;
    }

    public IList<IgnoreEntry> Ignores {
      get => _ignores ??= new List<IgnoreEntry>();
      set => _ignores = value;
    }
  }
}
