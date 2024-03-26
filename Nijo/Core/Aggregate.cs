using Nijo.Util.DotnetEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Nijo.Core.DirectedEdgeExtensions;

namespace Nijo.Core {
    public class Aggregate : ValueObject, IGraphNode {
        internal Aggregate(NodeId id, string displayName, bool useKeyInsteadOfName, AggregateBuildOption options) {
            Id = id;
            DisplayName = displayName;
            UseKeyInsteadOfName = useKeyInsteadOfName;
            Options = options;
        }

        public NodeId Id { get; }
        internal string DisplayName { get; }
        internal string UniqueId => Id.Value.ToHashedString();

        public string ClassName => DisplayName.ToCSharpSafe();
        public string TypeScriptTypeName => DisplayName.ToCSharpSafe();
        public string EFCoreEntityClassName => $"{DisplayName.ToCSharpSafe()}DbEntity";
        public string DbSetName => $"{ClassName}DbSet";

        internal bool UseKeyInsteadOfName { get; }
        internal AggregateBuildOption Options { get; }

        protected override IEnumerable<object?> ValueObjectIdentifiers() {
            yield return Id;
        }

        public override string ToString() => $"Aggregate[{Id}]";

        internal const string KEYEQUALS = "KeyEquals";
    }

    internal static class AggregateExtensions {

        internal static bool IsRoot(this GraphNode<Aggregate> graphNode) {
            return graphNode.GetRoot() == graphNode;
        }
        internal static GraphNode<Aggregate> GetRoot(this GraphNode<Aggregate> graphNode) {
            return graphNode.EnumerateAncestorsAndThis().First();
        }
        internal static GraphEdge<Aggregate>? GetParent(this GraphNode<Aggregate> graphNode) {
            return graphNode.EnumerateAncestors().LastOrDefault();
        }

        /// <summary>
        /// 祖先を列挙する。ルート要素が先。
        /// </summary>
        internal static IEnumerable<GraphNode<Aggregate>> EnumerateAncestorsAndThis(this GraphNode<Aggregate> graphNode) {
            foreach (var ancestor in graphNode.EnumerateAncestors()) {
                yield return ancestor.Initial;
            }
            yield return graphNode;
        }
        /// <summary>
        /// 祖先を列挙する。ルート要素が先。
        /// </summary>
        internal static IEnumerable<GraphEdge<Aggregate>> EnumerateAncestors(this GraphNode<Aggregate> graphNode) {
            var stack = new Stack<GraphEdge<Aggregate>>();
            GraphEdge<Aggregate>? edge = null;
            GraphNode<Aggregate>? node = graphNode;
            while (true) {
                edge = node.In
                    .SingleOrDefault(edge => edge.IsParentChild())?
                    .As<Aggregate>();

                if (edge == null) break;
                stack.Push(edge);

                node = edge.Initial.As<Aggregate>();
            }
            while (stack.Count > 0) {
                yield return stack.Pop();
            }
        }

        internal static IEnumerable<GraphNode<Aggregate>> EnumerateDescendants(this GraphNode<Aggregate> graphNode) {
            return graphNode.SelectNeighbors(node => node
                .Out
                .Where(edge => edge.IsParentChild())
                .Select(edge => edge.Terminal.As<Aggregate>()));
        }
        internal static IEnumerable<GraphNode<Aggregate>> EnumerateThisAndDescendants(this GraphNode<Aggregate> graphNode) {
            yield return graphNode;
            foreach (var desc in graphNode.EnumerateDescendants()) {
                yield return desc;
            }
        }

        internal static bool IsInTreeOf(this GraphNode<Aggregate> agg, GraphNode<Aggregate> target) {
            return target
                .GetRoot()
                .EnumerateThisAndDescendants()
                .Contains(agg);
        }

        internal static bool IsChildrenMember(this GraphNode<Aggregate> graphNode) {
            var parent = graphNode.GetParent();
            return parent != null
                && parent.Attributes.TryGetValue(REL_ATTR_MULTIPLE, out var isArray)
                && (bool)isArray;
        }
        internal static bool IsChildMember(this GraphNode<Aggregate> graphNode) {
            var parent = graphNode.GetParent();
            return parent != null
                && (!parent.Attributes.TryGetValue(REL_ATTR_MULTIPLE, out var isArray) || (bool)isArray == false)
                && (!parent.Attributes.TryGetValue(REL_ATTR_VARIATIONGROUPNAME, out var groupName) || (string)groupName == string.Empty);
        }
        internal static bool IsVariationMember(this GraphNode<Aggregate> graphNode) {
            var parent = graphNode.GetParent();
            return parent != null
                && (!parent.Attributes.TryGetValue(REL_ATTR_MULTIPLE, out var isArray) || (bool)isArray == false)
                && parent.Attributes.TryGetValue(REL_ATTR_VARIATIONGROUPNAME, out var groupName)
                && (string)groupName != string.Empty;
        }

        /// <summary>
        /// この集約がDBに保存されるものかどうかを返します。
        /// </summary>
        internal static bool IsStored(this GraphNode<Aggregate> aggregate) {
            var handler = aggregate.GetRoot().Item.Options.Handler;
            return handler == NijoCodeGenerator.Models.WriteModel.Key
                || handler == NijoCodeGenerator.Models.ReadModel.Key;
        }

