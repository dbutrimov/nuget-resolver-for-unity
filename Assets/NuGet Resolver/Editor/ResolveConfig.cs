using System.Collections.Generic;
using System.Text.RegularExpressions;
using NuGet.Packaging;

namespace NuGetResolver.Editor {
  internal sealed class IgnoreEntry {
    private readonly Regex _regex;

    public IgnoreEntry(string pattern) {
      pattern = Regex.Escape(pattern);
      pattern = Regex.Replace(pattern, @"\\\*", ".*");
      pattern = $"^{pattern}$";

      _regex = new Regex(pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);
    }

    public bool IsMatch(string packageId) {
      return _regex.IsMatch(packageId);
    }
  }

  internal sealed class ResolveConfig {
    private IList<PackageReference> _packages;
    private IList<IgnoreEntry> _ignores;

    public IList<PackageReference> Packages {
      get => _packages ??= new List<PackageReference>();
      set => _packages = value;
    }

    public IList<IgnoreEntry> Ignores {
      get => _ignores ??= new List<IgnoreEntry>();
      set => _ignores = value;
    }
  }
}
