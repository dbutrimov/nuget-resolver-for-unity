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

    public static async Task<IEnumerable<PackageReference>> GetDependenciesAsync(
      this SourceRepository repository,
      PackageReference package,
      NuGetFramework targetFramework,
      SourceCacheContext cacheContext,
      ILogger logger,
      ICollection<SourcePackageDependencyInfo> availablePackages,
      CancellationToken cancellationToken = default) {
      var packageFramework = package.TargetFramework ?? targetFramework;

      var dependencyInfoResource = await repository.GetResourceAsync<DependencyInfoResource>(cancellationToken);
      var dependencyInfo = await dependencyInfoResource.ResolvePackage(
        package.PackageIdentity, packageFramework, cacheContext, logger, cancellationToken);

      if (dependencyInfo == null) {
        return null;
      }

      availablePackages.Add(dependencyInfo);

      var result = new List<PackageReference>();
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

        result.Add(dependencyReference);

        var dependencies = await repository.GetDependenciesAsync(
          dependencyReference, targetFramework, cacheContext, logger, availablePackages, cancellationToken);
        result.AddRange(dependencies);
      }

      return result;
    }

    public static async Task<IEnumerable<PackageReference>> GetDependenciesAsync(
      this IEnumerable<SourceRepository> repositories,
      PackageReference package,
      NuGetFramework targetFramework,
      SourceCacheContext cacheContext,
      ILogger logger,
      ICollection<SourcePackageDependencyInfo> availablePackages,
      CancellationToken cancellationToken = default) {
      foreach (var repository in repositories) {
        var dependencies = await repository.GetDependenciesAsync(
          package, targetFramework, cacheContext, logger, availablePackages, cancellationToken);
        if (dependencies != null) {
          return dependencies;
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

    private static PackageReference Update(
      this PackageReference source, PackageReference other, FrameworkReducer frameworkReducer) {
      var sourceIdentity = source.PackageIdentity;
      var otherIdentity = other.PackageIdentity;
      if (!otherIdentity.Equals(sourceIdentity)) {
        return source;
      }

      var allowedVersions = SelectAllowedVersions(source.AllowedVersions, other.AllowedVersions);
      var preferredVersion = SelectPreferredVersion(allowedVersions, sourceIdentity.Version, otherIdentity.Version);

      return new PackageReference(
        new PackageIdentity(sourceIdentity.Id, preferredVersion),
        SelectTargetFramework(frameworkReducer, source.TargetFramework, other.TargetFramework),
        source.IsUserInstalled || other.IsUserInstalled,
        source.IsDevelopmentDependency && other.IsDevelopmentDependency,
        source.RequireReinstallation || other.RequireReinstallation,
        allowedVersions);
    }

    public static void Update(this ResolveConfig config, ResolveConfig other, FrameworkReducer frameworkReducer) {
      var packages = config.Packages;
      foreach (var otherPackage in other.Packages) {
        var existsIndex = -1;
        PackageReference existsPackage = null;
        for (var i = 0; i < packages.Count; i++) {
          var package = packages[i];
          if (!package.PackageIdentity.Equals(otherPackage.PackageIdentity)) {
            continue;
          }

          existsIndex = i;
          existsPackage = package;
          break;
        }

        if (existsIndex < 0) {
          packages.Add(otherPackage);
        } else {
          packages[existsIndex] = existsPackage.Update(otherPackage, frameworkReducer);
        }
      }

      config.Packages = packages;

      config.Ignores.AddRange(other.Ignores);
    }

    public static bool IsIgnored(this ResolveConfig config, string packageId) {
      return config.Ignores.Any(x => x.IsMatch(packageId));
    }
  }
}
