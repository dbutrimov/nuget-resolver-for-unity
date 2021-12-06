# nuget-resolver-for-unity

Resolve NuGet packages in Unity projects


## How to use?

### Add to project

Add NuGet Resolver to your project via Unity Package Manager (UPM)

```
https://github.com/dbutrimov/nuget-resolver-for-unity.git?path=Packages/com.whitesharx.nugetresolver
```

### Configure NuGet

Place `NuGet.config` file into root directory of the project:
```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
</configuration>
```

More details about `NuGet.config` available at link https://docs.microsoft.com/en-us/nuget/reference/nuget-config-file

### Configure packages

Place `*NuGetPackages.xml` file somewhere in the `Assets` directory of the project:
```xml
<?xml version="1.0" encoding="utf-8"?>
<packages>
  <!-- Ignore packages -->
  <ignore id="Microsoft.CSharp" />
  <ignore id="System.Diagnostics.*" />

  <!-- Development packages -->
  <package id="JWT" version="8.2.3" allowedVersions="[8.2.3,9.0)" developmentDependency="true" />

  <!-- Runtime packages -->
  <package id="Semver" version="2.0.6" allowedVersions="[2.0.6,3.0)" targetFramework="net452" />
</packages>
```

### Resolve packages

Just click `NuGet -> Resolve` and be happy! 😎


# License

GNU GENERAL PUBLIC LICENSE
