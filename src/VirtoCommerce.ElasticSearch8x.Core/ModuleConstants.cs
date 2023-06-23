using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Settings;

namespace VirtoCommerce.ElasticSearch8x.Core;

public static class ModuleConstants
{
    public static class Settings
    {
        public static class General
        {
            public static SettingDescriptor ElasticSearch8xEnabled { get; } = new SettingDescriptor
            {
                Name = "ElasticSearch8x.ElasticSearch8xEnabled",
                GroupName = "ElasticSearch8x|General",
                ValueType = SettingValueType.Boolean,
                DefaultValue = false,
            };

            public static IEnumerable<SettingDescriptor> AllGeneralSettings
            {
                get
                {
                    yield return ElasticSearch8xEnabled;
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
