using System.Collections.Generic;
using NuGet.Frameworks;

namespace NuGetResolver.Editor {
  internal sealed class UnityFrameworkCompatibilityProvider : IFrameworkCompatibilityProvider {
    private static readonly NuGetFrameworkFullComparer FullComparer = new NuGetFrameworkFullComparer();

    private static readonly ISet<NuGetFramework> Supported =
      new HashSet<NuGetFramework>(FullComparer) {
#if UNITY_2021_2_OR_NEWER
        NuGetFramework.Parse("netstandard2.1"),
#endif
        NuGetFramework.Parse("netstandard2.0"),
        NuGetFramework.Parse("net45"),
        NuGetFramework.Parse("net452"),
        NuGetFramework.Parse("net46")
      };

    public bool IsCompatible(NuGetFramework framework, NuGetFramework other) {
      if (FullComparer.Equals(framework, other)) {
        return true;
      }

      if (framework.IsAny || other.IsAny) {
        return true;
      }

      if (!Supported.Contains(other)) {
        return false;
      }

      if (framework.IsUnsupported) {
        return true;
      }

      if (other.IsAgnostic) {
        return true;
      }

      return !other.IsUnsupported;
    }
  }
}
