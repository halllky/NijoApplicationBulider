using HalApplicationBuilder.Core;
using HalApplicationBuilder.DotnetEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HalApplicationBuilder.CodeRendering.InstanceHandling {
    internal class RefTargetKeyName {
        internal RefTargetKeyName(GraphNode<Aggregate> aggregate) {
            _aggregate = aggregate;
        }
        private readonly GraphNode<Aggregate> _aggregate;

        internal string CSharpClassName => $"{_aggregate.Item.ClassName}KeysAndNames";
        internal string TypeScriptTypeName => $"{_aggregate.Item.ClassName}KeysAndNames";

        internal IEnumerable<AggregateMember.AggregateMemberBase> GetKeysAndNames() {
            return GetKeys().Union(GetNames());
        }
        internal IEnumerable<AggregateMember.AggregateMemberBase> GetKeys() {
            return _aggregate
                .GetKeys()
                .Where(m => _aggregate.EnumerateAncestorsAndThis().Contains(m.Declaring));
        }
        internal IEnumerable<AggregateMember.AggregateMemberBase> GetNames() {
            return _aggregate
                .GetMembers()
                .OfType<AggregateMember.ValueMember>()
                .Where(member => member.IsDisplayName);
        }

        internal string RenderCSharpDeclaring() {
            static List<string> GetAnnotations(AggregateMember.AggregateMemberBase m) {
                var list = new List<string>();
                if (m is AggregateMember.ValueMember v && v.IsKey) list.Add("Key");
                if (m is AggregateMember.Ref r && r.Relation.IsPrimary()) list.Add("Key");
                if (m is AggregateMember.ValueMember v2 && v2.IsDisplayName) list.Add("DisplayName");
                return list;
            }
            return $$"""
                public class {{CSharpClassName}} {
                {{GetKeysAndNames().SelectTextTemplate(member => $$"""
                    [{{GetAnnotations(member).Join(", ")}}]
                    public {{member.CSharpTypeName}} {{member.MemberName}} { get; set; }
                """)}}
                }
                """;
        }
        internal string RenderTypeScriptDeclaring() {
            return $$"""
                export type {{TypeScriptTypeName}} = {
                {{GetKeysAndNames().SelectTextTemplate(member => $$"""
                  {{member.MemberName}}: {{member.CSharpTypeName}}
                """)}}
                }
                """;
        }
    }
}
