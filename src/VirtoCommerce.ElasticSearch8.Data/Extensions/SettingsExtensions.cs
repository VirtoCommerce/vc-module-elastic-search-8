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

        public static bool GetSemanticSearchEnabled(this ISettingsManager settingsManager)
        {
            return settingsManager.GetValueByDescriptor<bool>(ModuleSettings.EnableSemanticSearch);
        }

        public static string GetPipelineName(this ISettingsManager settingsManager)
        {
            return settingsManager.GetValueByDescriptor<string>(ModuleSettings.SemanticPipelineName);
        }

        public static string GetModelId(this ISettingsManager settingsManager)
        {
            return settingsManager.GetValueByDescriptor<string>(ModuleSettings.SemanticModelId);
        }

        public static string GetModelFieldName(this ISettingsManager settingsManager)
        {
            return settingsManager.GetValueByDescriptor<string>(ModuleSettings.SemanticFieldName);
        }

        public static string GetSemanticModelType(this ISettingsManager settingsManager)
        {
            return settingsManager.GetValueByDescriptor<string>(ModuleSettings.SemanticModelType);
        }
    }
}
