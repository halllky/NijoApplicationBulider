﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace HalApplicationBuilder.CodeRendering.Util {
    using System.Linq;
    using System.Text;
    using System.Collections.Generic;
    using System;
    
    
    public partial class RuntimeSettings : RuntimeSettingsBase {
        
        public virtual string TransformText() {
            this.GenerationEnvironment = null;
            this.Write("using System;\r\nusing System.Collections.Generic;\r\nusing System.Linq;\r\nusing Syste" +
                    "m.Text;\r\nusing System.Text.Json;\r\nusing System.Text.Json.Serialization;\r\n\r\nnames" +
                    "pace ");
            this.Write(this.ToStringHelper.ToStringWithCulture(_ctx.Config.RootNamespace));
            this.Write(@" {
    public static class RuntimeSettings {

        /// <summary>
        /// 実行時クライアント側設定
        /// </summary>
        public class Client {
            [JsonPropertyName(""server"")]
            public string? ApServerUri { get; set; }
        }

        /// <summary>
        /// 実行時サーバー側設定。機密情報を含んでよい。
        /// 本番環境ではサーバー管理者のみ閲覧編集可能、デバッグ環境では画面から閲覧編集可能。
        /// </summary>
        public class ");
            this.Write(this.ToStringHelper.ToStringWithCulture(SERVER));
            this.Write(@" {
            /// <summary>
            /// 現在接続中のDBの名前。 <see cref=""DbProfiles""/> のいずれかのキーと一致
            /// </summary>
            [JsonPropertyName(""currentDb"")]
            public string? CurrentDb { get; set; }

            [JsonPropertyName(""db"")]
            public List<DbProfile> DbProfiles { get; set; } = new();
            public class DbProfile {
                [JsonPropertyName(""name"")]
                public string Name { get; set; } = string.Empty;
                [JsonPropertyName(""connStr"")]
                public string ConnStr { get; set; } = string.Empty;
            }

            public string ");
            this.Write(this.ToStringHelper.ToStringWithCulture(GET_ACTIVE_CONNSTR));
            this.Write(@"() {
                if (string.IsNullOrWhiteSpace(CurrentDb))
                    throw new InvalidOperationException(""接続文字列が未指定です。"");

                var db = DbProfiles.FirstOrDefault(db => db.Name == CurrentDb);
                if (db == null) throw new InvalidOperationException($""接続文字列 '{CurrentDb}' は無効です。"");

                return db.ConnStr;
            }
            public string ");
            this.Write(this.ToStringHelper.ToStringWithCulture(TO_JSON));
            this.Write(@"() {
                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions {
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All),
                    WriteIndented = true,
                });
                json = json.Replace(""\\u0022"", ""\\\""""); // ダブルクォートを\u0022ではなく\""で出力したい

                return json;
            }

            public static ");
            this.Write(this.ToStringHelper.ToStringWithCulture(SERVER));
            this.Write(" ");
            this.Write(this.ToStringHelper.ToStringWithCulture(GET_DEFAULT));
            this.Write(@"() {
                var connStr = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder();
                connStr.DataSource = ""bin/Debug/debug.sqlite3"";
                connStr.Pooling = false; // デバッグ終了時にshm, walファイルが残らないようにするため

                return new ");
            this.Write(this.ToStringHelper.ToStringWithCulture(SERVER));
            this.Write(@" {
                    CurrentDb = ""SQLITE"",
                    DbProfiles = new List<DbProfile> {
                        new DbProfile { Name = ""SQLITE"", ConnStr = connStr.ToString() },
                    },
                };
            }
        }
    }
}
");
            return this.GenerationEnvironment.ToString();
        }
        
        public virtual void Initialize() {
        }
    }
    
    public class RuntimeSettingsBase {
        
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