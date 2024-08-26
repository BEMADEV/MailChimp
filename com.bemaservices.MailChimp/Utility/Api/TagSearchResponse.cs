using System.Collections.Generic;
using Newtonsoft.Json;
namespace com.bemaservices.MailChimp.Utility.Api
{
    internal class TagSearchResponse
    {
        [JsonProperty( "tags" )]
        public List<TagResponse> Tags { get; set; }
        [JsonProperty( "total_items" )]
        public int? TotalItems { get; set; }
    }
}