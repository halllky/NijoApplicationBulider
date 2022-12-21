﻿using System;
using System.IO;

namespace haldoc {
    class Program {
        static void Main(string[] args) {
            Console.WriteLine("コード自動生成 開始");

            var context = new Core.ProjectContext(
                "サンプルプロジェクト",
                System.Reflection.Assembly.GetExecutingAssembly());

            File.WriteAllText(
                @"../../../../haldoc/AutoGenerated/EFCode.cs",
                new CodeGenerating.EFCodeGenerator(context).TransformText());

            Console.WriteLine("コード自動生成 終了");
        }
    }
}
