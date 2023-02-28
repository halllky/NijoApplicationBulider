﻿using System;
using System.Collections.Generic;
using System.Linq;
using HalApplicationBuilder.Core;
using HalApplicationBuilder.Core.DBModel;
using HalApplicationBuilder.Core.Members;
using HalApplicationBuilder.Core.UIModel;
using HalApplicationBuilder.DotnetEx;

namespace HalApplicationBuilder.EntityFramework {
    public class SearchMethodRenderer {

        public static string Render(IApplicationSchema applicationSchema, IDbSchema dbSchema, IViewModelProvider viewModelProvider, Config config) {
            var rootAggregates = applicationSchema.RootAggregates();
            var searchMethods = rootAggregates
                .Select(a => new Search(dbSchema.GetDbEntity(a), viewModelProvider.GetSearchConditionModel(a), viewModelProvider.GetSearchResultModel(a), config));
            var template = new SearchMethodTemplate {
                DbContextNamespace = config.DbContextNamespace,
                DbContextName = config.DbContextName,
                SearchMethods = searchMethods,
            };
            return template.TransformText();
        }


        public class Search {
            public Search(DbEntity dbEntity, SearchConditionClass searchConditionClass, SearchResultClass searchResultClass, Config config) {
                _dbEntity = dbEntity;
                _searchConditionClass = searchConditionClass;
                _searchResultClass = searchResultClass;
                _config = config;
            }
            private readonly DbEntity _dbEntity;
            private readonly SearchConditionClass _searchConditionClass;
            private readonly SearchResultClass _searchResultClass;
            private readonly Config _config;

            public string SearchResultClassName => $"{_config.MvcModelNamespace}.{_searchResultClass.ClassName}";
            public string SearchConditionClassName => $"{_config.MvcModelNamespace}.{_searchConditionClass.ClassName}";
            public string EntityTypeName => $"{_config.EntityNamespace}.{_dbEntity.ClassName}";
            public string DbSetName => _dbEntity.DbSetName;
            public string MethodName => $"Search_{_dbEntity.Source.Name}";
            public string Arg => "searchCondition";
            public string Query => "query";
            public string E => "e";

            public IEnumerable<string> GetSelectClause() {
                var list = new List<string>();
                var visitor = new SelectClauseRenderer(this, list);
                foreach (var member in _dbEntity.Source.Members) {
                    member.Accept(visitor);
                }
                foreach (var line in list) {
                    yield return line;
                }
            }

            public IEnumerable<string> GetWhereClause() {
                var list = new List<string>();
                var visitor = new WhereClauseRenderer(this, list);
                foreach (var member in _dbEntity.Source.Members) {
                    member.Accept(visitor);
                }
                foreach (var line in list) {
                    yield return line;
                }
            }

            private class SelectClauseRenderer : IMemberVisitor {
                public SelectClauseRenderer(Search method, IList<string> sourceCode) {
                    _method = method;
                    _sourceCode = sourceCode;
                }
                private readonly Search _method;
                private readonly IList<string> _sourceCode;

                public void Visit(SchalarValue member) {
                    _sourceCode.Add($"{member.SearchResultPropName} = {_method.E}.{member.SearchResultPropName},");
                }
                public void Visit(Child member) {
                    // TODO
                }
                public void Visit(Variation member) {
                    _sourceCode.Add($"{member.SearchResultPropName} = {_method.E}.{member.SearchResultPropName},");
                }
                public void Visit(Children member) {
                    // 何もしない
                }
                public void Visit(Reference member) {
                    // TODO
                }
            }

            private class WhereClauseRenderer : IMemberVisitor {
                public WhereClauseRenderer(Search method, IList<string> sourceCode) {
                    _method = method;
                    _sourceCode = sourceCode;
                }
                private readonly Search _method;
                private readonly IList<string> _sourceCode;

                public void Visit(SchalarValue member) {
                    var type = member.GetPropertyTypeExceptNullable();
                    if (member.IsRangeSearchCondition()) {
                        var valueFrom = $"{_method.Arg}.{member.Name}.{nameof(FromTo.From)}";
                        var valueTo = $"{_method.Arg}.{member.Name}.{nameof(FromTo.To)}";
                        _sourceCode.Add($"if ({valueFrom} != null) {{");
                        _sourceCode.Add($"    {_method.Query} = {_method.Query}.Where(e => e.{member.Name} >= {valueFrom});");
                        _sourceCode.Add($"}}");
                        _sourceCode.Add($"if ({valueTo} != null) {{");
                        _sourceCode.Add($"    {_method.Query} = {_method.Query}.Where(e => e.{member.Name} <= {valueTo});");
                        _sourceCode.Add($"}}");

                    } else if (type == typeof(string)) {
                        var value = $"{_method.Arg}.{member.Name}";
                        _sourceCode.Add($"if (!string.{nameof(string.IsNullOrWhiteSpace)}({value})) {{");
                        _sourceCode.Add($"    {_method.Query} = {_method.Query}.Where(e => e.{member.Name}.{nameof(string.Contains)}({value}));");
                        _sourceCode.Add($"}}");

                    } else {
                        var value = $"{_method.Arg}.{member.Name}";
                        _sourceCode.Add($"if ({value} != null) {{");
                        _sourceCode.Add($"    {_method.Query} = {_method.Query}.Where(e => e.{member.Name} == {value});");
                        _sourceCode.Add($"}}");
                    }
                }
                public void Visit(Variation member) {
                    // TODO
                }
                public void Visit(Reference member) {
                    // TODO
                }
                public void Visit(Child member) {
                    // TODO
                }
                public void Visit(Children member) {
                    // 何もしない
                }
            }
        }
    }

    partial class SearchMethodTemplate {
        public object DbContextNamespace { get; set; }
        public object DbContextName { get; set; }
        public IEnumerable<SearchMethodRenderer.Search> SearchMethods { get; set; }
    }
}