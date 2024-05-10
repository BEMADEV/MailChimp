using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using com.bemaservices.MailChimp.Utility;
using Quartz;

using Rock;
using Rock.Attribute;
using Rock.Communication;
using Rock.Data;
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
        DefaultValue = "0,1,2,3",
        Key = "MailChimpToRock")]
    [EnumsField( "Rock To MailChimp Settings",
        Description = "The rights Rock has to edit MailChimp.",
        EnumSourceType = typeof( SyncPrivileges ),
        DefaultValue = "0,1,2",
        Key = "RockToMailChimp")]
    [DisallowConcurrentExecution]
    public class MailChimpSync : IJob
    {
        public virtual void Execute( IJobExecutionContext context )
        {
            JobDataMap dataMap = context.JobDetail.JobDataMap;

            var accounts = DefinedTypeCache.Get( MailChimp.SystemGuid.SystemDefinedTypes.MAIL_CHIMP_ACCOUNTS.AsGuid() );

            var audienceGuids = dataMap.GetString( "Audiences" ).SplitDelimitedValues().AsGuidList();
            var daysToSyncUpdates = dataMap.GetString( "DaysToSyncUpdates" ).AsIntegerOrNull();
            var mailChimpToRockSettings = dataMap.GetString( "MailChimpToRock" ).SplitDelimitedValues().AsEnumList<SyncPrivileges>();
            var rockToMailChimpSettings = dataMap.GetString( "RockToMailChimp" ).SplitDelimitedValues().AsEnumList<SyncPrivileges>();

            MailChimpSyncSettings mailChimpSyncSettings = new MailChimpSyncSettings();
            mailChimpSyncSettings.DaysToSyncUpdates = daysToSyncUpdates;
            mailChimpSyncSettings.MailChimpToRockSettings = mailChimpToRockSettings;
            mailChimpSyncSettings.RockToMailChimpSettings = rockToMailChimpSettings;

            foreach ( var account in accounts.DefinedValues )
            {
                try
                {
                    Utility.MailChimpApi mailChimpApi = new Utility.MailChimpApi( account );
                    var mailChimpLists = mailChimpApi.GetMailChimpLists();

                    foreach ( var list in mailChimpLists )
                    {
                        if ( !audienceGuids.Any() || audienceGuids.Contains( list.Guid ) )
                        {
                            mailChimpApi.GetMailChimpMergeFields( DefinedValueCache.Get( list.Guid ) );

                            mailChimpApi.SyncMembers( DefinedValueCache.Get( list.Guid ), mailChimpSyncSettings );
                        }
                    }
                }
                catch ( Exception ex )
                {
                    string message = String.Format( "Error Syncing {0} Account from Mailchimp", account.Value );
                    ExceptionLogService.LogException( new Exception( message, ex ) );
                }

            }
        }
    }
}