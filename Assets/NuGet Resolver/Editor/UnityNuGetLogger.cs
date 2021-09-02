// Copyright (c) 2021 White Sharx (https://whitesharx.com) - All Rights Reserved.
// Unauthorized copying of this file, via any medium is strictly prohibited.
// Proprietary and confidential.

using System;
using System.Threading.Tasks;
using NuGet.Common;
using UnityEngine;
using ILogger = NuGet.Common.ILogger;

namespace NuGetResolver.Editor {
  internal sealed class UnityNuGetLogger : ILogger {
    private const string Prefix = "<color=lightblue><b>NuGet</b></color>: ";

    public static readonly ILogger Instance = new UnityNuGetLogger();

    public void LogDebug(string data) {
      Debug.Log(Prefix + data);
    }

    public void LogVerbose(string data) {
      LogDebug(data);
    }

    public void LogInformation(string data) {
      LogDebug(data);
    }

    public void LogMinimal(string data) {
      LogDebug(data);
    }

    public void LogWarning(string data) {
      Debug.LogWarning(Prefix + data);
    }

    public void LogError(string data) {
      Debug.LogError(Prefix + data);
    }

    public void LogInformationSummary(string data) {
      LogDebug(data);
    }

    public void Log(LogLevel level, string data) {
      switch (level) {
        case LogLevel.Debug:
          LogDebug(data);
          break;
        case LogLevel.Verbose:
          LogVerbose(data);
          break;
        case LogLevel.Information:
          LogInformation(data);
          break;
        case LogLevel.Minimal:
          LogMinimal(data);
          break;
        case LogLevel.Warning:
          LogWarning(data);
          break;
        case LogLevel.Error:
          LogError(data);
          break;
        default:
          throw new ArgumentOutOfRangeException(nameof(level), level, null);
      }
    }

    public void Log(ILogMessage message) {
      Log(message.Level, message.Message);
    }

    public Task LogAsync(LogLevel level, string data) {
      return Task.Factory.StartNew(() => Log(level, data));
    }

    public Task LogAsync(ILogMessage message) {
      return LogAsync(message.Level, message.Message);
    }
  }
}
