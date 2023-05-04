using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HalApplicationBuilder.Core;
using HalApplicationBuilder.DotnetEx;

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
            var verbose = new Option<bool>("--verbose");

            var create = new Command(name: "create", description: "新しいHalApplicationBuilderプロジェクトを作成します。") { applicationName, verbose };
            var debug = new Command(name: "debug", description: "プロジェクトのデバッグを開始します。") { path, verbose };

            create.SetHandler((applicationName, verbose) => {
                GeneratedProject
                    .Create(applicationName, verbose, cancellationTokenSource.Token, Console.Out);
            }, applicationName, verbose);

            debug.SetHandler((path, verbose) => {
                GeneratedProject
                    .Open(path)
                    .StartDebugging(verbose, cancellationTokenSource.Token, Console.Out);
            }, path, verbose);

            var rootCommand = new RootCommand("HalApplicationBuilder");
            rootCommand.AddCommand(create);
            rootCommand.AddCommand(debug);

            var parser = new CommandLineBuilder(rootCommand)
                .UseDefaults()
                .UseExceptionHandler((ex, _) => {
                    cancellationTokenSource.Cancel();
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Error.WriteLine(ex.ToString());
                    Console.ResetColor();
                })
                .Build();
            return await parser.InvokeAsync(args);
        }
    }
}
