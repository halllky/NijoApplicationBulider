using Nijo.Core;
using Nijo.Util.DotnetEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nijo.Parts.WebServer;
using Nijo.Util.CodeGenerating;
using Nijo.Models;

namespace Nijo.Features.Storing {
    internal class DeleteFeature {
        internal DeleteFeature(GraphNode<Aggregate> aggregate) {
            _aggregate = aggregate;
        }

        private readonly GraphNode<Aggregate> _aggregate;

        internal string ArgType => _aggregate.Item.ClassName;
        internal string MethodName => $"Delete{_aggregate.Item.DisplayName.ToCSharpSafe()}";

        internal string RenderController() {
            var controller = new Parts.WebClient.Controller(_aggregate.Item);
            var args = GetEFCoreMethodArgs();

            return $$"""
                [HttpDelete("{{Parts.WebClient.Controller.DELETE_ACTION_NAME}}/{{args.Select(a => "{" + a.MemberName + "}").Join("/")}}")]
                public virtual IActionResult Delete({{args.Select(m => $"{m.CSharpTypeName} {m.MemberName}").Join(", ")}}) {
                    if (_applicationService.{{MethodName}}({{args.Select(a => a.MemberName).Join(", ")}}, out var errors)) {
                        return Ok();
                    } else {
                        return BadRequest(this.JsonContent(errors));
                    }
                }
                """;
        }

        internal string RenderAppSrvMethod() {
            var appSrv = new ApplicationService();
            var controller = new Parts.WebClient.Controller(_aggregate.Item);
            var args = GetEFCoreMethodArgs().ToArray();
            var find = new FindFeature(_aggregate);
            var instanceClass = new AggregateDetail(_aggregate).ClassName;

            return $$"""
                public virtual bool {{MethodName}}({{args.Select(m => $"{m.CSharpTypeName} {m.MemberName}").Join(", ")}}, out ICollection<string> errors) {

                    {{WithIndent(find.RenderDbEntityLoading(
                        appSrv.DbContext,
                        "entity",
                        args.Select(a => a.MemberName).ToArray(),
                        tracks: true,
                        includeRefs: true), "    ")}}

                    if (entity == null) {
                        errors = new[] { "削除対象のデータが見つかりません。" };
                        return false;
                    }

                    var deleted = {{instanceClass}}.{{AggregateDetail.FROM_DBENTITY}}(entity);

                    {{appSrv.DbContext}}.Remove(entity);

                    try {
                        {{appSrv.DbContext}}.SaveChanges();
                    } catch (DbUpdateException ex) {
                        errors = ex.GetMessagesRecursively().ToArray();
                        return false;
                    }

                    // {{_aggregate.Item.DisplayName}}の更新をトリガーとする処理を実行します。
                    var updateEvent = new AggregateUpdateEvent<{{instanceClass}}> {
                        Deleted = new[] { deleted },
                    };
                    {{_aggregate.GetDependsOnMarkedReadModels().SelectTextTemplate(readModel => $$"""
                    {{WithIndent(ReadModel.RenderUpdateCalling(readModel, "updateEvent"), "    ")}}
                    """)}}

                    errors = Array.Empty<string>();
                    return true;
                }
                """;
        }

        private IEnumerable<AggregateMember.AggregateMemberBase> GetEFCoreMethodArgs() {
            return _aggregate
                .GetKeys()
                .Where(m => m is AggregateMember.ValueMember);
        }
    }
}