        /// <summary>
        /// このReadModelが依存するWriteModel（スキーマ定義で依存があると指定されているもの）を列挙する
        /// </summary>
        internal static IEnumerable<GraphNode<Aggregate>> GetDependsOnMarkedWriteModels(this GraphNode<Aggregate> readModel) {
            if (readModel.Item.Options.Handler != NijoCodeGenerator.Models.ReadModel.Key)
                throw new InvalidOperationException($"{readModel.Item} is not a read model.");

            foreach (var edge in readModel.Out) {
                if (!edge.Attributes.TryGetValue(REL_ATTR_RELATION_TYPE, out var type)) continue;
                if ((string)type != REL_ATTRVALUE_DEPENDSON) continue;
                yield return edge.Terminal.As<Aggregate>();
            }
        }
        /// <summary>
        /// このWriteModelに依存するReadModel（スキーマ定義で依存があると指定されているもの）を列挙する
        /// </summary>
        internal static IEnumerable<GraphNode<Aggregate>> GetDependsOnMarkedReadModels(this GraphNode<Aggregate> writeModel) {
            if (writeModel.Item.Options.Handler != NijoCodeGenerator.Models.WriteModel.Key)
                throw new InvalidOperationException($"{writeModel.Item} is not a write model.");

            foreach (var edge in writeModel.In) {
                if (!edge.Attributes.TryGetValue(REL_ATTR_RELATION_TYPE, out var type)) continue;
                if ((string)type != REL_ATTRVALUE_DEPENDSON) continue;
                yield return edge.Initial.As<Aggregate>();
            }
        }

        /// <summary>
        /// この集約が参照する集約、およびその参照先の祖先を列挙する
        /// </summary>
        internal static IEnumerable<GraphNode<Aggregate>> GetRefsAndTheirAncestorsRecursively(this GraphNode<Aggregate> aggregate) {
            IEnumerable<GraphNode<Aggregate>> Enumerate(GraphNode<Aggregate> agg) {
                foreach (var member in agg.GetMembers()) {
                    if (member is AggregateMember.Ref refMember) {
                        yield return refMember.MemberAggregate;
                        foreach (var item in Enumerate(refMember.MemberAggregate)) {
                            yield return item;
                        }
                    } else if (member is AggregateMember.Parent parent && !agg.IsInTreeOf(aggregate)) {
                        yield return parent.MemberAggregate;
                        foreach (var item in Enumerate(parent.MemberAggregate)) {
                            yield return item;
                        }
                    }
                }
            }
            return Enumerate(aggregate);
        }

        /// <summary>
        /// この集約に対するRefを持っている集約を列挙する。
        /// </summary>
        internal static IEnumerable<GraphEdge<Aggregate>> GetReferedEdges(this GraphNode<Aggregate> graphNode) {
            return graphNode.In
                .Where(edge => edge.Attributes.TryGetValue(REL_ATTR_RELATION_TYPE, out var type)
                            && (string)type == REL_ATTRVALUE_REFERENCE
                            && edge.Initial.Item is Aggregate)
                .Select(edge => edge.As<Aggregate>());
        }

        /// <summary>
        /// targetがsourceの唯一のキーであるか否か
        /// </summary>
        /// <param name="refTo">参照先</param>
        /// <param name="refFrom">参照元</param>
        internal static bool IsSingleRefKeyOf(this GraphNode<Aggregate> refTo, GraphNode<Aggregate> refFrom) {
            var keys = refFrom
                .GetKeys()
                .Where(key => key.DeclaringAggregate == refFrom)
                .ToArray();

            if (refFrom.Item.Options.Handler == NijoCodeGenerator.Models.WriteModel.Key
                && keys.Length == 1
                && keys[0] is AggregateMember.Ref rm
                && rm.MemberAggregate == refTo) {
                return true;
            }
            return false;
        }
        /// <summary>
        /// この集約を参照し、かつそれが参照元集約の唯一のキーであるものを列挙する。
        /// </summary>
        internal static IEnumerable<GraphEdge<Aggregate>> GetReferedEdgesAsSingleKey(this GraphNode<Aggregate> target) {
            return target
                .GetReferedEdges()
                .Where(edge => target.IsSingleRefKeyOf(edge.Initial));
        }
        /// <summary>
        /// この集約を参照し、かつそれが参照元集約の唯一のキーであるものを列挙する。
        /// 参照が2連鎖以上続く場合のために再帰処理。
        /// </summary>
        internal static IEnumerable<GraphEdge<Aggregate>> GetReferedEdgesAsSingleKeyRecursively(this GraphNode<Aggregate> target) {
            foreach (var ref1 in target.GetReferedEdgesAsSingleKey()) {
                yield return ref1;

                foreach (var ref2 in ref1.Initial.GetReferedEdgesAsSingleKeyRecursively()) {
                    yield return ref2;
                }
            }
        }

        /// <summary>
        /// この集約のすべてのメンバーが2次元の表で表現できるかどうかを返します。
        /// </summary>
        internal static bool CanDisplayAllMembersAs2DGrid(this GraphNode<Aggregate> aggregate) {
            return aggregate
                .EnumerateDescendants()
                .All(agg => !agg.IsChildrenMember()
                         && !agg.IsVariationMember());
        }
    }
}
