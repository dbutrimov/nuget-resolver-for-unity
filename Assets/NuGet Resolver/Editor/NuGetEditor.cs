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


    private readonly struct ProgressSegment {
      private readonly float min;
      private readonly float max;

      public ProgressSegment(float min, float max) {
        this.min = min;
        this.max = max;
      }

      public float Evaluate(float progress) {
        return min + (max - min) * progress;
      }
    }


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

    private static async Task ResolveAsync(
      IProgress<ProgressReport> progress = null,
      ILogger logger = null,
      CancellationToken cancellationToken = default) {
      const DependencyBehavior dependencyBehavior = DependencyBehavior.Highest;
      var targetFramework = NuGetFramework.Parse("netstandard2.0");
      var projectPath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

      logger ??= NullLogger.Instance;

      var progressSegment = new ProgressSegment(0, 0.1f);
      var progressReport = new ProgressReport { Progress = 0, Info = "Read packages..." };

      progress?.Report(progressReport);

      var assetsDir = new DirectoryInfo(Path.Combine(projectPath, "Assets"));
      logger.LogInformation(assetsDir.FullName);

      var configReader = new ResolveConfigReader();
      var resolveConfig = new ResolveConfig();

      var assetNameRegex = new Regex(@"^.*NuGetPackages\.xml$", RegexOptions.IgnoreCase | RegexOptions.Singleline);
      var assetGuids = AssetDatabase.FindAssets("NuGetPackages");
      for (var i = 0; i < assetGuids.Length; i++) {
        var assetPath = AssetDatabase.GUIDToAssetPath(assetGuids[i]);

        var fileName = Path.GetFileName(assetPath);
        if (!assetNameRegex.IsMatch(fileName)) {
          continue;
        }

        var textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
        if (textAsset == null) {
          logger.LogWarning($"No TextAsset at path {assetPath}");
        } else {
          logger.LogInformation(assetPath);

          using var reader = new StringReader(textAsset.text);
          var config = await Task.Run(() => configReader.Read(reader), cancellationToken);
          resolveConfig.Update(config);
        }

        progressReport.Progress = progressSegment.Evaluate((float)(i + 1) / assetGuids.Length);
        progress?.Report(progressReport);
      }


      var targetPackages = resolveConfig.Packages;

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
      var references = new List<PackageReference>();

      var packageNumber = 0;
      foreach (var targetPackage in targetPackages) {
        packageNumber++;

        var packageIdentity = targetPackage.PackageIdentity;

        progressReport.Info = $"Read dependencies ({packageNumber}/{targetPackages.Count}): {packageIdentity.Id}";
        progressReport.Progress = progressSegment.Evaluate((float)(packageNumber - 1) / targetPackages.Count);
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
          targetPackage.TargetFramework,
          targetPackage.IsUserInstalled,
          targetPackage.IsDevelopmentDependency,
          targetPackage.RequireReinstallation,
          targetPackage.AllowedVersions);

        preferredVersions.Add(preferredVersion);
        references.Add(packageReference);

        var dependencies = await repositories.GetDependenciesAsync(
          packageReference, targetFramework, cacheContext, logger,
          availablePackages, cancellationToken);
        references.AddRange(dependencies);
      }


      progressSegment = new ProgressSegment(0.7f, 0.9f);
      progressReport.Info = "Resolve packages...";
      progressReport.Progress = progressSegment.Evaluate(0);
      progress?.Report(progressReport);

      var targetIds = targetPackages.Select(x => x.PackageIdentity.Id).ToList();
      var resolverContext = new PackageResolverContext(
        dependencyBehavior,
        targetIds,
        targetIds,
        references,
        preferredVersions,
        availablePackages,
        repositories.Select(x => x.PackageSource),
        logger);

      var resolver = new PackageResolver();
      var packagesToInstall = resolver.Resolve(resolverContext, cancellationToken)
        .Select(p => availablePackages.Single(x => PackageIdentityComparer.Default.Equals(x, p)))
        .ToList();

      var nugetPath = Path.GetFullPath(".nuget");
      using var deleteNugetDir = new DeleteDirectoryDisposable(nugetPath);

      var packagePathResolver = new PackagePathResolver(nugetPath);
      var packageExtractionContext = new PackageExtractionContext(
        PackageSaveMode.Defaultv3,
        XmlDocFileSaveMode.None,
        ClientPolicyContext.GetClientPolicy(settings, logger),
        logger);

      var frameworkReducer = new FrameworkReducer(
        DefaultFrameworkNameProvider.Instance,
        new UnityFrameworkCompatibilityProvider());

      var tempPath = Path.GetFullPath(Path.Combine(".temp", $"NuGetResolver-{Guid.NewGuid():N}"));
      using var deleteTempDir = new DeleteDirectoryDisposable(tempPath);

      var tempRuntimeDir = DirectoryUtility.Create(Path.Combine(tempPath, "Runtime"), true);
      var tempEditorDir = DirectoryUtility.Create(Path.Combine(tempPath, "Editor"), true);

      for (var i = 0; i < packagesToInstall.Count; i++) {
        var packageToInstall = packagesToInstall[i];

        progressReport.Info = $"Install packages ({i + 1}/{packagesToInstall.Count}): {packageToInstall.Id}";
        progressReport.Progress = progressSegment.Evaluate((float)i / packagesToInstall.Count);
        progress?.Report(progressReport);

        if (resolveConfig.IsIgnored(packageToInstall.Id)) {
          logger.LogInformation($"Ignore {packageToInstall.Id}");
          continue;
        }

        PackageReaderBase packageReader;
        var installedPath = packagePathResolver.GetInstalledPath(packageToInstall);
        if (installedPath == null) {
          var downloadResource =
            await packageToInstall.Source.GetResourceAsync<DownloadResource>(cancellationToken);
          var downloadResult = await downloadResource.GetDownloadResourceResultAsync(
            packageToInstall,
            new PackageDownloadContext(cacheContext),
            SettingsUtility.GetGlobalPackagesFolder(settings),
            logger, cancellationToken);

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

        var isDevelopmentDependency = references.IsDevelopmentDependency(packageIdentity.Id);
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

      var packageFilePath = Path.Combine(packageDir.FullName, "package.json");
      File.WriteAllText(packageFilePath, packageFileContent, new UTF8Encoding(false));

      var runtimeDir = DirectoryUtility.Create(Path.Combine(projectPath, PackageRuntimePath), true);
      tempRuntimeDir.CopyTo(runtimeDir.FullName, true);

      var editorDir = DirectoryUtility.Create(Path.Combine(projectPath, PackageEditorPath), true);
      tempEditorDir.CopyTo(editorDir.FullName, true);

      assetsLock.Dispose();
      AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
    }


    [MenuItem("Assets/NuGet Resolver/Resolve")]
    public static async void Resolve() {
      const string title = "Resolve NuGet packages";
      var logger = UnityNuGetLogger.Instance;

      using var cts = new CancellationTokenSource();
      var progressDialog = new ProgressDialog(
        title,
        new ProgressReport { Progress = 0, Info = "Prepare..." },
        cts);

      var progress = new Progress<ProgressReport>(progressDialog.Update);

      try {
        await ResolveAsync(progress, logger, cts.Token);
      } catch (Exception ex) {
        logger.LogError(ex.ToString());

        progressDialog.Dispose();
        EditorUtility.DisplayDialog(title, $"Error: {ex.Message}", "OK");
        throw;
      }

      progressDialog.Dispose();
      EditorUtility.DisplayDialog(title, "Successfully completed!", "OK");
    }
  }
}
