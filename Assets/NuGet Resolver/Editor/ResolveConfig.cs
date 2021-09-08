using System.Collections.Generic;
using System.Text.RegularExpressions;
using NuGet.Packaging;

namespace NuGetResolver.Editor {
  internal sealed class IgnoreEntry {
    private readonly Regex regex;

    public IgnoreEntry(string pattern) {
      pattern = Regex.Escape(pattern);
      pattern = Regex.Replace(pattern, @"\\\*", ".*");
      pattern = $"^{pattern}$";

      regex = new Regex(pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);
    }

    public bool IsMatch(string packageId) {
      return regex.IsMatch(packageId);
    }
  }

  internal sealed class ResolveConfig {
    private ISet<PackageReference> packages;
    private ISet<IgnoreEntry> ignores;

    public ISet<PackageReference> Packages {
      get => packages ?? (packages = new HashSet<PackageReference>());
      set => packages = value;
    }

    public ISet<IgnoreEntry> Ignores {
      get => ignores ?? (ignores = new HashSet<IgnoreEntry>());
      set => ignores = value;
    }
  }
}
