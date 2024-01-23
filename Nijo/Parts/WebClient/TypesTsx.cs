using Nijo.Core;
using Nijo.Parts.WebServer;
using Nijo.Util.CodeGenerating;
using Nijo.Util.DotnetEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nijo.Parts.WebClient {
    internal class TypesTsx {

        internal static SourceFile Render(CodeRenderingContext context, IEnumerable<KeyValuePair<GraphNode<Aggregate>, List<string>>> typeScriptDataTypes) => new SourceFile {
            FileName = "autogenerated-types.ts",
            RenderContent = () => $$"""
                import { UUID } from 'uuidjs'

                {{typeScriptDataTypes.SelectTextTemplate(item => $$"""
                // ------------------ {{item.Key.Item.DisplayName}} ------------------
                {{item.Value.SelectTextTemplate(source => $$"""
                {{source}}

                """)}}

                """)}}
                """,
        };
    }
}