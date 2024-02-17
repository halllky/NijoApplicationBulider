using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nijo.IntegrationTest.Tests {
    partial class 観点 {
        [Test, CancelAfter(300000)]
        public void プロジェクト新規作成処理のテスト() {

            const string PROJECT_NAME = "プロジェクト新規作成処理のテスト";
            var projectDir = Path.Combine(TestContext.CurrentContext.WorkDirectory, PROJECT_NAME);

            // 作成先フォルダがある場合は削除する
            if (Directory.Exists(projectDir)) {
                // .gitフォルダ下のファイルは読み取り専用なのでアクセス権限を変更してから削除する
                var dirInfo = new DirectoryInfo(projectDir) { Attributes = FileAttributes.Normal };
                foreach (var file in dirInfo.GetFiles("*", SearchOption.AllDirectories)) {
                    file.Attributes = FileAttributes.Normal;
                }
                dirInfo.Delete(true);
            }

            // 作成先フォルダがないことを確認
            if (Directory.Exists(projectDir)) Assert.Fail("新規作成しようとしている位置にディレクトリが既に存在します。");

            var serviceProvider = Util.ConfigureServices();
            var logger = serviceProvider.GetRequiredService<ILogger>();

            //// nijo create コマンドを実行
            //var terminal = new Terminal(TestContext.CurrentContext.WorkDirectory, logger);
            //await terminal.Run(new[] {
            //    ".\\nijo.exe", "create", PROJECT_NAME
            //}, TestContext.CurrentContext.CancellationToken);

            // nijo create コマンドを実行（デバッグしたい場合）
            GeneratedProject.Create(
                projectDir,
                PROJECT_NAME,
                true,
                serviceProvider,
                TestContext.CurrentContext.CancellationToken,
                logger);

            var project = GeneratedProject.Open(
                projectDir,
                serviceProvider,
                logger);

            // とりあえずもっとも単純なパターンで書き換えてみる
            // (デフォルトでもバックグランドタスク用のテーブルが生成されているはずなので
            // 敢えて集約定義を書かなくても十分判定可能かもしれない)
            var test1xml = DataPattern.FromFileName(DataPattern.FILENAME_000).LoadXmlString();
            File.WriteAllText(project.SchemaXml.GetPath(), test1xml);

            // 何らかのDBアクセスする処理が正常終了するかを確認
            using var launcher = project.CreateLauncher();
            var exceptions = new List<Exception>();
            launcher.OnReady += (s, e) => {
                try {
                    using var webDriver = TestProject.CreateWebDriver();

                    // 設定画面にあるデバッグ用の「DB更新」というラベルのボタンを押してDB再作成。
                    // デフォルトでダミーデータ4個が一緒に作成されるオプションのためこのタイミングでデータも一緒に作られる
                    webDriver.FindElement(Util.ByInnerText("設定")).Click();
                    webDriver.FindElement(Util.ByInnerText("DB更新")).Click();

                    // DB作成が正常終了していればダミーデータ4個分のリンクがあるはず
                    webDriver.FindElement(Util.ByInnerText("集約A")).Click();
                    var count = webDriver.FindElements(Util.ByInnerText("詳細")).Count;
                    if (count != 4) {
                        exceptions.Add(new Exception($"画面中にある「詳細」の文字の数が4個でない: {count}個"));
                    }

                } catch (Exception ex) {
                    exceptions.Add(ex);
                } finally {
                    launcher.Terminate();
                }
            };
            launcher.OnError += (s, e) => {
                exceptions.Add(new Exception(e.ToString()));
                launcher.Terminate();
            };

            launcher.Launch();
            launcher.WaitForTerminate();

            if (exceptions.Count != 0) {
                var messages = exceptions.Select(ex => ex.ToString());
                Assert.Fail(string.Join(Environment.NewLine, messages));
            }
        }
    }
}
