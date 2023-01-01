﻿using System;
using System.Collections.Generic;

namespace HalApplicationBuilder.AspNetMvc {
    public class MultiView {
        internal Core.Aggregate RootAggregate { get; init; }

        internal string FileName => $"{RootAggregate.Name}__MultiView.cshtml";

        internal string TransformText() {
            var searchConditionClass = RootAggregate.ToSearchConditionModel(new Core.ViewRenderingContext("Model", nameof(Model<object, object>.SearchCondition)));
            var searchResultClass = RootAggregate.ToSearchResultModel(new Core.ViewRenderingContext("Model", nameof(Model<object, object>.SearchResult)));
            var template = new MultiViewTemplate {
                ModelTypeFullname = $"{GetType().FullName}.{nameof(Model<object, object>)}<{searchConditionClass.RuntimeFullName}, {searchResultClass.RuntimeFullName}>",
                PageTitle = RootAggregate.Name,
                SearchConditionClass = searchConditionClass,
                SearchResultClass = searchResultClass,
                ClearActionName = "Clear",
                SearchActionName = "Search",
                LinkToSingleViewActionName = "Detail",
            };
            return template.TransformText();
        }

        public class Model<TSearchCondition, TSearchResult> {
            public TSearchCondition SearchCondition { get; set; }
            public List<TSearchResult> SearchResult { get; set; } = new();
        }
    }

    partial class MultiViewTemplate {
        internal string ModelTypeFullname { get; set; }
        internal string PageTitle { get; set; }
        internal Core.AutoGenerateMvcModelClass SearchConditionClass { get; set; }
        internal Core.AutoGenerateMvcModelClass SearchResultClass { get; set; }
        internal string ClearActionName { get; set; }
        internal string SearchActionName { get; set; }
        internal string LinkToSingleViewActionName { get; set; }
    }
}