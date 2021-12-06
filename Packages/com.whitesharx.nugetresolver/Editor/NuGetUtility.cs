using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGetResolver.Editor {
  internal static class NuGetUtility {
    private static readonly StringComparer PackageIdComparer = StringComparer.OrdinalIgnoreCase;

    public static bool IsDevelopmentDependency(this IEnumerable<PackageReference> packages, string packageId) {
      var isDevelopmentDependency = false;
      foreach (var package in packages) {
        if (!PackageIdComparer.Equals(package.PackageIdentity.Id, packageId)) {
          continue;
        }

        if (!package.IsDevelopmentDependency) {
          return false;
        }

        isDevelopmentDependency = true;
      }

      return isDevelopmentDependency;
    }

    public static async Task<PackageIdentity> GetPreferredVersionAsync(
      this SourceRepository repository,
      string packageId,
      VersionRange allowedVersions,
      SourceCacheContext cacheContext,
      ILogger logger,
      CancellationToken cancellationToken = default) {
      var findResource = await repository.GetResourceAsync<FindPackageByIdResource>(cancellationToken);
      var versions = await findResource.GetAllVersionsAsync(
        packageId, cacheContext, logger, cancellationToken);
      if (versions == null) {
        return null;
      }

      NuGetVersion preferredVersion = null;
      foreach (var version in versions) {
        if (!allowedVersions.Satisfies(version)) {
          continue;
        }

        if (preferredVersion == null) {
          preferredVersion = version;
          continue;
        }

        if (version.IsPrerelease && !preferredVersion.IsPrerelease) {
          continue;
        }

        if (version > preferredVersion) {
          preferredVersion = version;
        }
      }

      return new PackageIdentity(packageId, preferredVersion);
    }

    public static async Task<PackageIdentity> GetPreferredVersionAsync(
      this IEnumerable<SourceRepository> repositories,
      string packageId,
      VersionRange allowedVersions,
      SourceCacheContext cacheContext,
      ILogger logger,
      CancellationToken cancellationToken = default) {
      foreach (var repository in repositories) {
        var preferredVersion = await repository.GetPreferredVersionAsync(
          packageId, allowedVersions, cacheContext, logger, cancellationToken);

        if (preferredVersion != null) {
          return preferredVersion;
        }
      }

      return null;
    }

    public static async Task<TreeNode<PackageReference>> GetDependenciesAsync(
      this SourceRepository repository,
      PackageReference package,
      NuGetFramework targetFramework,
      SourceCacheContext cacheContext,
      ILogger logger,
      ICollection<SourcePackageDependencyInfo> availablePackages,
      IReadOnlyCollection<IgnoreEntry> ignores,
      bool ignoreNode,
      CancellationToken cancellationToken = default) {
      ignoreNode = ignoreNode || (ignores?.Any(x => x.IsMatch(package.PackageIdentity.Id)) ?? false);

      var packageFramework = package.TargetFramework ?? targetFramework;

      var dependencyInfoResource = await repository.GetResourceAsync<DependencyInfoResource>(cancellationToken);
      var dependencyInfo = await dependencyInfoResource.ResolvePackage(
        package.PackageIdentity, packageFramework, cacheContext, logger, cancellationToken);

      if (dependencyInfo == null) {
        return null;
      }

      availablePackages.Add(dependencyInfo);

      var children = new List<TreeNode<PackageReference>>();
      foreach (var dependency in dependencyInfo.Dependencies) {
        var preferredVersion = await repository.GetPreferredVersionAsync(
          dependency.Id, dependency.VersionRange, cacheContext, logger, cancellationToken);
        if (preferredVersion == null) {
          preferredVersion = new PackageIdentity(dependency.Id, dependency.VersionRange.MinVersion);
        }

        var dependencyReference = new PackageReference(
          preferredVersion,
          packageFramework,
          package.IsUserInstalled,
          package.IsDevelopmentDependency,
          package.RequireReinstallation,
          dependency.VersionRange);

        var child = await repository.GetDependenciesAsync(
          dependencyReference, targetFramework, cacheContext, logger, availablePackages,
          ignores, ignoreNode, cancellationToken);

        children.Add(child);
      }

      return new TreeNode<PackageReference>(package, ignoreNode, children);
    }

    public static async Task<TreeNode<PackageReference>> GetDependenciesAsync(
      this IEnumerable<SourceRepository> repositories,
      PackageReference package,
      NuGetFramework targetFramework,
      SourceCacheContext cacheContext,
      ILogger logger,
      ICollection<SourcePackageDependencyInfo> availablePackages,
      IReadOnlyCollection<IgnoreEntry> ignores,
      CancellationToken cancellationToken = default) {
      foreach (var repository in repositories) {
        var result = await repository.GetDependenciesAsync(
          package, targetFramework, cacheContext, logger, availablePackages,
          ignores, false, cancellationToken);

        if (result != null) {
          return result;
        }
      }

      return null;
    }


    private static NuGetFramework SelectTargetFramework(
      FrameworkReducer reducer,
      params NuGetFramework[] frameworks) {
      var possibleFrameworks = frameworks.Where(x => x != null).ToList();
      if (possibleFrameworks.Count <= 0) {
        return null;
      }

      var framework = possibleFrameworks.First();
      if (possibleFrameworks.Count == 1) {
        return framework;
      }

      possibleFrameworks = possibleFrameworks.Skip(1).ToList();
      return reducer.GetNearest(framework, possibleFrameworks);
    }

    private static VersionRange SelectAllowedVersions(params VersionRange[] versions) {
      VersionRange result = null;
      foreach (var version in versions) {
        if (version == null) {
          continue;
        }

        if (result == null) {
          result = version;
          continue;
        }

        result = VersionRange.CommonSubSet(new[] { result, version });
      }

      return result;
    }

    private static NuGetVersion SelectPreferredVersion(VersionRange versionRange, params NuGetVersion[] versions) {
      var versionList = versions.Where(x => x != null).OrderBy(x => x).ToList();
      if (versionRange == null) {
        return versionList.FirstOrDefault();
      }

      return versionRange.FindBestMatch(versionList) ?? versionList.FirstOrDefault();
    }

    private static PackageEntry Merge(
      this PackageEntry source, PackageEntry other, FrameworkReducer frameworkReducer) {
      if (!PackageIdComparer.Equals(source.Id, other.Id)) {
        return source;
      }

      var allowedVersions = SelectAllowedVersions(source.AllowedVersions, other.AllowedVersions);

      var ignores = new List<IgnoreEntry>();
      ignores.AddRange(source.Ignores);
      ignores.AddRange(other.Ignores);

      return new PackageEntry(
        source.Id,
        SelectPreferredVersion(allowedVersions, source.Version, other.Version),
        SelectTargetFramework(frameworkReducer, source.TargetFramework, other.TargetFramework),
        source.IsDevelopmentDependency && other.IsDevelopmentDependency,
        allowedVersions,
        ignores);
    }

    public static ResolveConfig Merge(this ResolveConfig config, ResolveConfig other, FrameworkReducer frameworkReducer) {
      var packages = new List<PackageEntry>(config.Packages);
      foreach (var otherPackage in other.Packages) {
        var existsIndex = -1;
        PackageEntry existsPackage = null;
        for (var i = 0; i < packages.Count; i++) {
          var package = packages[i];
          if (!PackageIdComparer.Equals(package.Id, otherPackage.Id)) {
            continue;
          }

          existsIndex = i;
          existsPackage = package;
          break;
        }

        if (existsIndex < 0) {
          packages.Add(otherPackage);
        } else {
          packages[existsIndex] = existsPackage.Merge(otherPackage, frameworkReducer);
        }
      }

      var ignores = new List<IgnoreEntry>(config.Ignores);
      ignores.AddRange(other.Ignores);

      return new ResolveConfig(packages, ignores);
    }
  }
}
