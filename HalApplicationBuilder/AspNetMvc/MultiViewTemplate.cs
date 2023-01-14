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
    
    
    public partial class MultiViewTemplate : MultiViewTemplateBase {
        
        public virtual string TransformText() {
            this.GenerationEnvironment = null;
            this.Write("\n@model ");
            this.Write(this.ToStringHelper.ToStringWithCulture(ModelTypeFullname));
            this.Write(";\n@{\n    ViewData[\"Title\"] = \"");
            this.Write(this.ToStringHelper.ToStringWithCulture(PageTitle));
            this.Write("\";\n}\n\n<div class=\"flex gap-3 items-center\">\n    <h1 class=\"font-bold text-[18px] " +
                    "select-none\">\n        ");
            this.Write(this.ToStringHelper.ToStringWithCulture(PageTitle));
            this.Write("\n    </h1>\n    <a asp-action=\"New\" class=\"halapp-btn-link\">新規作成</a>\n</div>\n\n<form" +
                    ">\n    @* 検索条件欄 *@\n    <div class=\"border mt-2 p-2\">\n        ");
            this.Write(this.ToStringHelper.ToStringWithCulture(SearchConditionView));
            this.Write("\n        <button asp-action=\"");
            this.Write(this.ToStringHelper.ToStringWithCulture(SearchActionName));
            this.Write("\" class=\"halapp-btn-primary\">検索</button>\n        <button asp-action=\"");
            this.Write(this.ToStringHelper.ToStringWithCulture(ClearActionName));
            this.Write(@""" class=""halapp-btn-secondary"">クリア</button>
    </div>
    
    @* 検索結果欄 *@
    <div class=""mt-2"">
        <div style=""display: flex; justify-content: flex-end"">
        </div>
        <table class=""table table-sm text-left w-full border"">
            <thead class=""border-b"">
                <tr>
                    <th></th>
");
 foreach (var prop in SearchResultClass.Properties) { 
            this.Write("                    <th>\n                        ");
            this.Write(this.ToStringHelper.ToStringWithCulture(prop.PropertyName));
            this.Write("\n                    </th>\n");
 } 
            this.Write("                </tr>\n            </thead>\n            <tbody>\n                @f" +
                    "or (int i = 0; i < Model.SearchResult.Count; i++)\n                {\n            " +
                    "        <tr>\n                        <td>\n                            <a asp-act" +
                    "ion=\"");
            this.Write(this.ToStringHelper.ToStringWithCulture(LinkToSingleViewActionName));
            this.Write("\"\n                               asp-route-id=\"");
            this.Write(this.ToStringHelper.ToStringWithCulture(BoundIdPropertyPathName));
            this.Write("\"\n                               class=\"halapp-btn-link\">\n                       " +
                    "         詳細\n                            </a>\n                        </td>\n");
 foreach (var prop in SearchResultClass.Properties) { 
            this.Write("                        <td>\n                            @Model.SearchResult[i].");
            this.Write(this.ToStringHelper.ToStringWithCulture(prop.PropertyName));
            this.Write("\n                        </td>\n");
 } 
            this.Write("                    </tr>\n                }\n            </tbody>\n        </table>" +
                    "\n    </div>\n</form>\n\n");
            return this.GenerationEnvironment.ToString();
        }
        
        public virtual void Initialize() {
        }
    }
    
    public class MultiViewTemplateBase {
        
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
