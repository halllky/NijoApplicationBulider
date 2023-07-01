using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace HalApplicationBuilder.Runtime {
    public static class RuntimeSettings {

        /// <summary>
        /// 実行時クライアント側設定
        /// </summary>
        public class Client {
            [JsonPropertyName("server")]
            public string? ApServerUri { get; set; }
        }

        /// <summary>
        /// 実行時サーバー側設定。機密情報を含んでよい。
        /// 本番環境ではサーバー管理者のみ閲覧編集可能、デバッグ環境では画面から閲覧編集可能。
        /// </summary>
        public class Server {
            /// <summary>
            /// 現在接続中のDBの名前。 <see cref="DbProfiles"/> のいずれかのキーと一致
            /// </summary>
            [JsonPropertyName("currentDb")]
            public string? CurrentDb { get; set; }

            [JsonPropertyName("db")]
            public List<DbProfile> DbProfiles { get; set; } = new();
            public class DbProfile {
                [JsonPropertyName("name")]
                public string Name { get; set; } = string.Empty;
                [JsonPropertyName("connStr")]
                public string ConnStr { get; set; } = string.Empty;
            }

            public string GetActiveConnectionString() {
                if (string.IsNullOrWhiteSpace(CurrentDb))
                    throw new InvalidOperationException("接続文字列が未指定です。");

                var db = DbProfiles.FirstOrDefault(db => db.Name == CurrentDb);
                if (db == null) throw new InvalidOperationException($"接続文字列 '{CurrentDb}' は無効です。");

                return db.ConnStr;
            }
            public string ToJson() {
                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions {
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All),
                    WriteIndented = true,
                });
                json = json.Replace("\\u0022", "\\\""); // ダブルクォートを\u0022ではなく\"で出力したい

                return json;
            }

            public static Server GetDefault() => new Server {
                CurrentDb = "SQLITE",
                DbProfiles = new List<DbProfile> {
                    new DbProfile { Name = "SQLITE", ConnStr = @"Data Source=""bin/Debug/debug.sqlite3""" },
                },
            };
        }
    }
}