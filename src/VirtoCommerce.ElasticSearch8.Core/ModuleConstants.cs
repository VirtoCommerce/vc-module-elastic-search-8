using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Settings;

namespace VirtoCommerce.ElasticSearch8.Core;

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

    // Semantic/vector search section
    public const string NoModel = "Disabled";
    public const string ElserModel = "ELSER";
    public const string ThirdPartyModel = "ThirdParty";

    public const string ModelPropertyName = "__ml";
    public const string TokensFieldName = "tokens";
    public const string VectorFieldName = "predicted_value";

    public const string TokensPropertyName = $"{ModelPropertyName}.{TokensFieldName}";
    public const string VectorPropertyName = $"{ModelPropertyName}.{VectorFieldName}";

    public static class Settings
    {
        public static class General
        {


            public static SettingDescriptor IndexTotalFieldsLimit { get; } = new()
            {
                Name = "VirtoCommerce.Search.ElasticSearch8.IndexTotalFieldsLimit",
                GroupName = "Search|ElasticSearch8|General",
                ValueType = SettingValueType.Integer,
                DefaultValue = 1000,
            };

            public static SettingDescriptor TokenFilter { get; } = new()
            {
                Name = "VirtoCommerce.Search.ElasticSearch8.TokenFilter",
                GroupName = "Search|ElasticSearch8|General",
                ValueType = SettingValueType.ShortText,
                DefaultValue = "custom_edge_ngram",
            };

            public static SettingDescriptor MinGram { get; } = new()
            {
                Name = "VirtoCommerce.Search.ElasticSearch8.NGramTokenFilter.MinGram",
                GroupName = "Search|ElasticSearch8|General",
                ValueType = SettingValueType.Integer,
                DefaultValue = 1,
            };

            public static SettingDescriptor MaxGram { get; } = new()
            {
                Name = "VirtoCommerce.Search.ElasticSearch8.NGramTokenFilter.MaxGram",
                GroupName = "Search|ElasticSearch8|General",
                ValueType = SettingValueType.Integer,
                DefaultValue = 20,
            };


            public static SettingDescriptor SemanticSearchType { get; } = new()
            {
                Name = "VirtoCommerce.Search.ElasticSearch8.SemanticSearchType",
                ValueType = SettingValueType.ShortText,
                GroupName = "Search|ElasticSearch8|Semantic",
                DefaultValue = NoModel,
                AllowedValues = new object[] { NoModel, ElserModel, ThirdPartyModel },
            };

            public static SettingDescriptor SemanticModelId { get; } = new()
            {
                Name = "VirtoCommerce.Search.ElasticSearch8.SemanticModelId",
                GroupName = "Search|ElasticSearch8|Semantic",
                ValueType = SettingValueType.ShortText,
                DefaultValue = ".elser_model_1",
            };

            public static SettingDescriptor SemanticPipelineName { get; } = new()
            {
                Name = "VirtoCommerce.Search.ElasticSearch8.SemanticPipelineName",
                GroupName = "Search|ElasticSearch8|Semantic",
                ValueType = SettingValueType.ShortText,
                DefaultValue = "elser-v1-pipeline",
            };

            public static SettingDescriptor SemanticVectorModelDimensions { get; } = new()
            {
                Name = "VirtoCommerce.Search.ElasticSearch8.SemanticVectorModelDimensions",
                GroupName = "Search|ElasticSearch8|Semantic",
                ValueType = SettingValueType.PositiveInteger,
                DefaultValue = 384,
            };

            public static IEnumerable<SettingDescriptor> AllGeneralSettings
            {
                get
                {
                    yield return IndexTotalFieldsLimit;
                    yield return TokenFilter;
                    yield return MinGram;
                    yield return MaxGram;
                    yield return SemanticSearchType;
                    yield return SemanticModelId;
                    yield return SemanticPipelineName;
                    yield return SemanticVectorModelDimensions;
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
