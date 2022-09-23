using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Packaging.Signing;
using NuGet.Protocol.Core.Types;
using NuGet.Resolver;
using UnityEditor;
using UnityEngine;
using ILogger = NuGet.Common.ILogger;

namespace NuGetResolver.Editor {
  public static class NuGetEditor {
    private const string NuGetPackageId = "org.nuget.packages";
    private static readonly Version NuGetPackageVersion = new Version(1, 0, 0);

    public static string PackagePath => Path.Combine("Packages", NuGetPackageId);
    public static string PackageRuntimePath => Path.Combine(PackagePath, "Runtime");
    public static string PackageEditorPath => Path.Combine(PackagePath, "Editor");


    private static ISettings LoadSettings(ILogger logger = null) {
      logger ??= NullLogger.Instance;

      var configFileName = Path.GetFullPath("NuGet.config");
      if (File.Exists(configFileName)) {
        logger.LogInformation($"Load specific settings: {configFileName}");
        return Settings.LoadSpecificSettings(null, configFileName);
      }

      logger.LogInformation("Load default settings");
      return Settings.LoadDefaultSettings(null);
    }


    private static NuGetFramework GetTargetFramework() {
      var buildTargetGroup = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);
      var apiCompatibilityLevel = PlayerSettings.GetApiCompatibilityLevel(buildTargetGroup);
      return apiCompatibilityLevel switch {
#if UNITY_2021_2_OR_NEWER
        ApiCompatibilityLevel.NET_Standard => NuGetFramework.Parse("netstandard2.1"),
#else
        ApiCompatibilityLevel.NET_Standard_2_0 => NuGetFramework.Parse("netstandard2.0"),
#endif
        ApiCompatibilityLevel.NET_4_6 => NuGetFramework.Parse("net46"),
        _ => throw new InvalidOperationException($"Unsupported API Compatibility Level: {apiCompatibilityLevel}")
      };
    }

    private static async Task<IList<ResolveConfig>> ReadDependenciesAsync(
      IProgress<float> progress = null,
      ILogger logger = null,
      CancellationToken cancellationToken = default) {
      logger ??= NullLogger.Instance;

      var configReader = new ResolveConfigReader();
      var result = new List<ResolveConfig>();

      var assetNameRegex = new Regex(@"^.*NuGetPackages\.xml$", RegexOptions.IgnoreCase | RegexOptions.Singleline);
      var assetGuids = AssetDatabase.FindAssets("NuGetPackages");
      for (var i = 0; i < assetGuids.Length; i++) {
        var assetPath = AssetDatabase.GUIDToAssetPath(assetGuids[i]);

        var fileName = Path.GetFileName(assetPath);
        if (!assetNameRegex.IsMatch(fileName)) {
          progress?.Report((float)(i + 1) / assetGuids.Length);
          continue;
        }

        var textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
        if (textAsset == null) {
          logger.LogWarning($"No TextAsset at path {assetPath}");
        } else {
          logger.LogInformation(assetPath);

          using var reader = new StringReader(textAsset.text);
          var config = await Task.Run(() => configReader.Read(reader), cancellationToken);
          result.Add(config);
        }

        progress?.Report((float)(i + 1) / assetGuids.Length);
      }

      return result;
    }

