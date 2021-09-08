using System;
using System.IO;
using System.Text;
using System.Xml;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGetResolver.Editor {
  internal sealed class ResolveConfigReader {
    private static PackageReference ReadPackage(XmlNode node) {
      var attributes = node.Attributes;
      if (attributes == null) {
        throw new ArgumentException();
      }

      var packageId = attributes.GetNamedItem("id").Value;

      NuGetVersion version = null;
      var versionNode = attributes.GetNamedItem("version");
      if (versionNode != null) {
        version = NuGetVersion.Parse(versionNode.Value);
      }

      var allowedVersions = VersionRange.AllStable;
      var allowedVersionsNode = attributes.GetNamedItem("allowedVersions");
      if (allowedVersionsNode != null) {
        allowedVersions = VersionRange.Parse(allowedVersionsNode.Value);
      }

      NuGetFramework targetFramework = null;
      var targetFrameworkNode = attributes.GetNamedItem("targetFramework");
      if (targetFrameworkNode != null) {
        targetFramework = NuGetFramework.ParseFolder(targetFrameworkNode.Value);
      }

      var developmentDependency = false;
      var developmentDependencyNode = attributes.GetNamedItem("developmentDependency");
      if (developmentDependencyNode != null) {
        developmentDependency = bool.Parse(developmentDependencyNode.Value);
      }

      return new PackageReference(
        new PackageIdentity(packageId, version),
        targetFramework,
        true,
        developmentDependency,
        false,
        allowedVersions);
    }

    private static IgnoreEntry ReadIgnore(XmlNode node) {
      var attributes = node.Attributes;
      if (attributes == null) {
        throw new ArgumentException();
      }

      var pattern = attributes.GetNamedItem("id").Value;
      return new IgnoreEntry(pattern);
    }

    private static ResolveConfig Read(XmlNode node) {
      var config = new ResolveConfig();
      foreach (XmlNode childNode in node.ChildNodes) {
        switch (childNode.Name) {
          case "package":
            config.Packages.Add(ReadPackage(childNode));
            break;
          case "ignore":
            config.Ignores.Add(ReadIgnore(childNode));
            break;
        }
      }

      return config;
    }

    private static ResolveConfig Read(XmlReader reader) {
      var doc = new XmlDocument();
      doc.Load(reader);

      var rootNode = doc.DocumentElement;
      if (rootNode == null || rootNode.Name != "packages") {
        throw new ArgumentException();
      }

      return Read(rootNode);
    }

    public ResolveConfig Read(TextReader reader) {
      using var xmlReader = new XmlTextReader(reader);
      return Read(xmlReader);
    }

    public ResolveConfig Read(Stream stream) {
      using var reader = new StreamReader(stream, Encoding.UTF8);
      return Read(reader);
    }

    public ResolveConfig Read(string fileName) {
      using var stream = File.OpenRead(fileName);
      return Read(stream);
    }
  }
}