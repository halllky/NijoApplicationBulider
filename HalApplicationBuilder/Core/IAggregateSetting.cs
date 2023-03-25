using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace HalApplicationBuilder.Core {
    internal interface IAggregateSetting {
        internal static IAggregateSetting FromReflection(Config config, Type rootAggregateType) {
            return new ReflectionSetting(config, rootAggregateType);
        }
        internal static IEnumerable<IAggregateSetting> FromJson(Config config, string json) {
            var schema = JsonSerializer.Deserialize<Serialized.AppSchemaJson>(json);
            if (schema == null) throw new FormatException($"集約定義のJSONの形式が不正です。");
            return FromJson(config, schema);
        }
        private static IEnumerable<IAggregateSetting> FromJson(Config config, Serialized.AppSchemaJson schema) {
            if (schema.Aggregates == null) throw new FormatException($"集約定義のJSONの形式が不正です。");
            foreach (var rootAggregate in schema.Aggregates) {
                yield return new JsonHandler(config, rootAggregate, schema);
            }
        }

        string DisplayName { get; }
        IEnumerable<AggregateMember> GetMembers(Aggregate owner);

        private class ReflectionSetting : IAggregateSetting {
            public ReflectionSetting(Config config, Type aggregateType) {
                _config = config;
                _aggregateType = aggregateType;
            }
            private readonly Config _config;
            private readonly Type _aggregateType;

            public string DisplayName => _aggregateType.Name;

            public IEnumerable<AggregateMember> GetMembers(Aggregate owner) {
                foreach (var prop in _aggregateType.GetProperties(BindingFlags.Public | BindingFlags.Instance)) {
                    if (prop.GetCustomAttribute<NotMappedAttribute>() != null) continue;

                    var displayName = prop.Name;
                    var isPrimary = prop.GetCustomAttribute<KeyAttribute>() != null;

                    if (MemberImpl.SchalarValue.IsPrimitive(prop.PropertyType)) {
                        yield return new MemberImpl.SchalarValue(_config, displayName, isPrimary, owner, prop.PropertyType);

                    } else if (prop.PropertyType.IsGenericType
                        && prop.PropertyType.GetGenericTypeDefinition() == typeof(Child<>)) {

                        var childType = prop.PropertyType.GetGenericArguments()[0];
                        var variations = prop.GetCustomAttributes<VariationAttribute>();

                        if (!childType.IsAbstract && !variations.Any()) {
                            var childSetting = FromReflection(_config, childType);
                            yield return new MemberImpl.Child(_config, displayName, isPrimary, owner, childSetting);

                        } else if (childType.IsAbstract && variations.Any()) {
                            var variationSettings = variations.ToDictionary(v => v.Key, v => FromReflection(_config, v.Type));
                            yield return new MemberImpl.Variation(_config, displayName, isPrimary, owner, variationSettings);

                        } else {
                            throw new InvalidOperationException($"抽象型ならバリエーション必須、抽象型でないなら{nameof(VariationAttribute)}指定不可");
                        }
                    } else if (prop.PropertyType.IsGenericType
                        && prop.PropertyType.GetGenericTypeDefinition() == typeof(Children<>)) {
                        var childSetting = FromReflection(_config, prop.PropertyType.GetGenericArguments()[0]);
                        yield return new MemberImpl.Children(_config, displayName, isPrimary, owner, childSetting);

                    } else if (prop.PropertyType.IsGenericType
                        && prop.PropertyType.GetGenericTypeDefinition() == typeof(RefTo<>)) {
                        var getRefTarget = () => FromReflection(_config, prop.PropertyType.GetGenericArguments()[0]);
                        yield return new MemberImpl.Reference(_config, displayName, isPrimary, owner, getRefTarget);

                    } else {
                        throw new InvalidOperationException($"{DisplayName} の {prop.Name} の型 {prop.PropertyType.Name} は非対応");
                    }
                }
            }
        }

        private class JsonHandler : IAggregateSetting {
            public JsonHandler(Config config, Serialized.AggregateJson aggregate, Serialized.AppSchemaJson schema) {
                _config = config;
                _aggregate = aggregate;
                _schema = schema;
            }
            private readonly Config _config;
            private readonly Serialized.AggregateJson _aggregate;
            private readonly Serialized.AppSchemaJson _schema;

            public string DisplayName {
                get {
                    if (_aggregate.Name == null)
                        throw new FormatException($"nameが見つかりません。");
                    if (string.IsNullOrWhiteSpace(_aggregate.Name))
                        throw new FormatException($"nameが空です。");
                    return _aggregate.Name;
                }
            }

            private IAggregateSetting GetAggregateById(string id) {
                var found = FromJson(_config, _schema)
                    .Select(setting => new RootAggregate(_config, setting))
                    .SelectMany(root => root.GetDescendantsAndSelf())
                    .SingleOrDefault(aggregate => aggregate.GetUniquePath() == id);
                if (found == null)
                    throw new InvalidOperationException($"'{id}' の集約が見つかりません。");
                return found.Setting;
            }

            public IEnumerable<AggregateMember> GetMembers(Aggregate owner) {
                if (_aggregate.Members == null)
                    throw new FormatException($"membersが見つかりません。");

                foreach (var member in _aggregate.Members!) {
                    if (string.IsNullOrWhiteSpace(member.Name))
                        throw new FormatException($"nameが空です。");
                    if (string.IsNullOrWhiteSpace(member.Kind))
                        throw new FormatException($"kindが空です。");

                    var displayName = member.Name;
                    var isPrimary = member.IsPrimary == true;
                    var isNullable = member.IsNullable == true;

                    var schalarType = MemberImpl.SchalarValue.TryParseTypeName(member.Kind);
                    if (schalarType != null) {
                        yield return new MemberImpl.SchalarValue(_config, displayName, isPrimary, owner, schalarType, isNullable);

                    } else if (member.Kind == MemberImpl.Reference.JSON_KEY) {
                        if (string.IsNullOrWhiteSpace(member.RefTarget)) throw new FormatException($"refTargetが空です。");
                        var getRefTarget = () => GetAggregateById(member.RefTarget);
                        yield return new MemberImpl.Reference(_config, displayName, isPrimary, owner, getRefTarget);

                    } else if (member.Kind == MemberImpl.Child.JSON_KEY) {
                        if (member.Child == null) throw new FormatException($"childが空です。");
                        var child = new JsonHandler(_config, member.Child, _schema);
                        yield return new MemberImpl.Child(_config, displayName, isPrimary, owner, child);

                    } else if (member.Kind == MemberImpl.Children.JSON_KEY) {
                        if (member.Children == null) throw new FormatException($"childrenが空です。");
                        var children = new JsonHandler(_config, member.Children, _schema);
                        yield return new MemberImpl.Children(_config, displayName, isPrimary, owner, children);

                    } else {
                        throw new FormatException($"不正な種類です: {member.Kind}");
                    }
                }
            }
        }
    }
}
