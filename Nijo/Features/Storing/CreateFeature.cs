using Nijo.Core;
using Nijo.Util.DotnetEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nijo.Parts;
using Nijo.Parts.WebServer;
using Nijo.Util.CodeGenerating;
using Nijo.Models;

namespace Nijo.Features.Storing {
    internal class CreateFeature {
        internal CreateFeature(GraphNode<Aggregate> aggregate) {
            _aggregate = aggregate;
        }

        private readonly GraphNode<Aggregate> _aggregate;

        internal string ArgType => new AggregateCreateCommand(_aggregate).ClassName;
        internal string MethodName => $"Create{_aggregate.Item.DisplayName.ToCSharpSafe()}";

        internal string RenderController() {
            var controller = new Parts.WebClient.Controller(_aggregate.Item);
            var param = new AggregateCreateCommand(_aggregate);

            return $$"""
                [HttpPost("{{Parts.WebClient.Controller.CREATE_ACTION_NAME}}")]
                public virtual IActionResult Create([FromBody] {{param.ClassName}} param) {
                    if (_applicationService.{{MethodName}}(param, out var created, out var errors)) {
                        return this.JsonContent(created);
                    } else {
                        return BadRequest(this.JsonContent(errors));
                    }
                }
                """;
        }

        internal string RenderAppSrvMethod() {
            var appSrv = new ApplicationService();
            var controller = new Parts.WebClient.Controller(_aggregate.Item);
            var instanceClass = new AggregateDetail(_aggregate).ClassName;
            var param = new AggregateCreateCommand(_aggregate);
            var find = new FindFeature(_aggregate);

            var searchKeys = _aggregate
                .GetKeys()
                .Where(m => m is AggregateMember.ValueMember)
                .Select(m => $"dbEntity.{m.GetFullPath().Join(".")}");

            return $$"""
                public virtual bool {{MethodName}}({{param.ClassName}} command, out {{instanceClass}} created, out ICollection<string> errors) {
                    var dbEntity = command.{{AggregateDetail.TO_DBENTITY}}();
                    {{appSrv.DbContext}}.Add(dbEntity);

                    try {
                        {{appSrv.DbContext}}.SaveChanges();
                    } catch (DbUpdateException ex) {
                        created = new {{instanceClass}}();
                        errors = ex.GetMessagesRecursively("  ").ToList();
                        return false;
                    }

                    var afterUpdate = this.{{find.FindMethodName}}({{searchKeys.Join(", ")}});
                    if (afterUpdate == null) {
                        created = new {{instanceClass}}();
                        errors = new[] { "更新後のデータの再読み込みに失敗しました。" };
                        return false;
                    }

                    created = afterUpdate;
                    errors = new List<string>();

                    // {{_aggregate.Item.DisplayName}}の更新をトリガーとする処理を実行します。
                    var updateEvent = new AggregateUpdateEvent<{{instanceClass}}> {
                        Created = new[] { afterUpdate },
                    };
                    {{_aggregate.GetDependsOnMarkedReadModels().SelectTextTemplate(readModel => $$"""
                    {{WithIndent(ReadModel.RenderUpdateCalling(readModel, "updateEvent"), "    ")}}
                    """)}}

                    return true;
                }
                """;
        }
    }
}
