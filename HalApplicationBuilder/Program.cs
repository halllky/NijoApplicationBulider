﻿using System;
using System.IO;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.DependencyInjection;

namespace HalApplicationBuilder {
    public class Program {

        public static void ConfigureServices(IServiceCollection serviceCollection, string dllPath) {
            serviceCollection.AddScoped(_ => new Core.Config {
                DbContextName = "SampleDbContext",
                DbContextNamespace = "HalApplicationBuilderSampleMvc.EntityFramework",
                EntityNamespace = "HalApplicationBuilderSampleMvc.EntityFramework.Entities",
                MvcModelNamespace = "HalApplicationBuilderSampleMvc.Models",
                MvcControllerNamespace = "HalApplicationBuilderSampleMvc.Controllers",
                MvcViewDirectoryRelativePath = "Views/__AutoGenerated",
            });
            serviceCollection.AddScoped(provider => {
                var dll = Assembly.LoadFile(dllPath);
                return new Impl.SchemaImpl(dll, provider);
            });
            serviceCollection.AddScoped<Core.IAggregateMemberFactory>(provider => new Impl.AggregateMemberFactory(provider));
            serviceCollection.AddScoped<Core.IApplicationSchema>(provider => provider.GetRequiredService<Impl.SchemaImpl>());
            serviceCollection.AddScoped<AspNetMvc.IViewModelProvider>(provider => provider.GetRequiredService<Impl.SchemaImpl>());
            serviceCollection.AddScoped<EntityFramework.IDbSchema>(provider => provider.GetRequiredService<Impl.SchemaImpl>());
        }

        static void Main(string[] args) {
            var dllPath = "/__local__/20221211_haldoc_csharp/haldoc/HalApplicationBuilderSampleSchema/bin/Debug/net5.0/HalApplicationBuilderSampleSchema.dll";
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection, dllPath);
            var service = serviceCollection.BuildServiceProvider();

            var validator = new Core.AggregateValidator(service);
            if (validator.HasError(error => Console.WriteLine(error))) {
                Console.WriteLine("コード自動生成終了");
                return;
            }

            var dllFileInfo = new FileInfo(dllPath);
            Console.WriteLine($"コード自動生成開始: {dllFileInfo.Name} (最終更新時刻: {dllFileInfo.LastWriteTime:G})");

            Console.WriteLine("コード自動生成: Entity定義");
            var efSourceDir = "/__local__/20221211_haldoc_csharp/haldoc/HalApplicationBuilderSampleMvc/EntityFramework";
            var efSourceFile = Path.Combine(efSourceDir, "__AutoGeneratedEntities.cs");
            if (!Directory.Exists(efSourceDir)) Directory.CreateDirectory(efSourceDir);
            using (var sw = new StreamWriter(efSourceFile, append: false, encoding: Encoding.UTF8)) {
                var source = new EntityFramework.EFCoreSource();
                sw.Write(source.TransformText(
                    service.GetRequiredService<Core.IApplicationSchema>(),
                    service.GetRequiredService<EntityFramework.IDbSchema>(),
                    service.GetRequiredService<Core.Config>()));
            }

            Console.WriteLine("コード自動生成: MVC Model");
            var modelDir = "/__local__/20221211_haldoc_csharp/haldoc/HalApplicationBuilderSampleMvc/Models";
            var modelFile = Path.Combine(modelDir, "__AutoGeneratedModels.cs");
            if (!Directory.Exists(modelDir)) Directory.CreateDirectory(modelDir);
            using (var sw = new StreamWriter(modelFile, append: false, encoding: Encoding.UTF8)) {
                var source = new AspNetMvc.MvcModels();
                sw.Write(source.TransformText(
                    service.GetRequiredService<Core.IApplicationSchema>(),
                    service.GetRequiredService<AspNetMvc.IViewModelProvider>(),
                    service.GetRequiredService<Core.Config>()));
            }

