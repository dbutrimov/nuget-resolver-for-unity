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
    private IList<PackageReference> packages;
    private IList<IgnoreEntry> ignores;

    public IList<PackageReference> Packages {
      get => packages ??= new List<PackageReference>();
      set => packages = value;
    }

    public IList<IgnoreEntry> Ignores {
      get => ignores ??= new List<IgnoreEntry>();
      set => ignores = value;
    }
  }
}
