using HalApplicationBuilder.DotnetEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HalApplicationBuilder.Core {
    internal class Aggregate : ValueObject, IEFCoreEntity {
        internal Aggregate(NodeId id, string displayName, bool hasNameMember, AggregateBuildOption options) {
            Id = id;
            DisplayName = displayName;
            HasNameMember = hasNameMember;
            Options = options;
        }

        public NodeId Id { get; }
        internal string DisplayName { get; }
        internal string UniqueId => new HashedString(Id.ToString()).Guid.ToString().Replace("-", "");

        public string ClassName => DisplayName.ToCSharpSafe();
        public string TypeScriptTypeName => DisplayName.ToCSharpSafe();
        public string EFCoreEntityClassName => $"{DisplayName.ToCSharpSafe()}DbEntity";
        string IEFCoreEntity.ClassName => EFCoreEntityClassName;
        public string DbSetName => EFCoreEntityClassName;

        public IList<IReadOnlyMemberOptions> SchalarMembersNotRelatedToAggregate { get; } = new List<IReadOnlyMemberOptions>();
        internal bool HasNameMember { get; }
        internal AggregateBuildOption Options { get; }

        protected override IEnumerable<object?> ValueObjectIdentifiers() {
            yield return Id;
        }

        public override string ToString() => $"Aggregate[{Id}]";
    }
}
