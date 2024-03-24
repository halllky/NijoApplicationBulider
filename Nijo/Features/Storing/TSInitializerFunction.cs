using Nijo.Core;
using Nijo.Core.AggregateMemberTypes;
using Nijo.Util.DotnetEx;
using Nijo.Util.CodeGenerating;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nijo.Features.Storing {
    internal class TSInitializerFunction {
        internal TSInitializerFunction(GraphNode<Aggregate> instance) {
            _instance = instance;
        }
        private readonly GraphNode<Aggregate> _instance;

        internal string FunctionName => $"create{_instance.Item.TypeScriptTypeName}";

        internal string Render() {
            var children = _instance
                .GetMembers()
                .OfType<AggregateMember.Children>()
                .Select(member => new {
                    Key = member.MemberName,
                    Value = $"[]",
                });
            var child = _instance
                .GetMembers()
                .OfType<AggregateMember.Child>()
                .Select(member => new {
                    Key = member.MemberName,
                    Value = $"{new TSInitializerFunction(member.MemberAggregate).FunctionName}()",
                });
            var variation = _instance
                .GetMembers()
                .OfType<AggregateMember.VariationItem>()
                .Select(member => new {
                    Key = member.MemberName,
                    Value = $"{new TSInitializerFunction(member.MemberAggregate).FunctionName}()",
                });
            var variationSwitch = _instance
                .GetMembers()
                .OfType<AggregateMember.Variation>()
                .Select(member => new {
                    Key = member.MemberName,
                    Value = $"'{member.GetGroupItems().First().Key}'",
                });
            var uuid = new AggregateDetail(_instance)
                .GetOwnMembers()
                .OfType<AggregateMember.ValueMember>()
                .Where(member => member.Options.MemberType is Uuid)
                .Select(member => new {
                    Key = member.MemberName,
                    Value = "UUID.generate()",
                });

            var initializers = uuid
                .Concat(children)
                .Concat(child)
                .Concat(variation)
                .Concat(variationSwitch);

            return $$"""
                    export const {{FunctionName}} = (): {{_instance.Item.TypeScriptTypeName}} => ({
                    {{initializers.SelectTextTemplate(item => $$"""
                      {{item.Key}}: {{item.Value}},
                    """)}}
                      {{SingleViewDataClass.OBJECT_ID}}: UUID.generate(),
                    })
                    """;
        }
    }
}
