using System.Text.RegularExpressions;

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
}
