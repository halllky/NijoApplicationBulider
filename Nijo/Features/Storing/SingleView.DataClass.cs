using Nijo.Core;
using Nijo.Util.CodeGenerating;
using Nijo.Util.DotnetEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nijo.Features.Storing {
    internal class SingleViewDataClass {
        internal SingleViewDataClass(GraphNode<Aggregate> aggregate) {
            _aggregate = aggregate;
        }
        private readonly GraphNode<Aggregate> _aggregate;

        internal string Name => $"{_aggregate.Item.TypeScriptTypeName}SingleViewData";

        /// <summary>
        /// 編集画面でDBから読み込んだデータとその画面中で新たに作成されたデータで
        /// 挙動を分けるためのフラグ
        /// </summary>
        internal const string IS_LOADED = "__loaded";
        /// <summary>
        /// - useFieldArrayの中で配列インデックスをキーに使うと新規追加されたコンボボックスが
        ///   その1個上の要素の更新と紐づいてしまうのでクライアント側で要素1個ずつにIDを振る
        /// - TabGroupでどのタブがアクティブになっているかの判定にも使う
        /// </summary>
        internal const string OBJECT_ID = "__object_id";

        internal string RootPropName => _aggregate.Item.ClassName;

        internal IEnumerable<Prop> GetProps() {
            return _aggregate
                .EnumerateThisAndDescendants()
                .SelectMany(agg => agg.GetReferedEdgesAsSingleKeyRecursively())
                // TODO: 本当はDistinctを使いたいがAggregateの同一性判断にSourceが入っていない
                .GroupBy(relation => new { agg = relation.Initial.GetRoot(), relation })
                .Select(group => new Prop(_aggregate, group.Key.agg));
        }

        internal string RenderTypeScriptDataClassDeclaration() {
            return $$"""
                type {{Name}} = {
                  {{RootPropName}}?: {{_aggregate.Item.TypeScriptTypeName}}
                {{GetProps().SelectTextTemplate(p => $$"""
                  {{p.PropName}}?: {{(p.IsArray ? $"{p.RefTarget.Item.TypeScriptTypeName}[]" : p.RefTarget.Item.TypeScriptTypeName)}}
                """)}}
                }
                """;
        }
        internal string RenderCSharpDataClassDeclaration() {
            return $$"""
                public partial class {{Name}} {
                    public {{_aggregate.Item.ClassName}}? {{RootPropName}} { get; set; }
                {{GetProps().SelectTextTemplate(p => $$"""
                    public {{(p.IsArray ? $"List<{p.RefTarget.Item.ClassName}>?" : $"{p.RefTarget.Item.ClassName}?")}} {{p.PropName}} { get; set; }
                """)}}
                }
                """;
        }

        internal class Prop {
            internal Prop(GraphNode<Aggregate> root, GraphNode<Aggregate> refTarget) {
                _root = root;
                RefTarget = refTarget;
            }
            private readonly GraphNode<Aggregate> _root;
            internal GraphNode<Aggregate> RefTarget { get; }

            /// <summary>
            /// 従属集約が保管されるプロパティの名前を返します
            /// </summary>
            internal string PropName {
                get {
                    if (RefTarget.Source == null || RefTarget.Source.IsParentChild()) {
                        return RefTarget.Item.ClassName;

                    } else {
                        return $"{RefTarget.Source.RelationName.ToCSharpSafe()}_{RefTarget.Item.ClassName}";
                    }
                }
            }
            /// <summary>
            /// 主たる集約またはそれと1対1の多重度にある集約であればfalse
            /// </summary>
            internal bool IsArray {
                get {
                    foreach (var edge in RefTarget.PathFromEntry()) {
                        var initial = edge.Initial.As<Aggregate>();
                        var terminal = edge.Terminal.As<Aggregate>();

                        // 経路の途中にChildrenが含まれるならば多重度:多
                        if (terminal.IsChildrenMember()
                            && terminal.GetParent() == edge.As<Aggregate>()) {
                            return true;
                        }

                        // 経路の途中に主キーでないRefが含まれるならば多重度:多
                        if (edge.IsRef()
                            && !terminal.IsSingleRefKeyOf(initial)) {
                            return true;
                        }
                    }

                    return false;
                }
            }
        }
    }
}
