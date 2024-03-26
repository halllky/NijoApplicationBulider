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
    /// 登録更新の最初で対象の集約をDBからロードする処理
    /// </summary>
    internal class FindFeature {
        internal FindFeature(GraphNode<Aggregate> aggregate) {
            _aggregate = aggregate;
        }

        private readonly GraphNode<Aggregate> _aggregate;

        internal string FindMethodReturnType => _aggregate.Item.ClassName;
        internal string FindMethodName => $"Find{_aggregate.Item.DisplayName.ToCSharpSafe()}";

        internal string RenderAppSrvMethod() {
            var appSrv = new ApplicationService();
            var args = GetEFCoreMethodArgs().ToArray();

            return $$"""
                /// <summary>
                /// {{_aggregate.Item.DisplayName}}のキー情報から対象データの詳細を検索して返します。
                /// </summary>
                public virtual {{FindMethodReturnType}}? {{FindMethodName}}({{args.Select(m => $"{m.CSharpTypeName}? {m.MemberName}").Join(", ")}}) {

                    {{WithIndent(RenderDbEntityLoading(
                        _aggregate,
                        appSrv.DbContext,
                        "entity",
                        args.Select(a => a.MemberName).ToArray(),
                        tracks: false,
                        includeRefs: true), "    ")}}

                    if (entity == null) return null;

                    var aggregateInstance = {{_aggregate.Item.ClassName}}.{{AggregateDetail.FROM_DBENTITY}}(entity);
                    return aggregateInstance;
                }
                """;
        }

        private IEnumerable<AggregateMember.AggregateMemberBase> GetEFCoreMethodArgs() {
            return _aggregate
                .GetKeys()
                .Where(m => m is AggregateMember.ValueMember);
        }

        /// <summary>
        /// TODO: この操作は随所に出てくるので DbManipulation.cs などそれ用のクラスを設けてそちらに移したほうがよいかもしれない
        /// </summary>
        internal static string RenderDbEntityLoading(
            GraphNode<Aggregate> rootAggregate,
            string dbContextVarName,
            string entityVarName,
            IList<string> searchKeys,
            bool tracks,
            bool includeRefs,
            bool single = true) {

            // Include
            var includeEntities = rootAggregate
                .EnumerateThisAndDescendants()
                .ToList();
            if (includeRefs) {
                var refEntities = rootAggregate
                    .EnumerateThisAndDescendants()
                    .SelectMany(agg => agg.GetMembers())
                    .Select(m => m.DeclaringAggregate);
                foreach (var entity in refEntities) {
                    includeEntities.Add(entity);
                }
            }
            var paths = includeEntities
                .Select(entity => entity.PathFromEntry())
                .Distinct()
                .SelectMany(edge => edge)
                .Select(edge => edge.As<Aggregate>())
                .Select(edge => {
                    var source = edge.Source.As<Aggregate>();
                    var nav = new NavigationProperty(edge);
                    var prop = edge.Source.As<Aggregate>() == nav.Principal.Owner
                        ? nav.Principal.PropertyName
                        : nav.Relevant.PropertyName;
                    return new { source, prop };
                });

            // SingleOrDefaultならキーの数は固定、Whereならキーの数は少なくても可
            var keys = rootAggregate
                .GetKeys()
                .Where(m => m is AggregateMember.ValueMember)
                .ToArray();
            var whereClause = single
                ? keys.SelectTextTemplate((m, i) => $"x.{m.GetFullPath().Join(".")} == {searchKeys.ElementAtOrDefault(i)}")
                : searchKeys.SelectTextTemplate((searchKey, i) => $"x.{keys[i].GetFullPath().Join(".")} == {searchKey}");

            return $$"""
                var {{entityVarName}} = {{dbContextVarName}}.{{rootAggregate.Item.DbSetName}}
                {{If(tracks == false, () => $$"""
                    .AsNoTracking()
                """)}}
                {{paths.SelectTextTemplate(path => path.source == rootAggregate ? $$"""
                    .Include(x => x.{{path.prop}})
                """ : $$"""
                    .ThenInclude(x => x.{{path.prop}})
                """)}}
                {{If(single, () => $$"""
                    .SingleOrDefault(x => {{WithIndent(whereClause, "                       && ")}});
                """).Else(() => $$"""
                    .Where(x => {{WithIndent(whereClause, "             && ")}})
                    .AsEnumerable();
                """)}}
                """;
        }
    }
}
