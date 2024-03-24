using Nijo.Core;
using Nijo.Parts.WebServer;
using Nijo.Util.CodeGenerating;
using Nijo.Util.DotnetEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nijo.Features.Storing {
    /// <summary>
    /// SingleViewの画面構成にあわせたデータ（集約1件および隣接する集約）をDBから引っ張ってくる処理
    /// </summary>
    internal class FindOneFeature {
        internal FindOneFeature(GraphNode<Aggregate> aggregate) {
            _aggregate = aggregate;
        }

        private readonly GraphNode<Aggregate> _aggregate;

        internal string FindMethodReturnType => _aggregate.Item.ClassName;
        private const string ACTION_NAME = "load-one";

        internal string GetUrlStringForReact(IEnumerable<string> keyVariables) {
            var controller = new Parts.WebClient.Controller(_aggregate.Item);
            var encoded = keyVariables.Select(key => $"${{window.encodeURI({key})}}");
            return $"`/{controller.SubDomain}/{ACTION_NAME}/{encoded.Join("/")}`";
        }

        internal string RenderController() {
            var appSrv = new ApplicationService();
            var dataClass = new SingleViewDataClass(_aggregate).Name;
            var keys = _aggregate
                .GetKeys()
                .OfType<AggregateMember.ValueMember>()
                .ToArray();

            var singleRelevants = new List<GraphEdge<Aggregate>>(); // この集約1件に対して0件または1件存在しうる隣接Ref
            var manyRelevants = new List<GraphEdge<Aggregate>>(); // この集約1件に対して複数件存在しうる隣接Ref（Load処理を利用してフェッチする）
            foreach (var agg in _aggregate.EnumerateThisAndDescendants()) {
                if (agg.IsRoot() || !agg.EnumerateAncestors().Any(ancestor => ancestor.Terminal.IsChildrenMember())) {
                    singleRelevants.AddRange(agg.GetReferedEdgesAsSingleKeyRecursively());
                } else {
                    manyRelevants.AddRange(agg.GetReferedEdgesAsSingleKeyRecursively());
                }
            }

            return $$"""
                [HttpGet("{{ACTION_NAME}}/{{keys.Select(m => "{" + m.MemberName + "}").Join("/")}}")]
                public virtual IActionResult Find({{keys.Select(m => $"{m.CSharpTypeName}? {m.MemberName}").Join(", ")}}) {
                {{keys.SelectTextTemplate(m => $$"""
                    if ({{m.MemberName}} == null) return BadRequest();
                """)}}

                    {{WithIndent(FindFeature.RenderDbEntityLoading(
                        _aggregate,
                        $"_applicationService.{appSrv.DbContext}",
                        "entity",
                        keys.Select(a => a.MemberName).ToArray(),
                        tracks: false,
                        includeRefs: true), "    ")}}

                    if (entity == null) {
                        return NotFound();
                    }
                    var instance = new {{dataClass}} {
                        {{WithIndent(AggregateDetail.RenderBodyOfFromDbEntity(
                            _aggregate,
                            _aggregate,
                            "entity",
                            0,
                            agg => $"new {new SingleViewDataClass(agg).Name}()"), "        ")}}
                    };
                {{singleRelevants.SelectTextTemplate((rel, ix) => $$"""

                    {{WithIndent(FindFeature.RenderDbEntityLoading(
                        rel.Initial.AsEntry(),
                        $"_applicationService.{appSrv.DbContext}",
                        $"entity{ix}",
                        keys.Select(a => a.MemberName).ToArray(),
                        tracks: false,
                        includeRefs: true), "    ")}}

                    if (entity{{ix}} != null) {
                        instance.{{rel.Initial.PathFromEntry().Select(p => p.RelationName).Join(".")}} = new {{new SingleViewDataClass(rel.Initial).Name}} {
                            {{WithIndent(AggregateDetail.RenderBodyOfFromDbEntity(
                                rel.Initial.AsEntry(),
                                rel.Initial.AsEntry(),
                                $"entity{ix}",
                                0,
                                agg => $"new {new SingleViewDataClass(agg).Name}()"), "        ")}}
                        };
                    }
                """)}}

                    return this.JsonContent(instance);
                }
                """;
        }
    }
}
