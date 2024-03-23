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

        internal static bool IsStored(this GraphNode<Aggregate> aggregate) {
            var handler = aggregate.GetRoot().Item.Options.Handler;
            return handler == NijoCodeGenerator.Models.WriteModel.Key
                || handler == NijoCodeGenerator.Models.ReadModel.Key;
        }

        /// <summary>
        /// このReadModelが依存する集約を列挙する
        /// </summary>
        internal static IEnumerable<GraphNode<Aggregate>> GetDependency(this GraphNode<Aggregate> readModel) {
            if (readModel.Item.Options.Handler != NijoCodeGenerator.Models.ReadModel.Key)
                throw new InvalidOperationException($"{readModel.Item} is not a read model.");

            foreach (var edge in readModel.Out) {
                if (!edge.Attributes.TryGetValue(REL_ATTR_RELATION_TYPE, out var type)) continue;
                if ((string)type != REL_ATTRVALUE_DEPENDSON) continue;
                yield return edge.Terminal.As<Aggregate>();
            }
        }
        /// <summary>
        /// このWriteModelに依存する集約を列挙する
        /// </summary>
        internal static IEnumerable<GraphNode<Aggregate>> GetDependents(this GraphNode<Aggregate> writeModel) {
            if (writeModel.Item.Options.Handler != NijoCodeGenerator.Models.WriteModel.Key)
                throw new InvalidOperationException($"{writeModel.Item} is not a write model.");

            foreach (var edge in writeModel.In) {
                if (!edge.Attributes.TryGetValue(REL_ATTR_RELATION_TYPE, out var type)) continue;
                if ((string)type != REL_ATTRVALUE_DEPENDSON) continue;
                yield return edge.Initial.As<Aggregate>();
            }
        }

        internal static IEnumerable<GraphEdge<Aggregate>> GetReferedEdges(this GraphNode<Aggregate> graphNode) {
            return graphNode.In
                .Where(edge => edge.Attributes.TryGetValue(REL_ATTR_RELATION_TYPE, out var type)
                            && (string)type == REL_ATTRVALUE_REFERENCE
                            && edge.Initial.Item is Aggregate)
                .Select(edge => edge.As<Aggregate>());
        }
        /// <summary>
        /// この集約を参照し、かつそれが参照元集約の唯一のキーであるものを列挙する
        /// </summary>
        internal static IEnumerable<GraphEdge<Aggregate>> GetReferedEdgesAsSingleKey(this GraphNode<Aggregate> target) {
            var refered = target.GetReferedEdges();
            foreach (var edge in refered) {
                var source = edge.Initial;
                var keys = source
                    .GetKeys()
                    .Where(key => key.DeclaringAggregate == source)
                    .ToArray();

                if (keys.Length == 1
                    && keys[0] is AggregateMember.Ref rm
                    && rm.MemberAggregate == target) {
                    yield return edge;
                }
            }
        }
    }
}
