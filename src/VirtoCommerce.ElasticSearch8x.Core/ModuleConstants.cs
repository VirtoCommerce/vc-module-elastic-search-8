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
                Name = "VirtoCommerce.Search.ElasticSearch8x.IndexTotalFieldsLimit",
                GroupName = "Search|ElasticSearch8x",
                ValueType = SettingValueType.Integer,
                DefaultValue = 1000,
            };

            public static SettingDescriptor TokenFilter { get; } = new()
            {
                Name = "VirtoCommerce.Search.ElasticSearch8x.TokenFilter",
                GroupName = "Search|ElasticSearch8x",
                ValueType = SettingValueType.ShortText,
                DefaultValue = "custom_edge_ngram",
            };

            public static SettingDescriptor MinGram { get; } = new()
            {
                Name = "VirtoCommerce.Search.ElasticSearch8x.NGramTokenFilter.MinGram",
                GroupName = "Search|ElasticSearch8x",
                ValueType = SettingValueType.Integer,
                DefaultValue = 1,
            };

            public static SettingDescriptor MaxGram { get; } = new()
            {
                Name = "VirtoCommerce.Search.ElasticSearch8x.NGramTokenFilter.MaxGram",
                GroupName = "Search|ElasticSearch8x",
                ValueType = SettingValueType.Integer,
                DefaultValue = 20,
            };

            public static SettingDescriptor EnableCognitiveSearch { get; } = new()
            {
                Name = "VirtoCommerce.Search.ElasticSearch8x.EnableCognitiveSearch",
                GroupName = "Search|ElasticSearch8x",
                ValueType = SettingValueType.Boolean,
                DefaultValue = true,
            };

            public static SettingDescriptor CognitiveModelId { get; } = new()
            {
                Name = "VirtoCommerce.Search.ElasticSearch8x.ModelId",
                GroupName = "Search|ElasticSearch8x",
                ValueType = SettingValueType.ShortText,
                DefaultValue = ".elser_model_1",
            };

            public static SettingDescriptor CognitiveModelPiplelineName { get; } = new()
            {
                Name = "VirtoCommerce.Search.ElasticSearch8x.ModelPipelineName",
                GroupName = "Search|ElasticSearch8x",
                ValueType = SettingValueType.ShortText,
                DefaultValue = "elser-v1-pipeline",
            };

            public static SettingDescriptor CognitiveModelFieldName { get; } = new()
            {
                Name = "VirtoCommerce.Search.ElasticSearch8x.ModelFieldName",
                GroupName = "Search|ElasticSearch8x",
                ValueType = SettingValueType.ShortText,
                DefaultValue = "ml.tokens",
            };

            public static IEnumerable<SettingDescriptor> AllGeneralSettings
            {
                get
                {
                    yield return IndexTotalFieldsLimit;
                    yield return TokenFilter;
                    yield return MinGram;
                    yield return MaxGram;
                    yield return EnableCognitiveSearch;
                    yield return CognitiveModelId;
                    yield return CognitiveModelPiplelineName;
                    yield return CognitiveModelFieldName;
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