    private static async Task ResolveAsync(
      IProgress<ProgressReport> progress = null,
      ILogger logger = null,
      CancellationToken cancellationToken = default) {
      const DependencyBehavior dependencyBehavior = DependencyBehavior.Highest;

      var targetFramework = GetTargetFramework();

      var projectPath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
      var tempPath = Path.Combine(projectPath, "Temp");

      logger ??= NullLogger.Instance;

      var progressSegment = new ProgressSegment(0, 0.1f);
      var progressReport = new ProgressReport { Progress = 0, Info = "Read packages..." };

      progress?.Report(progressReport);

      var frameworkReducer = new FrameworkReducer(
        DefaultFrameworkNameProvider.Instance,
        new UnityFrameworkCompatibilityProvider());

      var resolveConfigs = await ReadDependenciesAsync(
        new Progress<float>(
          p => {
            progressReport.Progress = progressSegment.Evaluate(p);
            progress?.Report(progressReport);
          }),
        logger,
        cancellationToken);


      progressSegment = new ProgressSegment(0.1f, 0.7f);
      progressReport.Info = "Read dependencies...";
      progressReport.Progress = progressSegment.Evaluate(0);
      progress?.Report(progressReport);

      var availablePackages = new HashSet<SourcePackageDependencyInfo>(PackageIdentityComparer.Default);

      using var cacheContext = new SourceCacheContext();
      var settings = LoadSettings(logger);
      var sourceProvider = new PackageSourceProvider(settings);
      var repositoryProvider = new SourceRepositoryProvider(sourceProvider, Repository.Provider.GetCoreV3());
      var repositories = repositoryProvider.GetRepositories().ToList();


      var preferredVersions = new HashSet<PackageIdentity>(PackageIdentityComparer.Default);
      var dependencies = new List<PackageReference>();

      var packageCount = resolveConfigs.Sum(c => c.Packages.Count);
      var packageNumber = 0;
      foreach (var resolveConfig in resolveConfigs) {
        foreach (var targetPackage in resolveConfig.Packages) {
          packageNumber++;

          var packageIdentity = new PackageIdentity(targetPackage.Id, targetPackage.Version);

          progressReport.Info = $"Read dependencies ({packageNumber}/{packageCount}): {packageIdentity.Id}";
          progressReport.Progress = progressSegment.Evaluate((float)(packageNumber - 1) / packageCount);
          progress?.Report(progressReport);

          PackageIdentity preferredVersion;
          var allowedVersions = targetPackage.AllowedVersions;
          if (allowedVersions == null) {
            preferredVersion = packageIdentity;
          } else {
            preferredVersion = await repositories.GetPreferredVersionAsync(
              packageIdentity.Id, allowedVersions, cacheContext, logger, cancellationToken);
            if (preferredVersion == null) {
              preferredVersion = packageIdentity;
            }
          }

          var packageReference = new PackageReference(
            preferredVersion,
            targetPackage.TargetFramework ?? targetFramework,
            true,
            targetPackage.IsDevelopmentDependency,
            false,
            targetPackage.AllowedVersions);

          dependencies.Add(packageReference);
          preferredVersions.Add(preferredVersion);

          var ignores = new List<IgnoreEntry>(resolveConfig.Ignores);
          ignores.AddRange(targetPackage.Ignores);

          var packageDependencies = await repositories.GetDependenciesAsync(
            packageReference, targetFramework, cacheContext, availablePackages, ignores,
            logger, cancellationToken);

          if (packageDependencies != null) {
            dependencies.AddRange(packageDependencies);
          }
        }
      }


      progressSegment = new ProgressSegment(0.7f, 0.9f);
      progressReport.Info = "Resolve packages...";
      progressReport.Progress = progressSegment.Evaluate(0);
      progress?.Report(progressReport);

      var targetIds = resolveConfigs.SelectMany(x => x.Packages).Select(x => x.Id).ToList();
      var resolverContext = new PackageResolverContext(
        dependencyBehavior,
        targetIds,
        Enumerable.Empty<string>(),
        dependencies,
        preferredVersions,
        availablePackages,
        repositories.Select(x => x.PackageSource),
        logger);

      var resolver = new PackageResolver();
      var packagesToInstall = resolver.Resolve(resolverContext, cancellationToken)
        .Where(p => dependencies.Any(r => StringComparer.OrdinalIgnoreCase.Equals(r.PackageIdentity.Id, p.Id)))
        .Select(p => availablePackages.Single(x => PackageIdentityComparer.Default.Equals(x, p)))
        .ToList();

      using (var writer = File.CreateText(Path.GetFullPath(Path.Combine(tempPath, "NuGetPackages.txt")))) {
        foreach (var package in packagesToInstall.OrderBy(p => p.Id)) {
          await writer.WriteAsync(package.Id);
          if (package.HasVersion) {
            await writer.WriteAsync($".{package.Version}");
          }

          await writer.WriteLineAsync();
        }
      }

      var nugetPath = Path.Combine(tempPath, "NuGet");
      using var deleteNugetDir = new DeleteDirectoryDisposable(nugetPath);

      var packagePathResolver = new PackagePathResolver(nugetPath);
      var packageExtractionContext = new PackageExtractionContext(
        PackageSaveMode.Defaultv3,
        XmlDocFileSaveMode.None,
        ClientPolicyContext.GetClientPolicy(settings, logger),
        logger);

      var packagesTempPath = Path.Combine(tempPath, $"NuGetResolver-{Guid.NewGuid():N}");
      using var deleteTempDir = new DeleteDirectoryDisposable(packagesTempPath);

      var tempRuntimeDir = DirectoryUtility.Create(Path.Combine(packagesTempPath, "Runtime"), true);
      var tempEditorDir = DirectoryUtility.Create(Path.Combine(packagesTempPath, "Editor"), true);

      var downloadContext = new PackageDownloadContext(cacheContext);
      var globalPackagesFolder = SettingsUtility.GetGlobalPackagesFolder(settings);

      for (var i = 0; i < packagesToInstall.Count; i++) {
        var packageToInstall = packagesToInstall[i];

        progressReport.Info = $"Install packages ({i + 1}/{packagesToInstall.Count}): {packageToInstall.Id}";
        progressReport.Progress = progressSegment.Evaluate((float)i / packagesToInstall.Count);
        progress?.Report(progressReport);

        PackageReaderBase packageReader;
        var installedPath = packagePathResolver.GetInstalledPath(packageToInstall);
        if (installedPath == null) {
          var downloadResource =
            await packageToInstall.Source.GetResourceAsync<DownloadResource>(cancellationToken);
          var downloadResult = await downloadResource.GetDownloadResourceResultAsync(
            packageToInstall,
            downloadContext,
            globalPackagesFolder,
            logger,
            cancellationToken);

          await PackageExtractor.ExtractPackageAsync(
            downloadResult.PackageSource,
            downloadResult.PackageStream,
            packagePathResolver,
            packageExtractionContext,
            cancellationToken);

          installedPath = packagePathResolver.GetInstalledPath(packageToInstall);
          packageReader = downloadResult.PackageReader;
        } else {
          packageReader = new PackageFolderReader(installedPath);
        }

        var packageIdentity = await packageReader.GetIdentityAsync(cancellationToken);

        var libItems = (await packageReader.GetLibItemsAsync(cancellationToken)).ToList();
        var nearestFramework = frameworkReducer.GetNearest(targetFramework, libItems.Select(x => x.TargetFramework));
        var libsPath = libItems
          .Where(x => x.TargetFramework.Equals(nearestFramework))
          .SelectMany(x => x.Items);

        var isDevelopmentDependency = dependencies.IsDevelopmentDependency(packageIdentity.Id);
        var tempDir = isDevelopmentDependency ? tempEditorDir : tempRuntimeDir;
        var tempPackagePath = Path.Combine(tempDir.FullName, packageIdentity.ToString());

        foreach (var filePath in libsPath) {
          var srcPath = Path.Combine(installedPath, filePath);
          var dstPath = Path.Combine(tempPackagePath, filePath);

          var dstDirPath = Path.GetDirectoryName(dstPath);
          if (dstDirPath != null && !Directory.Exists(dstDirPath)) {
            Directory.CreateDirectory(dstDirPath);
          }

          File.Copy(srcPath, dstPath);
        }
      }


      progressSegment = new ProgressSegment(0.9f, 1.0f);
      progressReport.Info = "Copy packages...";
      progressReport.Progress = progressSegment.Evaluate(0);
      progress?.Report(progressReport);

      using var assetsLock = new StartAssetEditingDisposable();

      var packageDir = DirectoryUtility.GetOrCreate(Path.Combine(projectPath, PackagePath));

      var packageFileContent = $@"{{
  ""name"": ""{NuGetPackageId}"",
  ""version"": ""{NuGetPackageVersion.ToString(3)}"",
  ""displayName"": ""NuGet Packages""
}}";

      await Task.Run(
        () => {
          var packageFilePath = Path.Combine(packageDir.FullName, "package.json");
          File.WriteAllText(packageFilePath, packageFileContent, new UTF8Encoding(false));

          var runtimeDir = DirectoryUtility.Create(Path.Combine(projectPath, PackageRuntimePath), true);
          tempRuntimeDir.CopyTo(runtimeDir.FullName, true);

          var editorDir = DirectoryUtility.Create(Path.Combine(projectPath, PackageEditorPath), true);
          tempEditorDir.CopyTo(editorDir.FullName, true);
        },
        cancellationToken);

      assetsLock.Dispose();
      AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
    }


    [MenuItem("NuGet/Resolve")]
    public static async void Resolve() {
      var logger = new UnityNuGetLogger(LogLevel.Verbose);

      const string title = "Resolve NuGet packages";
      using var cts = new CancellationTokenSource();

      var cancel = new CancellationDisposable(cts);
      using var progressBar = new ProgressBarDialog(title, cancel);

      var progress = new Progress<ProgressReport>(
        report => {
          progressBar.Info = report.Info;
          progressBar.Progress = report.Progress;
        });

      try {
        await ResolveAsync(progress, logger, cts.Token);
      } catch (Exception ex) {
        logger.LogError(ex.ToString());
        throw;
      }
    }
  }
}
