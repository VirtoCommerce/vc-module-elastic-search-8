using Elastic.Clients.Elasticsearch;
using VirtoCommerce.SearchModule.Core.Model;

namespace VirtoCommerce.ElasticSearch8x.Data.Extensions
{
    public static class ElasticSearchGeoPointExtensions
    {
        public static object ToElasticValue(this GeoPoint point)
        {
            return point == null ? null : new { lat = point.Latitude, lon = point.Longitude };
        }

        public static GeoLocation ToGeoLocation(this GeoPoint point)
        {
            return point == null ? null : GeoLocation.LatitudeLongitude(new LatLonGeoLocation { Lat = point.Latitude, Lon = point.Longitude });
        }
    }
}
