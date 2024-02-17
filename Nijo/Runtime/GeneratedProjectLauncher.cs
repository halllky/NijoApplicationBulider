using Microsoft.Extensions.Logging;
using Nijo.Util.DotnetEx;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Nijo.Runtime {
    public class GeneratedProjectLauncher : IDisposable {
        internal GeneratedProjectLauncher(GeneratedProject project, ILogger logger) {
            _project = project;
            _logger = logger;
        }

        private readonly GeneratedProject _project;
        private readonly ILogger _logger;
        private readonly object _lock = new object();

        private E_State _state = E_State.Initialized;

        private Process? _dotnetRun;
        private bool _dotnetReady;

        private Process? _npmRun;
        private bool _npmReady;

        public event EventHandler? OnReady;

        public void Launch() {
            lock (_lock) {
                if (_state != E_State.Initialized)
                    throw new InvalidOperationException("デバッグは既に開始されています。");

                _npmRun = new Process();
                _npmRun.StartInfo.WorkingDirectory = _project.WebClientProjectRoot;
                _npmRun.StartInfo.FileName = "powershell";
                _npmRun.StartInfo.Arguments = "/c \"npm run dev\"";
                _npmRun.StartInfo.RedirectStandardOutput = true;
                _npmRun.StartInfo.RedirectStandardError = true;
                _npmRun.StartInfo.StandardOutputEncoding = Encoding.UTF8;
                _npmRun.StartInfo.StandardErrorEncoding = Encoding.UTF8;
                _npmRun.OutputDataReceived += OnNpmStdOut;
                _npmRun.ErrorDataReceived += OnNpmStdErr;

                _npmRun.Start();
                _npmRun.BeginOutputReadLine();
                _npmRun.BeginErrorReadLine();
                _logger.LogInformation("npm run   : Started. PID {PID}", _npmRun.Id);

                _dotnetRun = new Process();
                _dotnetRun.StartInfo.WorkingDirectory = _project.WebApiProjectRoot;
                _dotnetRun.StartInfo.FileName = "dotnet";
                _dotnetRun.StartInfo.Arguments = "run --launch-profile https";
                _dotnetRun.StartInfo.RedirectStandardOutput = true;
                _dotnetRun.StartInfo.RedirectStandardError = true;
                _dotnetRun.StartInfo.StandardOutputEncoding = Console.OutputEncoding;
                _dotnetRun.StartInfo.StandardErrorEncoding = Console.OutputEncoding;
                _dotnetRun.OutputDataReceived += OnDotnetStdOut;
                _dotnetRun.ErrorDataReceived += OnDotnetStdErr;

                _dotnetRun.Start();
                _dotnetRun.BeginOutputReadLine();
                _dotnetRun.BeginErrorReadLine();
                _logger.LogInformation("dotnet run: Started. PID {PID}", _dotnetRun.Id);

                _state = E_State.Launched;
            }
        }

        private void OnDotnetStdOut(object sender, DataReceivedEventArgs e) {
            // dotnet run の準備完了時の文字列「Now listening on:」
            if (!_dotnetReady && e.Data?.Contains("Now listening on:") == true) {
                _logger.LogInformation("dotnet run: Ready. ({Data})", e.Data?.Trim());
                _dotnetReady = true;
                OnReadyChanged();

            } else if (e.Data != null) {
                _logger.LogTrace("dotnet run: {Data}", e.Data);
            }
        }
        private void OnDotnetStdErr(object sender, DataReceivedEventArgs e) {
            if (e.Data != null) _logger.LogError("dotnet run: {Data}", e.Data);
        }

        private void OnNpmStdOut(object sender, DataReceivedEventArgs e) {
            // viteの準備完了時のログ「➜  Local:」
            if (!_npmReady && e.Data?.Contains("➜") == true) {
                _logger.LogInformation("npm run   : Ready. ({Data})", e.Data?.Trim());
                _npmReady = true;
                OnReadyChanged();

            } else if (e.Data != null) {
                _logger.LogTrace("npm run   : {Data}", e.Data);
            }
        }
        private void OnNpmStdErr(object sender, DataReceivedEventArgs e) {
            if (e.Data != null) _logger.LogError("npm run   : {Data}", e.Data);
        }

        private void OnReadyChanged() {
            lock (_lock) {
                if (!_dotnetReady) return;
                if (!_npmReady) return;
                if ((int)_state >= (int)E_State.Ready) return;

                _state = E_State.Ready;
                OnReady?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Terminate() {
            lock (_lock) {
                if (_state == E_State.Stopped) return;

                _logger.LogInformation("Now terminating debug process ...");

                if (_npmRun != null) _logger.LogInformation("npm run   : {msg}", _npmRun.EnsureKill());
                if (_dotnetRun != null) _logger.LogInformation("dotnet run: {msg}", _dotnetRun.EnsureKill());
                _state = E_State.Stopped;
                _logger.LogInformation("Prosess is terminated.");
            }
        }

        public void Dispose() {
            Terminate();
        }

        private enum E_State {
            Initialized = 0,
            Launched = 1,
            Ready = 2,
            Stopped = 3,
        }
    }
}
