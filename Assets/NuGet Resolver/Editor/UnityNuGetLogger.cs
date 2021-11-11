using System;
using System.Threading.Tasks;
using NuGet.Common;
using UnityEngine;
using ILogger = NuGet.Common.ILogger;

namespace NuGetResolver.Editor {
  internal sealed class UnityNuGetLogger : LoggerBase {
    private const string Prefix = "<color=lightblue><b>NuGet\u003E</b></color> ";

    public static readonly ILogger Instance = new UnityNuGetLogger();


    public UnityNuGetLogger(LogLevel verbosityLevel = LogLevel.Debug) : base(verbosityLevel) {
    }

    private static string FormatMessage(ILogMessage message) {
      return Prefix + message.Message;
    }

    public override void Log(ILogMessage message) {
      var data = FormatMessage(message);
      switch (message.Level) {
        case LogLevel.Debug:
        case LogLevel.Verbose:
        case LogLevel.Information:
        case LogLevel.Minimal:
          Debug.Log(data);
          break;
        case LogLevel.Warning:
          Debug.LogWarning(data);
          break;
        case LogLevel.Error:
          Debug.LogError(data);
          break;
        default:
          throw new ArgumentOutOfRangeException();
      }
    }

    public override Task LogAsync(ILogMessage message) {
      return Task.Factory.StartNew(() => Log(message));
    }
  }
}
