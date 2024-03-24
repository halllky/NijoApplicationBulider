using Nijo.Parts.WebServer;
using Nijo.Core;
using Nijo.Util.CodeGenerating;
using Nijo.Util.DotnetEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nijo.Features;

namespace Nijo.Models {
    internal class ReadModel : IModel {

        private const string CONTROLLER_ACTION_NAME = "reload";

        internal static string AppSrvMethodName(GraphNode<Aggregate> rootAggregate) {
            if (rootAggregate.Item.Options.Handler != NijoCodeGenerator.Models.ReadModel.Key)
                throw new InvalidOperationException($"{rootAggregate.Item} is not a read model.");
            return $"Reload{rootAggregate.Item.ClassName}";
        }
        internal static string RenderUpdateCalling(GraphNode<Aggregate> readModel, string aggregateChangedEvent) {
            var reload = AppSrvMethodName(readModel);
            return $$"""
                this.{{reload}}({{aggregateChangedEvent}});
                """;
        }

        void IModel.GenerateCode(CodeRenderingContext context, GraphNode<Aggregate> rootAggregate) {

            // WriteModelへのリンクを作成する
            // TOOD: ↑ is="ref-to:…" によって自動的に作成されるのでは？

            // TODO: MultiView, 検索処理を作成する

            context.UseAggregateFile(rootAggregate, builder => {
                var appSrv = new ApplicationService();
                var dbContext = appSrv.DbContext;
                var dbSet = rootAggregate.Item.DbSetName;
                var reload = AppSrvMethodName(rootAggregate);
                var controllerArgs = KeyArray.Create(rootAggregate, nullable: true);
                var appServiceArgs = KeyArray.Create(rootAggregate, nullable: false);

                // データ更新処理を作成する: Controller
                builder.ControllerActions.Add($$"""
                    [HttpGet("{{CONTROLLER_ACTION_NAME}}/{{controllerArgs.Select(m => "{" + m.VarName + "}").Join("/")}}")]
                    public virtual IActionResult Reload({{controllerArgs.Select(m => $"{m.CsType} {m.VarName}").Join(", ")}}) {
                    {{controllerArgs.SelectTextTemplate(m => $$"""
                        if ({{m.VarName}} == null) return BadRequest();
                    """)}}

                        using (var tran = _applicationService.{{dbContext}}.Database.BeginTransaction()) {
                            try {
                                _applicationService.{{reload}}({{appServiceArgs.Select(m => m.VarName).Join(", ")}});
                                tran.Commit();
                            } catch {
                                tran.Rollback();
                            }
                        }

                        return Ok();
                    }
                    """);

                // データ更新処理を作成する: ApplicationService
                // ※以下の理由からほぼなにも自動生成しない
                // - 一度の更新でどのエンティティまでIncludeすればよいかの自動判別が難しい
                // - ReadModelの主キーが必ずしもWriteModelの集約1種類へのRefのみで構成されるとは限らない
                var entity = $"{rootAggregate.Item.EFCoreEntityClassName}?";
                var dependencies = rootAggregate
                    .GetDependsOnMarkedWriteModels()
                    .Select(writeModel => writeModel.GetRoot())
                    .Distinct();

                builder.AppServiceMethods.Add($$"""
                    /// <summary>
                    /// {{rootAggregate.Item.DisplayName}}のデータ1件の更新処理。
                    /// 画面などからID指定でデータの洗い替えが指示されたときに実行されます。
                    /// </summary>
                    public virtual void {{reload}}({{appServiceArgs.Select(k => $"{k.CsType} {k.VarName}").Join(", ")}}) {
                        /// <see cref="{{appSrv.ConcreteClass}}"/>クラスでこのメソッドをオーバーライドして、
                        /// DbContextを操作し、対象データを追加、更新または削除する処理を実装してください。

                        // 実装例:
                        // var source = {{dbContext}}.<算出元データ>
                        //     .AsNoTracking()
                        //     .Include(<更新に必要な各種関連データ>)
                        //     .SingleOrDefault(e => {{appServiceArgs.Select(k => $"e.{k.VarName}").Join(" && ")}});
                        // var readModel = {{dbContext}}.{{dbSet}}
                        //     .Include(<一緒に更新する子孫データ>)
                        //     .SingleOrDefault(e => {{appServiceArgs.Select(k => $"e.{k.VarName}").Join(" && ")}});
                        // if (source == null) {
                        //     if (readModel != null) {{dbContext}}.{{dbSet}}.Remove(readModel);
                        //     return;
                        // }
                        // var insert = readModel == null;
                        // if (insert) readModel = new {{rootAggregate.Item.EFCoreEntityClassName}} { };
                        //
                        // // ここでsourceの値をもとにreadModelの値を算出する
                        //
                        // if (insert) {{dbContext}}.{{dbSet}}.Add(readModel);
                        // _applicationService.{{dbContext}}.SaveChanges();
                    }
                    {{dependencies.SelectTextTemplate(writeModel => $$"""
                    /// <summary>
                    /// {{rootAggregate.Item.DisplayName}}のデータの更新処理。
                    /// {{writeModel.Item.DisplayName}}が追加・削除・更新された後、コミットされる前に実行されます。
                    /// </summary>
                    public virtual void {{reload}}(AggregateUpdateEvent<{{new Features.Storing.AggregateDetail(writeModel).ClassName}}> ev) {
                        /// <see cref="{{appSrv.ConcreteClass}}"/>クラスでこのメソッドをオーバーライドして、
                        /// DbContextを操作し、対象データを追加、更新または削除する処理を実装してください。
                    }
                    """)}}
                    """);

                // AggregateDetailクラス定義を作成する
                foreach (var aggregate in rootAggregate.EnumerateThisAndDescendants()) {
                    var aggregateDetail = new Features.Storing.AggregateDetail(aggregate);
                    builder.DataClassDeclaring.Add(aggregateDetail.RenderCSharp(context));
                    builder.TypeScriptDataTypes.Add(aggregateDetail.RenderTypeScript(context));
                }

                // EFCoreエンティティ定義を作成する
                builder.HasDbSet = true;
                var dbContextClass = new DbContextClass(context.Config);

                foreach (var aggregate in rootAggregate.EnumerateThisAndDescendants()) {
                    // OnModelCreating
                    builder.OnModelCreating.Add(modelBuilder => dbContextClass.RenderEntity(modelBuilder, aggregate));

                    // DbEntity
                    static IEnumerable<NavigationProperty.PrincipalOrRelevant> EnumerateNavigationProperties(GraphNode<Aggregate> aggregate) {
                        foreach (var nav in aggregate.GetNavigationProperties()) {
                            if (nav.Principal.Owner == aggregate) yield return nav.Principal;
                            if (nav.Relevant.Owner == aggregate) yield return nav.Relevant;
                        }
                    }
                    builder.DataClassDeclaring.Add($$"""
                        /// <summary>
                        /// {{aggregate.Item.DisplayName}}のデータベースに保存されるデータの形を表すクラスです。
                        /// </summary>
                        public partial class {{aggregate.Item.EFCoreEntityClassName}} {
                        {{aggregate.GetMembers().OfType<AggregateMember.ValueMember>().SelectTextTemplate(col => $$"""
                            public {{col.Options.MemberType.GetCSharpTypeName()}}? {{col.MemberName}} { get; set; }
                        """)}}

                        {{EnumerateNavigationProperties(aggregate).SelectTextTemplate(nav => $$"""
                            public virtual {{nav.CSharpTypeName}} {{nav.PropertyName}} { get; set; }
                        """)}}

                            /// <summary>このオブジェクトと比較対象のオブジェクトの主キーが一致するかを返します。</summary>
                            public bool {{Aggregate.KEYEQUALS}}({{aggregate.Item.EFCoreEntityClassName}} entity) {
                        {{aggregate.GetKeys().OfType<AggregateMember.ValueMember>().SelectTextTemplate(col => $$"""
                                if (entity.{{col.MemberName}} != this.{{col.MemberName}}) return false;
                        """)}}
                                return true;
                            }
                        }
                        """);
                }
            });

        }

    }
}
