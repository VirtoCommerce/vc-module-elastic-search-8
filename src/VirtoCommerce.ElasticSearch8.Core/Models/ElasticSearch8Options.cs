using System;

namespace VirtoCommerce.ElasticSearch8.Core.Models
{
    public class ElasticSearch8Options
    {
        public string Server { get; set; }

        public string User { get; set; }

        /// <summary>
        /// Password
        /// </summary>
        public string Key { get; set; }

        public string CertificateFingerprint { get; set; }

        /// <summary>
        /// Set to True, to turn on settings that aid in debugging like DisableDirectStreaming() and PrettyJson() so that the original request
        /// and response JSON can be inspected. It also always asks the server for the full stack trace on errors.
        /// </summary>
        public bool EnableDebugMode { get; set; } = false;

        /// <summary>
        /// Timeout for every Elasticsearch request that does not set its own.
        /// </summary>
        public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Timeout for bulk indexing and deletion, which may run ingest pipelines such as semantic-search inference.
        /// </summary>
        public TimeSpan LongRunningRequestTimeout { get; set; } = TimeSpan.FromMinutes(10);
    }
}