            //Console.WriteLine("コード自動生成: MVC View - 既存ファイル削除");
            var viewDir = Path.Combine(
                "/__local__/20221211_haldoc_csharp/haldoc/HalApplicationBuilderSampleMvc",
                service.GetRequiredService<Core.Config>().MvcViewDirectoryRelativePath);
            if (!Directory.Exists(viewDir)) Directory.CreateDirectory(viewDir);
            //foreach (var file in Directory.GetFiles(viewDir)) {
            //    File.Delete(file);
            //}

            Console.WriteLine("コード自動生成: MVC View - MultiView");
            foreach (var aggregate in service.GetRequiredService<Core.IApplicationSchema>().RootAggregates()) {
                var view = new AspNetMvc.MultiView(aggregate);
                var filename = Path.Combine(viewDir, view.FileName);
                using var sw = new StreamWriter(filename, append: false, encoding: Encoding.UTF8);
                sw.Write(view.TransformText(service.GetRequiredService<AspNetMvc.IViewModelProvider>()));
            }

            Console.WriteLine("コード自動生成: MVC View - SingleView");
            foreach (var aggregate in service.GetRequiredService<Core.IApplicationSchema>().RootAggregates()) {
                var view = new AspNetMvc.SingleView(aggregate);
                var filename = Path.Combine(viewDir, view.FileName);
                using var sw = new StreamWriter(filename, append: false, encoding: Encoding.UTF8);
                sw.Write(view.TransformText(
                    service.GetRequiredService<AspNetMvc.IViewModelProvider>(),
                    service.GetRequiredService<Core.Config>()));
            }

            Console.WriteLine("コード自動生成: MVC View - CreateView");
            foreach (var aggregate in service.GetRequiredService<Core.IApplicationSchema>().RootAggregates()) {
                var view = new AspNetMvc.CreateView(aggregate);
                var filename = Path.Combine(viewDir, view.FileName);
                using var sw = new StreamWriter(filename, append: false, encoding: Encoding.UTF8);
                sw.Write(view.TransformText(
                    service.GetRequiredService<AspNetMvc.IViewModelProvider>(),
                    service.GetRequiredService<Core.Config>()));
            }

            Console.WriteLine("コード自動生成: MVC View - 集約部分ビュー");
            foreach (var aggregate in service.GetRequiredService<Core.IApplicationSchema>().AllAggregates()) {
                var view = new AspNetMvc.InstancePartialView(
                    aggregate,
                    service.GetRequiredService<Core.Config>());
                var filename = Path.Combine(viewDir, view.FileName);
                using var sw = new StreamWriter(filename, append: false, encoding: Encoding.UTF8);
                sw.Write(view.TransformText(
                    service.GetRequiredService<AspNetMvc.IViewModelProvider>()));
            }

            Console.WriteLine("コード自動生成: MVC Controller");
            var controllerDir = "/__local__/20221211_haldoc_csharp/haldoc/HalApplicationBuilderSampleMvc/Controllers";
            var controllerFile = Path.Combine(controllerDir, "__AutoGeneratedControllers.cs");
            if (!Directory.Exists(controllerDir)) Directory.CreateDirectory(controllerDir);
            using (var sw = new StreamWriter(controllerFile, append: false, encoding: Encoding.UTF8)) {
                var source = new AspNetMvc.Controller();
                sw.Write(source.TransformText(
                    service.GetRequiredService<Core.IApplicationSchema>(),
                    service.GetRequiredService<AspNetMvc.IViewModelProvider>(),
                    service.GetRequiredService<Core.Config>()));
            }

            Console.WriteLine("コード自動生成: JS");
            {
                var view = new AspNetMvc.JsTemplate();
                var filename = Path.Combine(viewDir, AspNetMvc.JsTemplate.FILE_NAME);
                using var sw = new StreamWriter(filename, append: false, encoding: Encoding.UTF8);
                sw.Write(view.TransformText());
            }

            Console.WriteLine("コード自動生成終了");
        }
    }
}
