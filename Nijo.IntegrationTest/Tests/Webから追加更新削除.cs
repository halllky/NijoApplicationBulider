using OpenQA.Selenium;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nijo.IntegrationTest.Tests {
    partial class 観点 {

        [UseDataPatterns]
        public async Task Webから追加更新削除(DataPattern pattern) {

            if (pattern.Name == DataPattern.FILENAME_001) {
                await Webから追加更新削除_Refのみ(pattern);

            } else if (pattern.Name == DataPattern.FILENAME_012) {
                await Webから追加更新削除_スカラメンバー網羅(pattern);

            } else {
                Assert.Warn($"期待結果が定義されていません: {pattern.Name}");
            }
        }

        private static async Task Webから追加更新削除_スカラメンバー網羅(DataPattern pattern) {

            TestProject.UpdateAutoGeneratedCode(pattern);

            using var launcher = TestProject.Current.CreateLauncher();
            var errors = new StringBuilder();
            launcher.OnError += (s, e) => errors.AppendLine(e);
            launcher.Launch();
            launcher.WaitForReady();

            using var driver = TestProject.CreateWebDriver();
            await driver.InitializeData();

            // 準備: 参照先を作る
            driver.FindElement(Util.ByInnerText("参照先")).Click();
            await driver.AddNewItemAndNavigateToCreateView();

            driver.FindElement(By.Name("Name")).SendKeys("テスト用の参照先");
            driver.FindElement(Util.ByInnerText("一時保存")).Click();
            driver.CommitLocalRepositoryChanges();

            // 新規作成できるか: データ作成
            driver.FindElement(Util.ByInnerText("親集約")).Click();
            await driver.AddNewItemAndNavigateToCreateView();

            driver.FindElement(By.Name("単語")).SendKeys("自動テストで作られたデータ");
            driver.ActivateTextarea("文章").SendKeys("このデータは自動テストで作られました。\r\n");
            driver.FindElement(By.Name("整数")).SendKeys("ー９９９９９９９９９９９９９９");
            driver.FindElement(By.Name("実数")).SendKeys("-９.９９９９９９９９９９９９９９");
            driver.FindElement(By.Name("日付時刻")).SendKeys("２０００－１－１　００：０１");
            driver.FindElement(By.Name("日付")).SendKeys("１９９９－２－３");
            driver.FindElement(By.Name("年月")).SendKeys("１９９８－１２");
            driver.FindElement(By.Name("年")).SendKeys("１９９７");
            driver.FindElement(By.Name("参照")).SendKeys("テスト用の参照先");
            driver.FindElement(By.Name("参照")).SendKeys(Keys.Tab);
            driver.FindElement(By.Name("真偽値")).SendKeys(Keys.Space);
            driver.FindElement(Util.ByInnerText("選択肢2")).Click(); // 列挙体

            // TODO: Childrenの各項目を入力

            var beforeCreate = driver.CaptureScreenShot();

            driver.FindElement(Util.ByInnerText("一時保存")).Click();
            driver.CommitLocalRepositoryChanges();

            // 新規作成できるか: 結果確認
            driver.FindElement(Util.ByInnerText("親集約")).Click();
            driver.FindElement(By.Name("単語")).SendKeys("自動テストで作られたデータ");
            await driver.SearchSingleAndNavigateToEditView();

            var afterCreate = driver.CaptureScreenShot();

            try {
                Assert.That(afterCreate, Is.EqualTo(beforeCreate));
            } catch {
                beforeCreate.SaveAsFile("期待結果.png");
                afterCreate.SaveAsFile("実際の値.png");
                throw;
            }
        }

        private static async Task Webから追加更新削除_Refのみ(DataPattern pattern) {

            TestProject.UpdateAutoGeneratedCode(pattern);

            // 開始
            using var launcher = TestProject.Current.CreateLauncher();
            var exceptions = new List<Exception>();
            launcher.OnError += (s, e) => {
                exceptions.Add(new Exception(e.ToString()));
                launcher.Terminate();
                Assert.Fail($"Launcher catched error: {e}");
            };

            launcher.Launch();
            launcher.WaitForReady();

            using var driver = TestProject.CreateWebDriver();
            await driver.InitializeData();

            // 準備: 参照先を作る
            driver.FindElement(Util.ByInnerText("参照先")).Click();
            await driver.AddNewItemAndNavigateToCreateView();

            driver.FindElement(By.Name("参照先集約ID")).SendKeys("あああああ");
            driver.FindElement(By.Name("参照先集約名")).SendKeys("いいいいい");
            driver.FindElement(Util.ByInnerText("一時保存")).Click();
            driver.CommitLocalRepositoryChanges();

            // 参照元を作成
            driver.FindElement(Util.ByInnerText("参照元")).Click();
            await driver.AddNewItemAndNavigateToCreateView();

            driver.FindElement(By.Name("参照元集約ID")).SendKeys("ううううう");
            driver.FindElement(By.Name("参照元集約名")).SendKeys("えええええ");
            driver.FindElement(By.Name("参照")).SendKeys("いいいいい");
            driver.FindElement(By.Name("参照")).SendKeys(Keys.Tab);
            driver.FindElement(Util.ByInnerText("一時保存")).Click();
            driver.CommitLocalRepositoryChanges();

            // 作成ができているか確認
            driver.FindElement(Util.ByInnerText("参照元")).Click();
            await Util.WaitUntil(() => driver.FindElements(Util.ByInnerText("ううううう")).Count > 0);
            Assert.Multiple(() => {
                Assert.That(driver.FindElements(Util.ByInnerText("ううううう")).Count, Is.EqualTo(1));
                Assert.That(driver.FindElements(Util.ByInnerText("えええええ")).Count, Is.EqualTo(1));
            });

            driver.FindElements(Util.ByInnerText("詳細"))[Util.DUMMY_DATA_COUNT].Click();

            // 参照元を更新

            // 更新ができているか確認

            // 参照元を削除

            // 削除ができているか確認

            if (exceptions.Count != 0) {
                throw new AggregateException(exceptions.ToArray());
            }

            TestContext.WriteLine("正常終了");
        }
    }
}
