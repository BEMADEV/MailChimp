using System.Collections.Generic;
using System.Linq;
using System.Net;
using Newtonsoft.Json;
using RestSharp;
using RestSharp.Authenticators;
using Rock.Web.Cache;
using Rock.Data;
using Rock.Model;
using Rock.Security;
using System;
using Rock;
namespace com.bemaservices.MailChimp.Utility.Api
{
    internal static class MailchimpDirectApi
    {
        #region Utilities        
        /// <summary>
        /// Gets the settings.
        /// </summary>
        /// <param name="rockContext">The rock context.</param>
        /// <returns></returns>
        private static List<AttributeValue> GetSettings( RockContext rockContext, DefinedValueCache mailChimpAccount )
        {
            if ( mailChimpAccount != null )
            {
                var definedValueId = mailChimpAccount.Id;
                var definedTypeIdString = mailChimpAccount.DefinedTypeId.ToString();
                var definedValueEntityType = EntityTypeCache.Get( typeof( Rock.Model.DefinedValue ) );
                if ( definedValueEntityType != null )
                {
                    var service = new AttributeValueService( rockContext );
                    return service.Queryable( "Attribute" )
                        .Where( v => v.Attribute.EntityTypeId == definedValueEntityType.Id &&
                                v.Attribute.EntityTypeQualifierValue == definedTypeIdString &&
                                v.EntityId == definedValueId )
                        .ToList();
                }
            }
            return null;
        }
        /// <summary>
        /// Gets the setting value.
        /// </summary>
        /// <param name="values">The values.</param>
        /// <param name="key">The key.</param>
        /// <returns></returns>
        private static string GetSettingValue( List<AttributeValue> values, string key, bool encryptedValue = false )
        {
            string value = values
                .Where( v => v.AttributeKey == key )
                .Select( v => v.Value )
                .FirstOrDefault();
            if ( encryptedValue && !string.IsNullOrWhiteSpace( value ) )
            {
                try
                { value = Encryption.DecryptString( value ); }
                catch { }
            }
            return value;
        }
        /// <summary>
        /// Return a rest client.
        /// </summary>
        /// <returns>The rest client.</returns>
        private static RestClient RestClient( DefinedValueCache mailChimpAccount )
        {
            string apiToken = null;
            string serverPrefix = "";
            using ( RockContext rockContext = new RockContext() )
            {
                var settings = GetSettings( rockContext, mailChimpAccount );
                if ( settings != null )
                {
                    apiToken = GetSettingValue( settings, "APIKey", false );
                    serverPrefix = GetSettingValue( settings, "ServerPrefix", false );
                }
            }
            if ( serverPrefix.IsNullOrWhiteSpace() )
            {
                serverPrefix = "us19";
            }
            var serverLink = string.Format( Constants.ROOT_API_URL, serverPrefix.Trim() );
            var restClient = new RestClient( serverLink );
            restClient.Authenticator = new HttpBasicAuthenticator( "anystring", apiToken );
            return restClient;
        }
        /// <summary>
        /// RestClient request to string for debugging purposes.
        /// </summary>
        /// <param name="restClient">The rest client.</param>
        /// <param name="restRequest">The rest request.</param>
        /// <returns>The RestClient Request in string format.</returns>
        // https://stackoverflow.com/questions/15683858/restsharp-print-raw-request-and-response-headers
        private static string RequestToString( RestClient restClient, RestRequest restRequest )
        {
            var requestToLog = new
            {
                resource = restRequest.Resource,
                // Parameters are custom anonymous objects in order to have the parameter type as a nice string
                // otherwise it will just show the enum value
                parameters = restRequest.Parameters.Select( parameter => new
                {
                    name = parameter.Name,
                    value = parameter.Value,
                    type = parameter.Type.ToString()
                } ),
                // ToString() here to have the method as a nice string otherwise it will just show the enum value
                method = restRequest.Method.ToString(),
                // This will generate the actual Uri used in the request
                uri = restClient.BuildUri( restRequest ),
            };
            return JsonConvert.SerializeObject( requestToLog );
        }
        /// <summary>
        /// RestClient response to string for debugging purposes.
        /// </summary>
        /// <param name="restResponse">The rest response.</param>
        /// <returns>The RestClient response in string format.</returns>
        // https://stackoverflow.com/questions/15683858/restsharp-print-raw-request-and-response-headers
        private static string ResponseToString( IRestResponse restResponse )
        {
            var responseToLog = new
            {
                statusCode = restResponse.StatusCode,
                content = restResponse.Content,
                headers = restResponse.Headers,
                // The Uri that actually responded (could be different from the requestUri if a redirection occurred)
                responseUri = restResponse.ResponseUri,
                errorMessage = restResponse.ErrorMessage,
            };
            return JsonConvert.SerializeObject( responseToLog );
        }
        #endregion
        #region Public Methods
        /// <summary>
        /// Gets the packages.
        /// </summary>
        /// <param name="getPackagesResponse">The get packages response.</param>
        /// <param name="errorMessages">The error messages.</param>
        /// <returns>True/False value of whether the request was successfully sent or not.</returns>
        internal static bool GetTagsForUser( DefinedValueCache mailChimpAccount, string listId, string subscriberHash, out List<TagResponse> tagResponse, List<string> errorMessages )
        {
            tagResponse = null;
            RestClient restClient = RestClient( mailChimpAccount );
            RestRequest restRequest = new RestRequest( String.Format( Constants.LIST_MEMBER_TAGS, listId, subscriberHash ) );
            IRestResponse restResponse = restClient.Execute( restRequest );
            if ( restResponse.StatusCode == HttpStatusCode.Unauthorized )
            {
                errorMessages.Add( "Failed to authorize Mailchimp. Please confirm your access token." );
                return false;
            }
            if ( restResponse.StatusCode != HttpStatusCode.OK )
            {
                errorMessages.Add( "Failed to get Mailchimp Tags for User: " + restResponse.Content );
                return false;
            }
            var tagSearchResponse = JsonConvert.DeserializeObject<TagSearchResponse>( restResponse.Content );
            if ( tagSearchResponse == null )
            {
                errorMessages.Add( "Get Tags for User is not valid: " + restResponse.Content );
                return false;
            }
            else
            {
                tagResponse = tagSearchResponse.Tags;
            }
            return true;
        }
        #endregion
    }
}