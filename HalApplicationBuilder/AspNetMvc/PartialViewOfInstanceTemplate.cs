﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace HalApplicationBuilder.AspNetMvc {
    using System.Linq;
    using System.Text;
    using System.Collections.Generic;
    using System;
    
    
    public partial class PartialViewOfInstanceTemplate : PartialViewOfInstanceTemplateBase {
        
        public virtual string TransformText() {
            this.GenerationEnvironment = null;
            this.Write("\n<div class=\"flex flex-col\">\n\n");
 foreach (var member in Members) { 
            this.Write("    <div class=\"flex flex-col md:flex-row mb-1\">\n    \n");
     /* メンバー名 */ 
            this.Write("        <label class=\"w-32 select-none\">\n            ");
            this.Write(this.ToStringHelper.ToStringWithCulture(member.Key));
            this.Write("\n        </label>\n        <div class=\"flex-1\">\n        \n");
     /* SchalarValue */ 
     if (member.Value is InstanceTemplateSchalarValueData schalarValue) { 
            this.Write("            <input asp-for=\"");
            this.Write(this.ToStringHelper.ToStringWithCulture(schalarValue.AspForPath));
            this.Write("\" class=\"border\" />\n\n");
     /* SchalarValue検索条件 */ 
     } else if (member.Value is InstanceTemplateSchalarValueSearchConditionData schalarValueSC) { 
            this.Write("\n");
         /* ただのinput */ 
         if (schalarValueSC.Type == InstanceTemplateSchalarValueSearchConditionData.E_Type.Input) { 
            this.Write("            <input asp-for=\"");
            this.Write(this.ToStringHelper.ToStringWithCulture(schalarValueSC.AspFor[0]));
            this.Write("\" class=\"border\" />\n            \n");
         /* 範囲検索 */ 
         } else if (schalarValueSC.Type == InstanceTemplateSchalarValueSearchConditionData.E_Type.Range) { 
            this.Write("            <input asp-for=\"");
            this.Write(this.ToStringHelper.ToStringWithCulture(schalarValueSC.AspFor[0]));
            this.Write("\" class=\"border\" />\n            〜\n            <input asp-for=\"");
            this.Write(this.ToStringHelper.ToStringWithCulture(schalarValueSC.AspFor[1]));
            this.Write("\" class=\"border\" />\n            \n");
         /* ドロップダウン */ 
         } else if (schalarValueSC.Type == InstanceTemplateSchalarValueSearchConditionData.E_Type.Select) { 
            this.Write("            <select asp-for=\"");
            this.Write(this.ToStringHelper.ToStringWithCulture(schalarValueSC.AspFor[0]));
            this.Write("\"\n                    asp-items=\"@Html.GetEnumSelectList(typeof(");
            this.Write(this.ToStringHelper.ToStringWithCulture(schalarValueSC.EnumTypeName));
            this.Write("))\">\n");
             foreach (var opt in schalarValueSC.Options) { 
            this.Write("                <option selected=\"selected\" value=\"");
            this.Write(this.ToStringHelper.ToStringWithCulture(opt.Key));
            this.Write("\">\n                    ");
            this.Write(this.ToStringHelper.ToStringWithCulture(opt.Value));
            this.Write("\n                </option>\n");
             } 
            this.Write("            </select>\n");
         } 
            this.Write("        \n");
     /* Children */ 
     } else if (member.Value is InstanceTemplateChildrenData chlidren) { 
            this.Write("            @for (var ");
            this.Write(this.ToStringHelper.ToStringWithCulture(chlidren.i));
            this.Write(" = 0; ");
            this.Write(this.ToStringHelper.ToStringWithCulture(chlidren.i));
            this.Write(" < ");
            this.Write(this.ToStringHelper.ToStringWithCulture(chlidren.Count));
            this.Write("; ");
            this.Write(this.ToStringHelper.ToStringWithCulture(chlidren.i));
            this.Write("++) {\n                <partial name=\"");
            this.Write(this.ToStringHelper.ToStringWithCulture(chlidren.PartialViewName));
            this.Write("\" for=\"");
            this.Write(this.ToStringHelper.ToStringWithCulture(chlidren.PartialViewBoundObjectName));
            this.Write("\" />\n            }\n            <input\n                type=\"button\"\n             " +
                    "   value=\"追加\"\n                class=\"");
            this.Write(this.ToStringHelper.ToStringWithCulture(InstanceTemplateChildrenData.AddButtonCssClass));
            this.Write(" halapp-btn-secondary\"\n                ");
            this.Write(this.ToStringHelper.ToStringWithCulture(InstanceTemplateChildrenData.AddButtonSenderIdentifier));
            this.Write("=\"");
            this.Write(this.ToStringHelper.ToStringWithCulture(chlidren.AspForAddChild));
            this.Write("\"\n                ");
            this.Write(this.ToStringHelper.ToStringWithCulture(InstanceTemplateChildrenData.AddButtonModelIdentifier));
            this.Write("=\"");
            this.Write(this.ToStringHelper.ToStringWithCulture(chlidren.AddButtonBoundObjectName));
            this.Write("\" />\n                \n");
     /* Reference, Reference検索条件 */ 
     } else if (member.Value is InstanceTemplateReferencenData reference) { 
            this.Write("            <div>\n                <input type=\"hidden\" asp-for=\"");
            this.Write(this.ToStringHelper.ToStringWithCulture(reference.AspForKey));
            this.Write("\" />\n                <input asp-for=\"");
            this.Write(this.ToStringHelper.ToStringWithCulture(reference.AspForText));
            this.Write("\" class=\"border\" />\n            </div>\n\n");
     /* Child, Child検索条件 */ 
     } else if (member.Value is InstanceTemplateChildData child) { 
            this.Write("            ");
            this.Write(this.ToStringHelper.ToStringWithCulture(child.ChildView));
            this.Write("\n            \n");
     /* Variation */ 
     } else if (member.Value is IEnumerable<InstanceTemplateVariationData> variations) { 
         foreach (var variation in variations) { 
            this.Write("            <div>\n                <label>\n                    <input type=\"radio\"" +
                    " asp-for=\"");
            this.Write(this.ToStringHelper.ToStringWithCulture(variation.RadioButtonAspFor));
            this.Write("\" value=\"");
            this.Write(this.ToStringHelper.ToStringWithCulture(variation.Key));
            this.Write("\" />\n                    ");
            this.Write(this.ToStringHelper.ToStringWithCulture(variation.Name));
            this.Write("\n                </label>\n                ");
            this.Write(this.ToStringHelper.ToStringWithCulture(variation.ChildAggregateView));
            this.Write("\n            </div>\n");
         } 
            this.Write("            \n");
     /* Variation検索条件 */ 
     } else if (member.Value is IEnumerable<InstanceTemplateVariationSearchConditionData> variationSCs) { 
         foreach (var variation in variationSCs) { 
            this.Write("            <label>\n                <input type=\"checkbox\" asp-for=\"");
            this.Write(this.ToStringHelper.ToStringWithCulture(variation.AspFor));
            this.Write("\" />\n                ");
            this.Write(this.ToStringHelper.ToStringWithCulture(variation.PropertyName));
            this.Write("\n            </label>\n");
         } 
            this.Write(" \n");
     } 
            this.Write("\n        </div>\n    </div>\n");
 } 
            this.Write("\n</div>\n");
            return this.GenerationEnvironment.ToString();
        }
        
        public virtual void Initialize() {
        }
    }
    
    public class PartialViewOfInstanceTemplateBase {
        
        private global::System.Text.StringBuilder builder;
        
        private global::System.Collections.Generic.IDictionary<string, object> session;
        
        private global::System.CodeDom.Compiler.CompilerErrorCollection errors;
        
        private string currentIndent = string.Empty;
        
        private global::System.Collections.Generic.Stack<int> indents;
        
        private ToStringInstanceHelper _toStringHelper = new ToStringInstanceHelper();
        
        public virtual global::System.Collections.Generic.IDictionary<string, object> Session {
            get {
                return this.session;
            }
            set {
                this.session = value;
            }
        }
        
        public global::System.Text.StringBuilder GenerationEnvironment {
            get {
                if ((this.builder == null)) {
                    this.builder = new global::System.Text.StringBuilder();
                }
                return this.builder;
            }
            set {
                this.builder = value;
            }
        }
        
        protected global::System.CodeDom.Compiler.CompilerErrorCollection Errors {
            get {
                if ((this.errors == null)) {
                    this.errors = new global::System.CodeDom.Compiler.CompilerErrorCollection();
                }
                return this.errors;
            }
        }
        
        public string CurrentIndent {
            get {
                return this.currentIndent;
            }
        }
        
        private global::System.Collections.Generic.Stack<int> Indents {
            get {
                if ((this.indents == null)) {
                    this.indents = new global::System.Collections.Generic.Stack<int>();
                }
                return this.indents;
            }
        }
        
        public ToStringInstanceHelper ToStringHelper {
            get {
                return this._toStringHelper;
            }
        }
        
        public void Error(string message) {
            this.Errors.Add(new global::System.CodeDom.Compiler.CompilerError(null, -1, -1, null, message));
        }
        
        public void Warning(string message) {
            global::System.CodeDom.Compiler.CompilerError val = new global::System.CodeDom.Compiler.CompilerError(null, -1, -1, null, message);
            val.IsWarning = true;
            this.Errors.Add(val);
        }
        
        public string PopIndent() {
            if ((this.Indents.Count == 0)) {
                return string.Empty;
            }
            int lastPos = (this.currentIndent.Length - this.Indents.Pop());
            string last = this.currentIndent.Substring(lastPos);
            this.currentIndent = this.currentIndent.Substring(0, lastPos);
            return last;
        }
        
        public void PushIndent(string indent) {
            this.Indents.Push(indent.Length);
            this.currentIndent = (this.currentIndent + indent);
        }
        
        public void ClearIndent() {
            this.currentIndent = string.Empty;
            this.Indents.Clear();
        }
        
        public void Write(string textToAppend) {
            this.GenerationEnvironment.Append(textToAppend);
        }
        
        public void Write(string format, params object[] args) {
            this.GenerationEnvironment.AppendFormat(format, args);
        }
        
        public void WriteLine(string textToAppend) {
            this.GenerationEnvironment.Append(this.currentIndent);
            this.GenerationEnvironment.AppendLine(textToAppend);
        }
        
        public void WriteLine(string format, params object[] args) {
            this.GenerationEnvironment.Append(this.currentIndent);
            this.GenerationEnvironment.AppendFormat(format, args);
            this.GenerationEnvironment.AppendLine();
        }
        
        public class ToStringInstanceHelper {
            
            private global::System.IFormatProvider formatProvider = global::System.Globalization.CultureInfo.InvariantCulture;
            
            public global::System.IFormatProvider FormatProvider {
                get {
                    return this.formatProvider;
                }
                set {
                    if ((value != null)) {
                        this.formatProvider = value;
                    }
                }
            }
            
            public string ToStringWithCulture(object objectToConvert) {
                if ((objectToConvert == null)) {
                    throw new global::System.ArgumentNullException("objectToConvert");
                }
                global::System.Type type = objectToConvert.GetType();
                global::System.Type iConvertibleType = typeof(global::System.IConvertible);
                if (iConvertibleType.IsAssignableFrom(type)) {
                    return ((global::System.IConvertible)(objectToConvert)).ToString(this.formatProvider);
                }
                global::System.Reflection.MethodInfo methInfo = type.GetMethod("ToString", new global::System.Type[] {
                            iConvertibleType});
                if ((methInfo != null)) {
                    return ((string)(methInfo.Invoke(objectToConvert, new object[] {
                                this.formatProvider})));
                }
                return objectToConvert.ToString();
            }
        }
    }
}
