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

        internal static string RenderTypeScriptDataClassDeclaration(GraphNode<Aggregate> rootAggregate) {
            if (!rootAggregate.IsRoot()) throw new InvalidOperationException();

            return rootAggregate.EnumerateThisAndDescendants().SelectTextTemplate(aggregate => {
                var ownMembers = aggregate
                    .GetMembers()
                    .Where(m => m.DeclaringAggregate == aggregate);
                var refered = aggregate
                    .GetReferedEdgesAsSingleKey();

                return $$"""
                    export type {{new SingleViewDataClass(aggregate).Name}} = {
                    {{ownMembers.SelectTextTemplate(m => $$"""
                      {{m.MemberName}}?: {{m.TypeScriptTypename}}
                    """)}}
                    {{refered.SelectTextTemplate(edge => $$"""
                      {{edge.RelationName}}?: {{new SingleViewDataClass(edge.Initial).Name}}
                    """)}}
                      {{IS_LOADED}}?: boolean
                      {{OBJECT_ID}}?: string
                    }
                    """;
            });
        }
        internal static string RenderCSharpDataClassDeclaration(GraphNode<Aggregate> rootAggregate) {
            if (!rootAggregate.IsRoot()) throw new InvalidOperationException();

            return rootAggregate.EnumerateThisAndDescendants().SelectTextTemplate(aggregate => {
                var ownMembers = aggregate
                    .GetMembers()
                    .Where(m => m.DeclaringAggregate == aggregate);
                var refered = aggregate
                    .GetReferedEdgesAsSingleKey();

                return $$"""
                    /// <summary>
                    /// {{aggregate.Item.DisplayName}}の詳細画面のデータ構造です。
                    /// </summary>
                    public partial class {{new SingleViewDataClass(aggregate).Name}} {
                    {{ownMembers.SelectTextTemplate(m => $$"""
                    {{If(m is AggregateMember.Child, () => $$"""
                        public virtual {{new SingleViewDataClass(((AggregateMember.Child)m).MemberAggregate).Name}}? {{m.MemberName}} { get; set; }
                    """).ElseIf(m is AggregateMember.VariationItem, () => $$"""
                        public virtual {{new SingleViewDataClass(((AggregateMember.VariationItem)m).MemberAggregate).Name}}? {{m.MemberName}} { get; set; }
                    """).ElseIf(m is AggregateMember.Children, () => $$"""
                        public virtual List<{{new SingleViewDataClass(((AggregateMember.Children)m).MemberAggregate).Name}}>? {{m.MemberName}} { get; set; }
                    """).Else(() => $$"""
                        public virtual {{m.CSharpTypeName}}? {{m.MemberName}} { get; set; }
                    """)}}
                    """)}}
                    {{refered.SelectTextTemplate(edge => $$"""
                        public virtual {{new SingleViewDataClass(edge.Initial).Name}}? {{edge.RelationName}} { get; set; }
                    """)}}
                    }
                    """;
            });
        }
    }
}
