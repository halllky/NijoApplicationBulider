using Nijo.Core;
using Nijo.Features.Storing;
using Nijo.Parts.WebClient;
using Nijo.Parts.WebServer;
using Nijo.Util.CodeGenerating;
using Nijo.Util.DotnetEx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Nijo.Parts {
    internal class App {

        internal const string ASP_UTIL_DIR = "Util";
        internal const string ASP_CONTROLLER_DIR = "Web";
        internal const string REACT_PAGE_DIR = "pages";
        internal const string REACT_UTIL_DIR = "util";

        internal List<Func<string, string>> ConfigureServices { get; } = new List<Func<string, string>>();
        internal List<Func<string, string>> ConfigureServicesWhenWebServer { get; } = new List<Func<string, string>>();
        internal List<Func<string, string>> ConfigureServicesWhenBatchProcess { get; } = new List<Func<string, string>>();
        internal List<Func<string, string>> ConfigureWebApp { get; } = new List<Func<string, string>>();

        internal List<IReactPage> ReactPages { get; } = new List<IReactPage>();
        internal List<string> AppSrvMethods { get; } = new List<string>();
        internal List<string> DashBoardImports { get; } = new List<string>();
        internal List<string> DashBoardContents { get; } = new List<string>();

        internal readonly Dictionary<GraphNode<Aggregate>, AggregateFile> _itemsByAggregate = new();
        internal void Aggregate(GraphNode<Aggregate> aggregate, Action<AggregateFile> fn) {
            if (!_itemsByAggregate.TryGetValue(aggregate, out var item)) {
                item = new AggregateFile(aggregate);
                _itemsByAggregate.Add(aggregate, item);
            }
            fn(item);
        }

        internal void GenerateCode(CodeRenderingContext context) {
            context.EditWebApiDirectory(genDir => {
                genDir.Generate(Configure.Render(
                    context,
                    ConfigureServicesWhenWebServer,
                    ConfigureWebApp,
                    ConfigureServicesWhenBatchProcess,
                    ConfigureServices));
                genDir.Generate(EnumDefs.Render(context));
                genDir.Generate(new ApplicationService().Render(context, AppSrvMethods));

                genDir.Directory(ASP_UTIL_DIR, utilDir => {
                    utilDir.Generate(RuntimeSettings.Render(context));
                    utilDir.Generate(Parts.Utility.DotnetExtensions.Render(context));
                    utilDir.Generate(Parts.Utility.AggregateUpdateEvent.Render(context));
                    utilDir.Generate(Parts.Utility.FromTo.Render(context));
                    utilDir.Generate(Parts.Utility.UtilityClass.RenderJsonConversionMethods(context));
                });
                genDir.Directory("Web", controllerDir => {

                });
                genDir.Directory("EntityFramework", efDir => {
                    var onModelCreating = _itemsByAggregate
                        .Where(x => x.Value.OnModelCreating.Any())
                        .Select(x => $"OnModelCreating_{x.Key.Item.ClassName}");
                    efDir.Generate(new DbContextClass(context.Config).RenderDeclaring(context, onModelCreating));
                });

                foreach (var aggFile in _itemsByAggregate.Values) {
                    genDir.Generate(aggFile.Render(context));
                }
            });

            context.EditReactDirectory(reactDir => {

                reactDir.CopyEmbeddedResource(context.EmbeddedResources
                    .Get("REACT_AND_WEBAPI", "react", "src", "__autoGenerated", "index.tsx"));
                reactDir.CopyEmbeddedResource(context.EmbeddedResources
                    .Get("REACT_AND_WEBAPI", "react", "src", "__autoGenerated", "nijo-default-style.css"));

                reactDir.Generate(TypesTsx.Render(context, _itemsByAggregate.Select(x => KeyValuePair.Create(x.Key, x.Value.TypeScriptDataTypes))));
                reactDir.Generate(MenuTsx.Render(context, ReactPages));

                reactDir.Directory("collection", layoutDir => {
                    var resources = context.EmbeddedResources
                        .Enumerate("REACT_AND_WEBAPI", "react", "src", "__autoGenerated", "collection");
                    foreach (var resource in resources) {
                        layoutDir.CopyEmbeddedResource(resource);
                    }
                });
                reactDir.Directory("input", userInputDir => {
                    var resources = context.EmbeddedResources
                        .Enumerate("REACT_AND_WEBAPI", "react", "src", "__autoGenerated", "input");
                    foreach (var resource in resources) {
                        userInputDir.CopyEmbeddedResource(resource);
                    }

                    // TODO: どの集約がコンボボックスを作るのかをNijoFeatureBaseに主導権握らせたい
                    userInputDir.Generate(ComboBox.RenderDeclaringFile(context));
                });
                reactDir.Directory("util", reactUtilDir => {
                    var resources = context.EmbeddedResources
                        .Enumerate("REACT_AND_WEBAPI", "react", "src", "__autoGenerated", "util");
                    foreach (var resource in resources) {
                        reactUtilDir.CopyEmbeddedResource(resource);
                    }
                });
                reactDir.Directory(REACT_PAGE_DIR, pageDir => {

                    pageDir.Generate(DashBoard.Generate(context, this));

                    var resources = context.EmbeddedResources
                        .Enumerate("REACT_AND_WEBAPI", "react", "src", "__autoGenerated", "pages");
                    foreach (var resource in resources) {
                        pageDir.CopyEmbeddedResource(resource);
                    }

                    foreach (var group in ReactPages.GroupBy(p => p.DirNameInPageDir)) {
                        pageDir.Directory(group.Key, aggregatePageDir => {
                            foreach (var page in group) {
                                aggregatePageDir.Generate(page.GetSourceFile());
                            }
                        });
                    }
                });
            });
        }
    }
}
