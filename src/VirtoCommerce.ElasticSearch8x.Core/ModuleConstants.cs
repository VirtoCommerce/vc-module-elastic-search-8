using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Settings;

namespace VirtoCommerce.ElasticSearch8x.Core;

public static class ModuleConstants
{
    public const string ElasticSearchExceptionTitle = "Elasticsearch8 Server";

    public const string ScoreFieldName = "score";

    public const string ActiveIndexAlias = "active";
    public const string BackupIndexAlias = "backup";
    public const string SearchableFieldAnalyzerName = "searchable_field_analyzer";
    public const string NGramFilterName = "custom_ngram";
    public const string EdgeNGramFilterName = "custom_edge_ngram";
    public const string CompletionSubFieldName = "completion";
    public const int SuggestionFieldLength = 256;

    public static class Settings
    {
        public static class General
        {
            public static SettingDescriptor IndexTotalFieldsLimit { get; } = new()
            {
                Name = "VirtoCommerce.Search.ElasticSearch.IndexTotalFieldsLimit",
                GroupName = "Search|ElasticSearch",
                ValueType = SettingValueType.Integer,
                DefaultValue = 1000,
            };

            public static SettingDescriptor TokenFilter { get; } = new()
            {
                Name = "VirtoCommerce.Search.ElasticSearch.TokenFilter",
                GroupName = "Search|ElasticSearch",
                ValueType = SettingValueType.ShortText,
                DefaultValue = "custom_edge_ngram",
            };

            public static SettingDescriptor MinGram { get; } = new()
            {
                Name = "VirtoCommerce.Search.ElasticSearch.NGramTokenFilter.MinGram",
                GroupName = "Search|ElasticSearch",
                ValueType = SettingValueType.Integer,
                DefaultValue = 1,
            };

            public static SettingDescriptor MaxGram { get; } = new()
            {
                Name = "VirtoCommerce.Search.ElasticSearch.NGramTokenFilter.MaxGram",
                GroupName = "Search|ElasticSearch",
                ValueType = SettingValueType.Integer,
                DefaultValue = 20,
            };

            public static IEnumerable<SettingDescriptor> AllGeneralSettings
            {
                get
                {
                    yield return IndexTotalFieldsLimit;
                    yield return TokenFilter;
                    yield return MinGram;
                    yield return MaxGram;
                }
            }
        }

        public static IEnumerable<SettingDescriptor> AllSettings
        {
            get
            {
                return General.AllGeneralSettings;
            }
        }
    }
}
