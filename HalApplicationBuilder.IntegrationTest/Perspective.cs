using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HalApplicationBuilder.IntegrationTest.Perspectives {
    [NonParallelizable]
    public partial class Perspective {

        #region 期待結果が定義されていない場合にテストの事前準備をスキップするための仕組み
        private static DelayedExecuter If(DataPattern pattern) {
            return new DelayedExecuter(pattern);
        }
        private class DelayedExecuter {
            public DelayedExecuter(DataPattern pattern) {
                _pattern = pattern;
            }
            private readonly DataPattern _pattern;
            private readonly Dictionary<E_DataPattern, Func<Task>> _describes = new();
            public DelayedExecuter When(E_DataPattern pattern, Func<Task> then) {
                _describes[pattern] = then;
                return this;
            }
            public async Task LaunchTest() {
                if (!_describes.TryGetValue(_pattern.AsEnum(), out var describe)) {
                    Assert.Warn("期待結果が定義されていません。");
                    return;
                }

                using var ct = new CancellationTokenSource();
                using var dotnetRun = SharedResource.Project.CreateServerRunningProcess(ct.Token, Console.Out);
                try {

                    // halapp.xmlの更新
                    File.WriteAllText(SharedResource.Project.GetAggregateSchemaPath(), _pattern.LoadXmlString());
                    SharedResource.Project.UpdateAutoGeneratedCode();

                    // DB（前のテストで作成されたDBを削除）
                    var migrationDir = Path.Combine(SharedResource.Project.ProjectRoot, "Migrations");
                    if (Directory.Exists(migrationDir)) {
                        foreach (var file in Directory.GetFiles(migrationDir)) {
                            File.Delete(file);
                        }
                    }
                    File.Delete(Path.Combine(SharedResource.Project.ProjectRoot, "bin", "Debug", "debug.sqlite3"));
                    File.Delete(Path.Combine(SharedResource.Project.ProjectRoot, "bin", "Debug", "debug.sqlite3-shm"));
                    File.Delete(Path.Combine(SharedResource.Project.ProjectRoot, "bin", "Debug", "debug.sqlite3-wal"));

                    await SharedResource.Project.BuildAsync();

                    // DB（このデータパターンの定義に従ったDBを作成）
                    try {
                        SharedResource.Project.EnsureCreateDatabase();
                    } catch (Exception ex) {
                        throw new Exception("DB作成失敗", ex);
                    }

                    await dotnetRun.Launch();
                    await describe();
                } finally {
                    ct.Cancel();
                }
            }
        }
        #endregion 期待結果が定義されていない場合にテストの事前準備をスキップするための仕組み
    }
}
