using VirtoCommerce.ElasticSearch8.Core;
using VirtoCommerce.Platform.Core.Settings;
using ModuleSettings = VirtoCommerce.ElasticSearch8.Core.ModuleConstants.Settings.General;

namespace VirtoCommerce.ElasticSearch8.Data.Extensions
{
    public static class SettingsExtensions
    {
        public static int GetFieldsLimit(this ISettingsManager settingsManager)
        {
            return settingsManager.GetValueByDescriptor<int>(ModuleSettings.IndexTotalFieldsLimit);
        }

        public static string GetTokenFilterName(this ISettingsManager settingsManager)
        {
            return settingsManager.GetValueByDescriptor<string>(ModuleSettings.TokenFilter);
        }

        public static int GetMinGram(this ISettingsManager settingsManager)
        {
            return settingsManager.GetValueByDescriptor<int>(ModuleSettings.MinGram);
        }

        public static int GetMaxGram(this ISettingsManager settingsManager)
        {
            return settingsManager.GetValueByDescriptor<int>(ModuleSettings.MaxGram);
        }

        public static string GetSemanticSearchType(this ISettingsManager settingsManager)
        {
            return settingsManager.GetValueByDescriptor<string>(ModuleSettings.SemanticSearchType);
        }

        public static bool GetSemanticSearchEnabled(this ISettingsManager settingsManager)
        {
            var semanticSearchType = settingsManager.GetSemanticSearchType();
            return semanticSearchType != ModuleConstants.NoModel;
        }

        public static string GetPipelineName(this ISettingsManager settingsManager)
        {
            return settingsManager.GetValueByDescriptor<string>(ModuleSettings.SemanticPipelineName);
        }

        public static string GetModelId(this ISettingsManager settingsManager)
        {
            return settingsManager.GetValueByDescriptor<string>(ModuleSettings.SemanticModelId);
        }
    }
}
