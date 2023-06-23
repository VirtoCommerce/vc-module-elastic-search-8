namespace VirtoCommerce.ElasticSearch8x.Data.Models
{
    public class ElasticSearch8xOptions
    {
        public string Server { get; set; }

        public string User { get; set; }

        /// <summary>
        /// Password
        /// </summary>
        public string Key { get; set; }

        public string CertificateFingerprint { get; set; }
    }
}
