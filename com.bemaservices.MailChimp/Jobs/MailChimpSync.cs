using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using com.bemaservices.MailChimp.Utility;
using Quartz;

using Rock;
using Rock.Attribute;
using Rock.Communication;
using Rock.Data;
using Rock.Jobs;
using Rock.Model;
using Rock.Web.Cache;

namespace com.bemaservices.MailChimp.Jobs
{
    [DefinedValueField( "0ED80CA8-987E-4A00-8CA5-56D0A4BDD629", "Audiences", "The Audiences whose members should by synced. Leave blank if you would like all audiences synced.", false, true, false, "", "", 0, "Audiences" )]
    [IntegerField( "Days Back to Sync Updates For", "Limit the sync to only Mailchimp and Rock members updated within the last X days. Leave blank to sync all members", false, Key = "DaysToSyncUpdates" )]
    [BooleanField( "Import Mailchimp Tags?", "Whether or not to include Mailchimp Tags", false, Key = "ImportMailChimpTags" )]
    [EnumsField( "MailChimp To Rock Settings",
        Description = "The rights MailChimp has to edit Rock.",
        EnumSourceType = typeof( SyncPrivileges ),
        DefaultValue = "0,1,2,3,4",
        Key = "MailChimpToRock" )]
    [EnumsField( "Rock To MailChimp Settings",
        Description = "The rights Rock has to edit MailChimp.",
        EnumSourceType = typeof( SyncPrivileges ),
        DefaultValue = "0,1,2",
        Key = "RockToMailChimp" )]
    [DisallowConcurrentExecution]
    public class MailChimpSync : RockJob
    {
        public override void Execute()
        {
            var accounts = DefinedTypeCache.Get( MailChimp.SystemGuid.SystemDefinedTypes.MAIL_CHIMP_ACCOUNTS.AsGuid() );

            var audienceGuids = GetAttributeValue( "Audiences" ).SplitDelimitedValues().AsGuidList();
            var daysToSyncUpdates = GetAttributeValue( "DaysToSyncUpdates" ).AsIntegerOrNull();
            var mailChimpToRockSettings = GetAttributeValue( "MailChimpToRock" ).SplitDelimitedValues().AsEnumList<SyncPrivileges>();
            var rockToMailChimpSettings = GetAttributeValue( "RockToMailChimp" ).SplitDelimitedValues().AsEnumList<SyncPrivileges>();

            MailChimpSyncSettings mailChimpSyncSettings = new MailChimpSyncSettings();
            mailChimpSyncSettings.DaysToSyncUpdates = daysToSyncUpdates;
            mailChimpSyncSettings.MailChimpToRockSettings = mailChimpToRockSettings;
            mailChimpSyncSettings.RockToMailChimpSettings = rockToMailChimpSettings;

            StringBuilder results = new StringBuilder();
            foreach ( var account in accounts.DefinedValues )
            {
                try
                {
                    Utility.MailChimpApi mailChimpApi = new Utility.MailChimpApi( account );
                    var mailChimpLists = mailChimpApi.GetMailChimpLists();
                    results.AppendFormat( "Grabbed {0} Audiences from Mailchimp", mailChimpLists.Count );

                    foreach ( var list in mailChimpLists )
                    {
                        if ( !audienceGuids.Any() || audienceGuids.Contains( list.Guid ) )
                        {
                            results.AppendLine().AppendLine().AppendFormat( "Syncing {0}:", list.Value );
                            results.Append( "<ul>" );

                            var mergefields = mailChimpApi.GetMailChimpMergeFields( DefinedValueCache.Get( list.Guid ), out List<string> mergeFieldStatusMessages );
                            foreach(var mergeFieldStatusMessage in mergeFieldStatusMessages )
                            {
                                results.AppendFormat( "<li>{0}</li>", mergeFieldStatusMessage );
                            }

                            mailChimpApi.SyncMembers( DefinedValueCache.Get( list.Guid ), mailChimpSyncSettings, out List<String> membersStatusMessages );
                            foreach ( var membersStatusMessage in membersStatusMessages )
                            {
                                results.AppendFormat( "<li>{0}</li>", membersStatusMessage );
                            }

                            results.Append( "</ul>" );
                        }
                    }
                }
                catch ( Exception ex )
                {
                    string message = String.Format( "Error Syncing {0} Account from Mailchimp", account.Value );
                    results.AppendLine( message );
                    ExceptionLogService.LogException( new Exception( message, ex ) );
                }

            }

            this.Result = results.ToString();
        }
    }
}