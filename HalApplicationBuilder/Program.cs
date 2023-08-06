using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HalApplicationBuilder.Core;
using HalApplicationBuilder.DotnetEx;

[assembly: InternalsVisibleTo("HalApplicationBuilder.Test")]
[assembly: InternalsVisibleTo("HalApplicationBuilder.IntegrationTest")]

namespace HalApplicationBuilder {
    public class Program {

        static async Task<int> Main(string[] args) {

            var cancellationTokenSource = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, e) => {

                cancellationTokenSource.Cancel();

                // キャンセル時のリソース解放を適切に行うために既定の動作（アプリケーション終了）を殺す
                e.Cancel = true;
            };

            var path = new Argument<string?>(() => null);
            var applicationName = new Argument<string?>();
            var verbose = new Option<bool>("--verbose", description: "詳細なログを出力します。");
            var keepTempIferror = new Option<bool>("--keep-temp-if-error", description: "エラー発生時、原因調査ができるようにするため一時フォルダを削除せず残します。");

            var create = new Command(name: "create", description: "新しいHalApplicationBuilderプロジェクトを作成します。") { applicationName, verbose, keepTempIferror };
            var debug = new Command(name: "debug", description: "プロジェクトのデバッグを開始します。") { path, verbose };
            var fix = new Command(name: "fix", description: "コード自動生成処理をかけなおします。") { path, verbose };

            create.SetHandler((applicationName, verbose, keepTempIferror) => {
                if (!CheckIfToolIsAvailable(cancellationTokenSource.Token, "dotnet", "npm", "git")) return;
                if (string.IsNullOrEmpty(applicationName)) throw new ArgumentException($"Application name is required.");
                var projectRootDir = Path.Combine(Directory.GetCurrentDirectory(), applicationName);
                HalappProject.Create(projectRootDir, applicationName, keepTempIferror, cancellationTokenSource.Token, Console.Out, verbose);
            }, applicationName, verbose, keepTempIferror);

            debug.SetHandler(async (path, verbose) => {
                if (!CheckIfToolIsAvailable(cancellationTokenSource.Token, "dotnet", "npm")) return;
                var project = HalappProject.Open(path, Console.Out, verbose);
                project.AddReferenceToHalappDll();
                await project.StartDebugging(cancellationTokenSource.Token);
            }, path, verbose);

            fix.SetHandler((path, verbose) => {
                if (!CheckIfToolIsAvailable(cancellationTokenSource.Token, "dotnet", "npm")) return;
                HalappProject
                    .Open(path, Console.Out, verbose)
                    .UpdateAutoGeneratedCode()
                    //.AddNugetPackages()
                    //.AddReferenceToHalappDll()
                    //.EnsureCreateRuntimeSettingFile()
                    //.EnsureCreateDatabase()
                    //.InstallNodeModules()
                    ;
            }, path, verbose);

            var rootCommand = new RootCommand("HalApplicationBuilder");
            rootCommand.AddCommand(create);
            rootCommand.AddCommand(debug);
            rootCommand.AddCommand(fix);

            var parser = new CommandLineBuilder(rootCommand)
                .UseDefaults()
                .UseExceptionHandler((ex, _) => {
                    if (ex is OperationCanceledException) {
                        Console.Error.WriteLine("キャンセルされました。");
                    } else {
                        cancellationTokenSource.Cancel();
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Error.WriteLine(ex.ToString());
                        Console.ResetColor();
                    }
                })
                .Build();
            return await parser.InvokeAsync(args);
        }


        /// <summary>
        /// 外部ツールが使用可能かどうかを検査する（'--version' コマンドを実行することで確認）
        /// </summary>
        private static bool CheckIfToolIsAvailable(CancellationToken cancellationToken, params string[] names) {
            var ok = true;
            foreach (var name in names) {
                try {
                    var cmd = new DotnetEx.Cmd {
                        WorkingDirectory = ".",
                        CancellationToken = cancellationToken,
                        Verbose = false,
                    };
                    cmd.Exec(name, "--version");
                } catch (OperationCanceledException) {
                    throw;
                } catch {
                    Console.Error.WriteLine($"Command line tool '{name}' is not available. Please install it from the official website of '{name}'.");
                    ok = false;
                }
            }
            return ok;
        }
    }
}
