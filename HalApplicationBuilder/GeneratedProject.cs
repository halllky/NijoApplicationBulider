using HalApplicationBuilder.CodeRendering.ReactAndWebApi;
using HalApplicationBuilder.Core;
using HalApplicationBuilder.DotnetEx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace HalApplicationBuilder {
    public class GeneratedProject {

        private const string HALAPP_XML_NAME = "halapp.xml";
        private const string REACT_DIR = "ClientApp";
        internal const string REACT_PAGE_DIR = "pages";
        private const string HALAPP_DLL_COPY_TARGET = "halapp-resource";

        /// <summary>
        /// 新しいhalappプロジェクトを作成します。
        /// </summary>
        /// <param name="applicationName">アプリケーション名</param>
        /// <param name="verbose">ログの詳細出力を行うかどうか</param>
        /// <returns>作成されたプロジェクトを表すオブジェクト</returns>
        public static GeneratedProject Create(string? applicationName, bool verbose, CancellationToken cancellationToken, TextWriter? log = null) {

            if (string.IsNullOrWhiteSpace(applicationName))
                throw new InvalidOperationException($"Please specify name of new application. example 'halapp create my-new-app'");

            if (Path.GetInvalidFileNameChars().Any(applicationName.Contains))
                throw new InvalidOperationException($"'{applicationName}' contains invalid characters for a file name.");

            var projectRoot = Path.Combine(Directory.GetCurrentDirectory(), applicationName);
            if (Directory.Exists(projectRoot))
                throw new InvalidOperationException($"'{projectRoot}' is already exists.");

            var rootNamespace = applicationName.ToCSharpSafe();
            var config = new Config {
                ApplicationName = applicationName,
                DbContextName = "MyDbContext",
                DbContextNamespace = $"{rootNamespace}.EntityFramework",
                EntityFrameworkDirectoryRelativePath = "EntityFramework/__AutoGenerated",
                EntityNamespace = $"{rootNamespace}.EntityFramework.Entities",
                MvcControllerDirectoryRelativePath = "Controllers/__AutoGenerated",
                MvcControllerNamespace = $"{rootNamespace}.Controllers",
                MvcModelDirectoryRelativePath = "Models/__AutoGenerated",
                MvcModelNamespace = $"{rootNamespace}.Models",
                MvcViewDirectoryRelativePath = "Views/_AutoGenerated",
                OutProjectDir = ".",
            };

            var ramdomName = $"halapp.temp.{Path.GetRandomFileName()}";
            var tempDir = Path.Combine(Directory.GetCurrentDirectory(), ramdomName);

            try {
                Directory.CreateDirectory(tempDir);

                var cmd = new DotnetEx.Cmd {
                    WorkingDirectory = tempDir,
                    CancellationToken = cancellationToken,
                    Verbose = verbose,
                };

                // dotnet CLI でプロジェクトを新規作成
                log?.WriteLine($"プロジェクトを作成します。");
                cmd.Exec("dotnet", "new", "webapi", "--output", ".", "--name", config.ApplicationName);

                log?.WriteLine($"Microsoft.EntityFrameworkCore パッケージへの参照を追加します。");
                cmd.Exec("dotnet", "add", "package", "Microsoft.EntityFrameworkCore");

                log?.WriteLine($"Microsoft.EntityFrameworkCore.Proxies パッケージへの参照を追加します。");
                cmd.Exec("dotnet", "add", "package", "Microsoft.EntityFrameworkCore.Proxies");

                log?.WriteLine($"Microsoft.EntityFrameworkCore.Design パッケージへの参照を追加します。"); // migration add に必要
                cmd.Exec("dotnet", "add", "package", "Microsoft.EntityFrameworkCore.Design");

                log?.WriteLine($"Microsoft.EntityFrameworkCore.Sqlite パッケージへの参照を追加します。");
                cmd.Exec("dotnet", "add", "package", "Microsoft.EntityFrameworkCore.Sqlite");

                // halapp.dll への参照を加える。実行時にRuntimeContextを参照しているため
                log?.WriteLine($"halapp.dll を参照に追加します。");

                CopyHalapDllTo(tempDir);

                // csprojファイルを編集: csprojファイルを開く
                var csprojPath = Path.Combine(tempDir, $"{config.ApplicationName}.csproj");
                var projectOption = new Microsoft.Build.Definition.ProjectOptions {
                    // Referenceを追加するだけなので Microsoft.NET.Sdk.Web が無くてもエラーにならないようにしたい
                    LoadSettings = Microsoft.Build.Evaluation.ProjectLoadSettings.IgnoreMissingImports,
                };
                var csproj = Microsoft.Build.Evaluation.Project.FromFile(csprojPath, projectOption);
                var itemGroup = csproj.Xml.AddItemGroup();

                // csprojファイルを編集: halapp.dll への参照を追加する（dll参照は dotnet add でサポートされていないため）
                var reference = itemGroup.AddItem("Reference", include: "halapp");
                reference.AddMetadata("HintPath", Path.Combine(HALAPP_DLL_COPY_TARGET, "halapp.dll"));

                // csprojファイルを編集: ビルド時に halapp.dll が含まれるディレクトリがコピーされるようにする
                var none = itemGroup.AddItem("None", Path.Combine(HALAPP_DLL_COPY_TARGET, "**", "*.*"));
                none.AddMetadata("CopyToOutputDirectory", "Always");

                csproj.Save();

                // Program.cs書き換え
                log?.WriteLine($"HalappDefaultConfigure.cs ファイルを作成します。");
                using (var sw = new StreamWriter(Path.Combine(tempDir, "HalappDefaultConfigure.cs"), append: false, encoding: Encoding.UTF8)) {
                    sw.Write(new CodeRendering.DefaultRuntimeConfigTemplate(config).TransformText());
                }
                log?.WriteLine($"Program.cs ファイルを書き換えます。");
                var programCsPath = Path.Combine(tempDir, "Program.cs");
                var lines = File.ReadAllLines(programCsPath).ToList();
                var regex1 = new Regex(@"^.*[a-zA-Z]+ builder = .+;$");
                var position1 = lines.FindIndex(regex1.IsMatch);
                if (position1 == -1) throw new InvalidOperationException("Program.cs の中にIServiceCollectionを持つオブジェクトを初期化する行が見つかりません。");
                lines.InsertRange(position1 + 1, new[] {
                    $"",
                    $"/* HalApplicationBuilder によって自動生成されたコード ここから */",
                    $"var runtimeRootDir = System.IO.Directory.GetCurrentDirectory();",
                    $"HalApplicationBuilder.Runtime.HalAppDefaultConfigurer.Configure(builder.Services, runtimeRootDir);",
                    $"// HTMLのエンコーディングをUTF-8にする(日本語のHTMLエンコード防止)",
                    $"builder.Services.Configure<Microsoft.Extensions.WebEncoders.WebEncoderOptions>(options => {{",
                    $"    options.TextEncoderSettings = new System.Text.Encodings.Web.TextEncoderSettings(System.Text.Unicode.UnicodeRanges.All);",
                    $"}});",
                    $"// npm start で実行されるポートがASP.NETのそれと別なので",
                    $"builder.Services.AddCors(options => {{",
                    $"    options.AddDefaultPolicy(builder => {{",
                    $"        builder.AllowAnyOrigin()",
                    $"            .AllowAnyMethod()",
                    $"            .AllowAnyHeader();",
                    $"    }});",
                    $"}});",
                    $"/* HalApplicationBuilder によって自動生成されたコード ここまで */",
                    $"",
                });
                var regex2 = new Regex(@"^.*[a-zA-Z]+ app = .+;$");
                var position2 = lines.FindIndex(regex2.IsMatch);
                if (position2 == -1) throw new InvalidOperationException("Program.cs の中にappオブジェクトを初期化する行が見つかりません。");
                lines.InsertRange(position2 + 1, new[] {
                    $"",
                    $"/* HalApplicationBuilder によって自動生成されたコード ここから */",
                    $"// 前述AddCorsの設定をするならこちらも必要",
                    $"app.UseCors();",
                    $"/* HalApplicationBuilder によって自動生成されたコード ここまで */",
                    $"",
                });
                File.WriteAllLines(programCsPath, lines);

                // DbContext生成
                var dbContextFileName = $"{config.DbContextName}.cs";
                log?.WriteLine($"{dbContextFileName} ファイルを作成します。");
                var dbContextDir = Path.Combine(tempDir, "EntityFramework");
                Directory.CreateDirectory(dbContextDir);
                using (var sw = new StreamWriter(Path.Combine(dbContextDir, dbContextFileName), append: false, encoding: Encoding.UTF8)) {
                    sw.Write(new CodeRendering.EFCore.DbContextTemplate(config).TransformText());
                }

                // Migrationが1件もないと最初のデバッグ時におかしな挙動になるので、空のMigrationを作成しておく
                cmd.Exec("dotnet", "ef", "migrations", "add", "init");
                cmd.Exec("dotnet", "ef", "database", "update");

                // React.js アプリケーションを作成
                log?.WriteLine($"React.jsアプリケーションを作成します。");

                // プロジェクトテンプレートのコピー
                var projectTemplateDir = Path.Combine(Path.GetDirectoryName(
                    Assembly.GetExecutingAssembly().Location)!,
                    "CodeRendering",
                    "ReactAndWebApi",
                    "project-template");
                var reactDir = Path.Combine(cmd.WorkingDirectory, REACT_DIR);
                DotnetEx.IO.CopyDirectory(projectTemplateDir, reactDir);

                var componentsIn = Path.Combine(projectTemplateDir, "src", "__AutoGenerated", "components");
                var componentsOut = Path.Combine(reactDir, "src", "__AutoGenerated", "components");
                DotnetEx.IO.CopyDirectory(componentsIn, componentsOut);

                var hooksIn = Path.Combine(projectTemplateDir, "src", "__AutoGenerated", "hooks");
                var hooksOut = Path.Combine(reactDir, "src", "__AutoGenerated", "hooks");
                DotnetEx.IO.CopyDirectory(hooksIn, hooksOut);

                var npmProcess = new DotnetEx.Cmd {
                    WorkingDirectory = reactDir,
                    CancellationToken = cmd.CancellationToken,
                    Verbose = verbose,
                };
                npmProcess.Exec("npm", "ci");

                // halapp.xmlの作成
                var xmlPath = Path.Combine(tempDir, HALAPP_XML_NAME);
                var xmlContent = new XDocument(config.ToXmlWithRoot());
                using (var sw = new StreamWriter(xmlPath, append: false, encoding: new UTF8Encoding(false))) {
                    sw.WriteLine(xmlContent.ToString());
                }

                // ここまでの処理がすべて成功したら一時ディレクトリを本来のディレクトリ名に変更
                if (Directory.Exists(projectRoot)) throw new InvalidOperationException($"プロジェクトディレクトリを {projectRoot} に移動できません。");
                Directory.Move(tempDir, projectRoot);

                var generatedProject = new GeneratedProject(projectRoot);
                generatedProject.UpdateAutoGeneratedCode(log);

                log?.WriteLine("プロジェクト作成完了");

                return generatedProject;

            } finally {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        /// <summary>
        /// 既存のhalappプロジェクトを開きます。
        /// </summary>
        /// <param name="path">プロジェクトルートディレクトリの絶対パス</param>
        /// <returns>作成されたプロジェクトを表すオブジェクト</returns>
        public static GeneratedProject Open(string? path) {
            if (string.IsNullOrWhiteSpace(path))
                return new GeneratedProject(Directory.GetCurrentDirectory());
            else if (Directory.Exists(path))
                return new GeneratedProject(path);
            else
                return new GeneratedProject(Path.Combine(Directory.GetCurrentDirectory(), path));
        }

        private GeneratedProject(string projetctRoot) {
            if (string.IsNullOrWhiteSpace(projetctRoot))
                throw new ArgumentException($"'{nameof(projetctRoot)}' is required.");

            ProjectRoot = projetctRoot;
        }

        private string ProjectRoot { get; }
        private AppSchema ReadSchema() {
            var xmlFullPath = Path.Combine(ProjectRoot, HALAPP_XML_NAME);
            using var stream = DotnetEx.IO.OpenFileWithRetry(xmlFullPath);
            using var reader = new StreamReader(stream);
            var xmlContent = reader.ReadToEnd();
            var xDocument = XDocument.Parse(xmlContent);
            var appSchema = AppSchema.FromXml(xDocument);
            return appSchema;
        }
        private Config ReadConfig() {
            var xmlFullPath = Path.Combine(ProjectRoot, HALAPP_XML_NAME);
            using var stream = DotnetEx.IO.OpenFileWithRetry(xmlFullPath);
            using var reader = new StreamReader(stream);
            var xmlContent = reader.ReadToEnd();
            var xDocument = XDocument.Parse(xmlContent);
            var config = Core.Config.FromXml(xDocument);
            return config;
        }

        /// <summary>
        /// このディレクトリがhalappのものとして妥当なものかどうかを検査します。
        /// </summary>
        /// <param name="log">エラー内容出力</param>
        /// <returns></returns>
        public bool IsValidDirectory(TextWriter? log = null) {
            var errors = new List<string>();

            if (Path.GetInvalidPathChars().Any(ProjectRoot.Contains))
                errors.Add($"Invalid path format: '{ProjectRoot}'");

            if (!Directory.Exists(ProjectRoot))
                errors.Add($"Directory '{ProjectRoot}' is not exist.");

            var halappXml = Path.Combine(ProjectRoot, HALAPP_XML_NAME);
            if (!File.Exists(halappXml))
                errors.Add($"'{halappXml}' is not found.");

            if (log != null) {
                foreach (var error in errors) log.WriteLine(error);
            }
            return errors.Count == 0;
        }

        /// <summary>
        /// コードの自動生成を行います。
        /// </summary>
        /// <param name="log">ログ出力先</param>
        public GeneratedProject UpdateAutoGeneratedCode(TextWriter? log = null) {

            if (!IsValidDirectory(log)) return this;

            log?.WriteLine($"コード自動生成開始");

            var config = ReadConfig();
            var rootAggregates = ReadSchema().GetRootAggregates(config).ToArray();
            var allAggregates = rootAggregates
                .SelectMany(a => a.GetDescendantsAndSelf())
                .ToArray();

            log?.WriteLine("コード自動生成: スキーマ定義");
            using (var sw = new StreamWriter(Path.Combine(ProjectRoot, "halapp.json"), append: false, encoding: Encoding.UTF8)) {
                var schema = new Serialized.AppSchemaJson {
                    Config = config.ToJson(onlyRuntimeConfig: true),
                    Aggregates = rootAggregates.Select(a => a.ToJson()).ToArray(),
                };
                sw.Write(System.Text.Json.JsonSerializer.Serialize(schema, new System.Text.Json.JsonSerializerOptions {
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All), // 日本語用
                    WriteIndented = true,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull, // nullのフィールドをシリアライズしない
                }));
            }

            var modelDir = Path.Combine(ProjectRoot, config.MvcModelDirectoryRelativePath);
            if (Directory.Exists(modelDir)) Directory.Delete(modelDir, recursive: true);
            Directory.CreateDirectory(modelDir);

            log?.WriteLine("コード自動生成: UI Model");
            using (var sw = new StreamWriter(Path.Combine(modelDir, "Models.cs"), append: false, encoding: Encoding.UTF8)) {
                sw.Write(new CodeRendering.UIModelsTemplate(config, allAggregates).TransformText());
            }

            var efSourceDir = Path.Combine(ProjectRoot, config.EntityFrameworkDirectoryRelativePath);
            if (Directory.Exists(efSourceDir)) Directory.Delete(efSourceDir, recursive: true);
            Directory.CreateDirectory(efSourceDir);

            log?.WriteLine("コード自動生成: Entity定義");
            using (var sw = new StreamWriter(Path.Combine(efSourceDir, "Entities.cs"), append: false, encoding: Encoding.UTF8)) {
                sw.Write(new CodeRendering.EFCore.EntityClassTemplate(config, allAggregates).TransformText());
            }
            log?.WriteLine("コード自動生成: DbSet");
            using (var sw = new StreamWriter(Path.Combine(efSourceDir, "DbSet.cs"), append: false, encoding: Encoding.UTF8)) {
                sw.Write(new CodeRendering.EFCore.DbSetTemplate(config, allAggregates).TransformText());
            }
            log?.WriteLine("コード自動生成: OnModelCreating");
            using (var sw = new StreamWriter(Path.Combine(efSourceDir, "OnModelCreating.cs"), append: false, encoding: Encoding.UTF8)) {
                sw.Write(new CodeRendering.EFCore.OnModelCreatingTemplate(config, allAggregates).TransformText());
            }
            log?.WriteLine("コード自動生成: Search");
            using (var sw = new StreamWriter(Path.Combine(efSourceDir, "Search.cs"), append: false, encoding: Encoding.UTF8)) {
                sw.Write(new CodeRendering.EFCore.SearchMethodTemplate(config, rootAggregates).TransformText());
            }
            log?.WriteLine("コード自動生成: AutoCompleteSource");
            using (var sw = new StreamWriter(Path.Combine(efSourceDir, "AutoCompleteSource.cs"), append: false, encoding: Encoding.UTF8)) {
                sw.Write(new CodeRendering.EFCore.AutoCompleteSourceTemplate(config, allAggregates).TransformText());
            }

            // Web API
            using (var sw = new StreamWriter(Path.Combine(ProjectRoot, "Controllers", "__AutoGenerated.cs"), append: false, encoding: Encoding.UTF8)) {
                var template = new WebApiControllerTemplate(config, rootAggregates);
                sw.Write(template.TransformText());
            }
            using (var sw = new StreamWriter(Path.Combine(ProjectRoot, "Controllers", "Debugger.cs"), append: false, encoding: Encoding.UTF8)) {
                var template = new WebApiDebuggerTemplate(config);
                sw.Write(template.TransformText());
            }

            // React.js
            var tsDir = Path.Combine(ProjectRoot, REACT_DIR, "src", "__AutoGenerated");
            if (!Directory.Exists(tsDir)) Directory.CreateDirectory(tsDir);

            var generateStartTime = DateTime.Now;

            // 集約定義
            var utf8withoutBOM = new UTF8Encoding(false);
            log?.WriteLine("コード自動生成: 集約のTypeScript型定義");
            using (var sw = new StreamWriter(Path.Combine(tsDir, ReactTypeDefTemplate.FILE_NAME), append: false, encoding: utf8withoutBOM)) {
                var template = new ReactTypeDefTemplate();
                sw.Write(template.TransformText());
            }
            // コンポーネント
            log?.WriteLine("コード自動生成: 集約のReactコンポーネント");
            var reactPageDir = Path.Combine(tsDir, REACT_PAGE_DIR);
            if (!Directory.Exists(reactPageDir)) Directory.CreateDirectory(reactPageDir);
            var updatetdReactFiles = new HashSet<string>();
            foreach (var rootAggregate in rootAggregates) {
                var template = new ReactComponentTemplate(rootAggregate);
                var filepath = Path.Combine(reactPageDir, template.FileName);
                using var sw = new StreamWriter(filepath, append: false, encoding: utf8withoutBOM);
                sw.Write(template.TransformText());

                updatetdReactFiles.Add(filepath);
            }

            var deleteFiles = Directory
                .GetFiles(reactPageDir)
                .Where(file => !updatetdReactFiles.Contains(file));
            foreach (var filepath in deleteFiles) {
                File.Delete(filepath);
            }

            log?.WriteLine("コード自動生成: index.ts等");
            // menu.tsx
            using (var sw = new StreamWriter(Path.Combine(tsDir, menuItems.FILE_NAME), append: false, encoding: utf8withoutBOM)) {
                var template = new menuItems(rootAggregates);
                sw.Write(template.TransformText());
            }
            // index.ts
            using (var sw = new StreamWriter(Path.Combine(tsDir, index.FILE_NAME), append: false, encoding: utf8withoutBOM)) {
                var template = new index(rootAggregates);
                sw.Write(template.TransformText());
            }

            log?.WriteLine("コード自動生成終了");

            return this;
        }

        /// <summary>
        /// デバッグを開始します。
        /// </summary>
        public void StartDebugging(bool verbose, CancellationToken cancellationToken, TextWriter? log = null) {

            if (!IsValidDirectory(log)) return;

            var config = ReadConfig();

            // migration用設定
            var migrationList = new DotnetEx.Cmd {
                WorkingDirectory = ProjectRoot,
                CancellationToken = cancellationToken,
                Verbose = verbose,
            };
            var previousMigrationId = migrationList
                .ReadOutputs("dotnet", "ef", "migrations", "list")
                .LastOrDefault()
                ?? string.Empty;
            var nextMigrationId = Guid
                .NewGuid()
                .ToString()
                .Replace("-", "");

            // 以下の2種類のキャンセルがあるので統合する
            // - ユーザーの操作による halapp debug 全体のキャンセル
            // - 集約定義ファイル更新によるビルドのキャンセル
            CancellationTokenSource? rebuildCancellation = null;
            CancellationTokenSource? linkedTokenSource = null;

            // バックグラウンド処理の宣言
            DotnetEx.Cmd.Background? dotnetRun = null;
            DotnetEx.Cmd.Background? npmStart = null;

            // ファイル変更監視用オブジェクト
            FileSystemWatcher? watcher = null;

            try {
                var changed = false;

                // halapp debug 中ずっと同じインスタンスが使われるものを初期化する
                watcher = new FileSystemWatcher(ProjectRoot);
                watcher.Filter = HALAPP_XML_NAME;
                watcher.NotifyFilter = NotifyFilters.LastWrite;
                watcher.Changed += (_, _) => {
                    changed = true;
                    rebuildCancellation?.Cancel();
                };

                npmStart = new DotnetEx.Cmd.Background {
                    WorkingDirectory = Path.Combine(ProjectRoot, REACT_DIR),
                    Filename = "npm",
                    Args = new[] { "start" },
                    CancellationToken = cancellationToken,
                    Verbose = verbose,
                };

                // 監視開始
                watcher.EnableRaisingEvents = true;
                npmStart.Restart();

                // リビルドの度に実行される処理
                while (true) {
                    dotnetRun?.Dispose();
                    rebuildCancellation?.Dispose();
                    linkedTokenSource?.Dispose();

                    rebuildCancellation = new CancellationTokenSource();
                    linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                        cancellationToken,
                        rebuildCancellation.Token);

                    try {
                        // ソースファイル再生成 & npm watch による自動更新
                        UpdateAutoGeneratedCode(log);

                        linkedTokenSource.Token.ThrowIfCancellationRequested();

                        // DB定義の更新
                        var migration = new DotnetEx.Cmd {
                            WorkingDirectory = ProjectRoot,
                            CancellationToken = linkedTokenSource.Token,
                            Verbose = verbose,
                        };
                        migration.Exec("dotnet", "build");

                        // 集約定義を書き換えるたびにマイグレーションが積み重なっていってしまうため、
                        // 1回のhalapp debugで作成されるマイグレーションは1つまでとする
                        var latestMigrationId = migrationList
                            .ReadOutputs("dotnet", "ef", "migrations", "list")
                            .LastOrDefault()
                            ?? string.Empty;
                        if (latestMigrationId != previousMigrationId) {
                            Console.WriteLine($"DB定義を右記地点に巻き戻します: {previousMigrationId}");
                            migration.Exec("dotnet", "ef", "database", "update", previousMigrationId);
                            migration.Exec("dotnet", "ef", "migrations", "remove");
                        }

                        migration.Exec("dotnet", "ef", "migrations", "add", nextMigrationId);
                        migration.Exec("dotnet", "ef", "database", "update", nextMigrationId);

                        linkedTokenSource.Token.ThrowIfCancellationRequested();

                        // ビルドが完了したので dotnet run を再開
                        dotnetRun = new DotnetEx.Cmd.Background {
                            WorkingDirectory = ProjectRoot,
                            Filename = "dotnet",
                            Args = new[] { "run", "--launch-profile", "https", "--no-build" },
                            CancellationToken = linkedTokenSource.Token,
                            Verbose = verbose,
                        };
                        dotnetRun.Restart();

                    } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                        throw; // デバッグ自体を中断

                    } catch (OperationCanceledException) when (rebuildCancellation.IsCancellationRequested) {
                        continue; // 実行中のビルドを中断してもう一度最初から

                    } catch (Exception ex) {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Error.WriteLine(ex.ToString());
                        Console.ResetColor();
                    }

                    changed = false;

                    // 次の更新まで待機
                    while (changed == false) {
                        Thread.Sleep(100);
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                }

            } catch (OperationCanceledException) {
                Console.WriteLine("デバッグを中断します。");

            } finally {
                rebuildCancellation?.Dispose();
                linkedTokenSource?.Dispose();
                dotnetRun?.Dispose();
                npmStart?.Dispose();
                watcher?.Dispose();
            }
        }

        /// <summary>
        /// halapp.dllとその依存先をプロジェクトディレクトリにコピー
        /// </summary>
        public GeneratedProject CopyHalapDll() {
            CopyHalapDllTo(ProjectRoot);
            return this;
        }
        /// <summary>
        /// halapp.dllとその依存先をプロジェクトディレクトリにコピー
        /// </summary>
        /// <param name="projectRoot">コピー先</param>
        private static void CopyHalapDllTo(string projectRoot) {
            var halappDirCopySource = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
            var halappDirCopyDist = Path.Combine(projectRoot, HALAPP_DLL_COPY_TARGET);
            DotnetEx.IO.CopyDirectory(halappDirCopySource, halappDirCopyDist);
        }
    }
}
