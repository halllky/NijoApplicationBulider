using Nijo.Core;
using Nijo.Util.DotnetEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nijo.Util.CodeGenerating;
using Nijo.Parts;
using Nijo.Parts.WebServer;
using Nijo.Models;

namespace Nijo.Features.Storing {
    internal class UpdateFeature {
        internal UpdateFeature(GraphNode<Aggregate> aggregate) {
            _aggregate = aggregate;
        }

        private readonly GraphNode<Aggregate> _aggregate;

        internal string ArgType => _aggregate.Item.ClassName;
        internal string MethodName => $"Update{_aggregate.Item.DisplayName.ToCSharpSafe()}";

        internal string RenderController() {
            var controller = new Parts.WebClient.Controller(_aggregate.Item);

            return $$"""
                [HttpPost("{{Parts.WebClient.Controller.UPDATE_ACTION_NAME}}")]
                public virtual IActionResult Update({{_aggregate.Item.ClassName}} param) {
                    if (_applicationService.{{MethodName}}(param, out var updated, out var errors)) {
                        return this.JsonContent(updated);
                    } else {
                        return BadRequest(this.JsonContent(errors));
                    }
                }
                """;
        }

        internal string RenderAppSrvMethod() {
            var appSrv = new ApplicationService();
            var controller = new Parts.WebClient.Controller(_aggregate.Item);
            var find = new FindFeature(_aggregate);

            var detail = new AggregateDetail(_aggregate);
            var searchKeys = _aggregate
                .GetKeys()
                .OfType<AggregateMember.ValueMember>()
                .Select(vm => AggregateDetail.GetPathOf("after", _aggregate, vm).Join("."))
                .ToArray();

            return $$"""
                public virtual bool {{MethodName}}({{detail.ClassName}} after, out {{detail.ClassName}} updated, out ICollection<string> errors) {
                    errors = new List<string>();

                    {{WithIndent(find.RenderDbEntityLoading(
                        appSrv.DbContext,
                        "beforeDbEntity",
                        searchKeys,
                        tracks: false,
                        includeRefs: true), "    ")}}

                    if (beforeDbEntity == null) {
                        updated = new {{_aggregate.Item.ClassName}}();
                        errors.Add("更新対象のデータが見つかりません。");
                        return false;
                    }

                    var beforeUpdate = {{detail.ClassName}}.{{AggregateDetail.FROM_DBENTITY}}(beforeDbEntity);
                    var afterDbEntity = after.{{AggregateDetail.TO_DBENTITY}}();

                    // Attach
                    {{appSrv.DbContext}}.Entry(afterDbEntity).State = EntityState.Modified;

                    {{WithIndent(RenderDescendantsAttaching(appSrv.DbContext, "beforeDbEntity", "afterDbEntity"), "    ")}}

                    try {
                        {{appSrv.DbContext}}.SaveChanges();
                    } catch (DbUpdateException ex) {
                        updated = new {{_aggregate.Item.ClassName}}();
                        foreach (var msg in ex.GetMessagesRecursively()) errors.Add(msg);
                        return false;
                    }

                    var afterUpdate = this.{{find.FindMethodName}}({{searchKeys.Join(", ")}});
                    if (afterUpdate == null) {
                        updated = new {{_aggregate.Item.ClassName}}();
                        errors.Add("更新後のデータの再読み込みに失敗しました。");
                        return false;
                    }

                    // {{_aggregate.Item.DisplayName}}の更新をトリガーとする処理を実行します。
                    var updateEvent = new AggregateUpdateEvent<{{detail.ClassName}}> {
                        Modified = new AggregateBeforeAfter<{{detail.ClassName}}>[] { new() { Before = beforeUpdate, After = afterUpdate } },
                    };
                    {{_aggregate.GetDependsOnMarkedReadModels().SelectTextTemplate(readModel => $$"""
                    {{WithIndent(ReadModel.RenderUpdateCalling(readModel, "updateEvent"), "    ")}}
                    """)}}

                    updated = afterUpdate;
                    return true;
                }
                """;
        }

        private string RenderDescendantsAttaching(string dbContext, string before, string after) {
            var builder = new StringBuilder();

            var descendantDbEntities = _aggregate.EnumerateDescendants().ToArray();
            for (int i = 0; i < descendantDbEntities.Length; i++) {
                var paths = descendantDbEntities[i].PathFromEntry().ToArray();

                // before, after それぞれの子孫インスタンスを一次配列に格納する
                void RenderEntityArray(bool renderBefore) {
                    if (paths.Any(path => path.Terminal.As<Aggregate>().IsChildrenMember())) {
                        // 子集約までの経路の途中に配列が含まれる場合
                        builder.AppendLine($"var arr{i}_{(renderBefore ? "before" : "after")} = {(renderBefore ? before : after)}");

                        var select = false;
                        foreach (var path in paths) {
                            if (select && path.Terminal.As<Aggregate>().IsChildrenMember()) {
                                builder.AppendLine($"    .SelectMany(x => x.{path.RelationName})");
                            } else if (select) {
                                builder.AppendLine($"    .Select(x => x.{path.RelationName})");
                            } else {
                                builder.AppendLine($"    .{path.RelationName}?");
                                if (path.Terminal.As<Aggregate>().IsChildrenMember()) select = true;
                            }
                        }
                        builder.AppendLine($"    .OfType<{descendantDbEntities[i].Item.EFCoreEntityClassName}>()");
                        builder.AppendLine($"    ?? Enumerable.Empty<{descendantDbEntities[i].Item.EFCoreEntityClassName}>();");

                    } else {
                        // 子集約までの経路の途中に配列が含まれない場合
                        builder.AppendLine($"var arr{i}_{(renderBefore ? "before" : "after")} = new {descendantDbEntities[i].Item.EFCoreEntityClassName}?[] {{");
                        builder.AppendLine($"    {(renderBefore ? before : after)}.{paths.Select(p => p.RelationName).Join("?.")},");
                        builder.AppendLine($"}}.OfType<{descendantDbEntities[i].Item.EFCoreEntityClassName}>().ToArray();");
                    }
                }
                RenderEntityArray(true);
                RenderEntityArray(false);

                // ChangeState変更
                builder.AppendLine($"foreach (var a in arr{i}_after) {{");
                builder.AppendLine($"    var b = arr{i}_before.SingleOrDefault(b => b.{Aggregate.KEYEQUALS}(a));");
                builder.AppendLine($"    if (b == null) {{");
                builder.AppendLine($"        {dbContext}.Entry(a).State = EntityState.Added;");
                builder.AppendLine($"    }} else {{");
                builder.AppendLine($"        {dbContext}.Entry(a).State = EntityState.Modified;");
                builder.AppendLine($"    }}");
                builder.AppendLine($"}}");

                builder.AppendLine($"foreach (var b in arr{i}_before) {{");
                builder.AppendLine($"    var a = arr{i}_after.SingleOrDefault(a => a.{Aggregate.KEYEQUALS}(b));");
                builder.AppendLine($"    if (a == null) {{");
                builder.AppendLine($"        {dbContext}.Entry(b).State = EntityState.Deleted;");
                builder.AppendLine($"    }}");
                builder.AppendLine($"}}");
            }

            return builder.ToString();
        }
    }
}
