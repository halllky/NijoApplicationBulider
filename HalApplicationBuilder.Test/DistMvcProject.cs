﻿using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Firefox;
using static HalApplicationBuilder.Core.DBModel.SelectStatement;

namespace HalApplicationBuilder.Test {
    public class DistMvcProject {
        public static DistMvcProject Instance { get; } = new DistMvcProject();

        private DistMvcProject() { }

        private readonly object _lockObject = new();

        private static HalApplicationBuilder.Core.Config GetConfig() => new() {
            OutProjectDir = AppSettings.Load().GetTestAppCsprojDir(),

            EntityFrameworkDirectoryRelativePath = "EntityFramework/__AutoGenerated",
            DbContextName = "MyDbContext",
            DbContextNamespace = "HalApplicationBuilder.Test.DistMvc.EntityFramework",
            EntityNamespace = "HalApplicationBuilder.Test.DistMvc.EntityFramework.Entities",

            MvcModelDirectoryRelativePath = "Models/__AutoGenerated",
            MvcModelNamespace = "HalApplicationBuilder.Test.DistMvc.Models",

            MvcControllerDirectoryRelativePath = "Controllers/__AutoGenerated",
            MvcControllerNamespace = "HalApplicationBuilder.Test.DistMvc.Controllers",

            MvcViewDirectoryRelativePath = "Views/_AutoGenerated",
        };

        public Assembly GetAssembly() {
            lock (_lockObject) {
                var csprojDir = AppSettings.Load().GetTestAppCsprojDir();
                var path = Path.Combine(csprojDir, "bin", "Debug", "net7.0", "HalApplicationBuilder.Test.DistMvc.dll");
                return Assembly.LoadFile(path);
            }
        }

        public DistMvcProject GenerateCode(string? @namespace) {
            lock (_lockObject) {
                var serviceCollection = new ServiceCollection();
                HalApp.Configure(
                    serviceCollection,
                    GetConfig(),
                    Assembly.GetExecutingAssembly(),
                    @namespace);
                var provider = serviceCollection.BuildServiceProvider();
                var halapp = provider.GetRequiredService<HalApp>();

                halapp.GenerateCode();
            }
            return this;
        }

        public WebProcess RunWebProcess() => WebProcess.Run();

        public class WebProcess : IDisposable
        {
            public static WebProcess Run()
            {
                var process = new Process();
                process.StartInfo.WorkingDirectory = "../../../../HalApplicationBuilder.Test.DistMvc";
                process.StartInfo.FileName = "dotnet";
                process.StartInfo.ArgumentList.Add("run");

                process.StartInfo.UseShellExecute = false;

                process.StartInfo.RedirectStandardInput = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.StandardInputEncoding = Encoding.UTF8;
                process.StartInfo.StandardErrorEncoding = Encoding.UTF8;
                process.StartInfo.StandardOutputEncoding = Encoding.UTF8;

                var webPrcess = new WebProcess(process);
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                return webPrcess;
            }

            private WebProcess(Process process)
            {
                _process = process;
                _process.OutputDataReceived += StdOutReceived;
                _process.ErrorDataReceived += StdErrReceived;
            }


            private void StdOutReceived(object sender, DataReceivedEventArgs e)
            {
                _stdOut.AppendLine("STDOUT:: " + e.Data);
                Console.WriteLine("STDOUT:: " + e.Data);
            }
            private void StdErrReceived(object sender, DataReceivedEventArgs e)
            {
                _stdOut.AppendLine("STDERR:: " + e.Data);
                Console.WriteLine("STDERR:: " + e.Data);
            }

            private readonly StringBuilder _stdOut = new();


            private Uri? _rootUrl;
            public Uri GetRootURL()
            {
                if (_rootUrl != null) return _rootUrl;

                var regex = new Regex(@"Now listening on: (http.*)");
                var timeout = DateTime.Now.AddSeconds(30);
                while (DateTime.Now <= timeout)
                {
                    var match = regex.Match(_stdOut.ToString());
                    if (match.Success)
                    {
                        _rootUrl = new Uri(match.Groups[1].Value);
                        return _rootUrl;
                    }
                    Thread.Sleep(1000);
                }

                throw new InvalidOperationException($"テストアプリケーションのURLを特定できません。");
            }

            public IWebDriver GetChromeDriver()
            {
                var driver = new ChromeDriver();
                driver.Navigate().GoToUrl(GetRootURL());
                return driver;
            }
            public IWebDriver GetFireFoxDriver()
            {
                var driver = new FirefoxDriver();
                driver.Navigate().GoToUrl(GetRootURL());
                return driver;
            }

            private readonly Process _process;
            private bool _disposed = false;
            public void Dispose()
            {
                if (!_disposed)
                {
                    _process.Kill(entireProcessTree: true);
                    _process.Dispose();
                    _disposed = true;
                }
            }
        }
    }
}
