// Copyright (c) 2021 White Sharx (https://whitesharx.com) - All Rights Reserved.
// Unauthorized copying of this file, via any medium is strictly prohibited.
// Proprietary and confidential.

using System;
using System.IO;

namespace NuGetResolver.Editor {
  internal static class DirectoryUtility {
    public static DirectoryInfo Create(string path, bool overwrite = false) {
      if (Directory.Exists(path)) {
        if (!overwrite) {
          throw new InvalidOperationException("Directory already exists");
        }

        Directory.Delete(path, true);
      }

      return Directory.CreateDirectory(path);
    }

    public static DirectoryInfo GetOrCreate(string path) {
      return Directory.Exists(path) ? new DirectoryInfo(path) : Directory.CreateDirectory(path);
    }

    public static DirectoryInfo CopyTo(this DirectoryInfo source, string destPath, bool recursive = false) {
      var dest = Directory.Exists(destPath)
        ? new DirectoryInfo(destPath)
        : Directory.CreateDirectory(destPath);

      foreach (var fileInfo in source.EnumerateFiles()) {
        fileInfo.CopyTo(Path.Combine(dest.FullName, fileInfo.Name));
      }

      if (!recursive) {
        return dest;
      }

      foreach (var dirInfo in source.EnumerateDirectories()) {
        dirInfo.CopyTo(Path.Combine(dest.FullName, dirInfo.Name), true);
      }

      return dest;
    }
  }
}
