using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MCNet = MailChimp.Net;
using MCInterfaces = MailChimp.Net.Interfaces;
using MCModels = MailChimp.Net.Models;

using Rock;
using Rock.Data;
using Rock.Model;
using Rock.Attribute;
using Rock.Web.Cache;
using System.Data.Entity;
using MailChimp.Net.Core;
using DotLiquid.Tags;
using MailChimp.Net.Models;
using System.Security.Cryptography.X509Certificates;
using com.bemaservices.MailChimp.Utility.Api;

namespace com.bemaservices.MailChimp.Utility
{
    public class MailChimpApi
    {
        private MCInterfaces.IMailChimpManager _mailChimpManager;
        private string _apiKey;
        private DefinedValueCache _mailChimpAccount;

        private Guid MailchimpTagDefinedTypeGuid
        {
            get { return SystemGuid.SystemDefinedTypes.MAIL_CHIMP_TAGS.AsGuid(); }
        }

        public MailChimpApi( DefinedValueCache mailChimpAccount )
        {

            if ( !mailChimpAccount.DefinedType.Guid.ToString().Equals( MailChimp.SystemGuid.SystemDefinedTypes.MAIL_CHIMP_ACCOUNTS, StringComparison.OrdinalIgnoreCase ) )
            {
                var newException = new Exception( "Defined Value is not of type Mail Chimp Account." );
                ExceptionLogService.LogException( newException );
            }
            else
            {
                _mailChimpAccount = mailChimpAccount;

                var apiKeyAttributeKey = AttributeCache.Get( MailChimp.SystemGuid.Attribute.MAIL_CHIMP_ACCOUNT_APIKEY_ATTRIBUTE ).Key;
                _apiKey = _mailChimpAccount.GetAttributeValue( apiKeyAttributeKey );

                if ( _apiKey.IsNullOrWhiteSpace() )
                {
                    var newException = new Exception( "No Api Key provided on the Mail Account Defined Value" );
                    ExceptionLogService.LogException( newException );
                }
                else
                {
                    _mailChimpManager = new MCNet.MailChimpManager( _apiKey );
                }
            }
        }

        public List<DefinedValue> GetMailChimpLists()
        {
            RockContext rockContext = new RockContext();
            DefinedValueService definedValueService = new DefinedValueService( rockContext );
            AttributeValueService attributeValueService = new AttributeValueService( rockContext );

            List<DefinedValue> mailChimpListValues = null;
            List<int?> mailChimpListDefinedValueIds = new List<int?>();

            if ( _mailChimpAccount is null || _apiKey.IsNullOrWhiteSpace() )
            {
                var newException = new Exception( "The Helper Class has not been properly intialized with a valid Mail Chimp Account Defined Value, or the Mail Chimp Account does not have an APIKEY." );
                ExceptionLogService.LogException( newException );
            }
            else
            {
                try
                {
                    mailChimpListDefinedValueIds = attributeValueService.GetByAttributeId( AttributeCache.Get( MailChimp.SystemGuid.Attribute.MAIL_CHIMP_AUDIENCE_ACCOUNT_ATTRIBUTE ).Id )
                                                  .Where( av => av.Value.Equals( _mailChimpAccount.Guid.ToString() ) ).Select( av => av.EntityId )
                                                  .ToList();

                    mailChimpListValues = definedValueService.GetByDefinedTypeGuid( MailChimp.SystemGuid.SystemDefinedTypes.MAIL_CHIMP_AUDIENCES.AsGuid() )
                        .Where( v => mailChimpListDefinedValueIds.Contains( v.Id ) ).ToList();

                }
                catch ( Exception ex )
                {
                    string message = String.Format( "Error Grabbing Mailchimp Audiences from Rock. This is most likely due to the configured Mailchimp account being invalid or closed. Please check your configuration to verify you are using an active Mailchimp account." );
                    ExceptionLogService.LogException( new Exception( message ) );
                }

                try
                {
                    var mailChimpListCollection = _mailChimpManager.Lists.GetAllAsync().Result;

                    // Loop over each List from Mail Chimp and attempt to find the related Defined Value in Rock.  Then Update / Add those defined values into Rock
                    foreach ( var mailChimpList in mailChimpListCollection )
                    {
                        try
                        {
                            var mailChimpListValue = mailChimpListValues.Where( x => x.ForeignId == mailChimpList.WebId &&
                                                                                x.ForeignKey == MailChimp.Constants.ForeignKey )
                                                                    .FirstOrDefault();
                            if ( mailChimpListValue is null )
                            {
                                try
                                {
                                    mailChimpListValue = new DefinedValue();
                                    mailChimpListValue.ForeignId = mailChimpList.WebId;
                                    mailChimpListValue.ForeignKey = MailChimp.Constants.ForeignKey;
                                    mailChimpListValue.IsSystem = true;
                                    mailChimpListValue.DefinedTypeId = DefinedTypeCache.Get( MailChimp.SystemGuid.SystemDefinedTypes.MAIL_CHIMP_AUDIENCES.AsGuid() ).Id;
                                    mailChimpListValue.Value = mailChimpList.Name;

                                    definedValueService.Add( mailChimpListValue );

                                    rockContext.SaveChanges();
                                }
                                catch ( Exception ex )
                                {
                                    string message = String.Format( "Error Adding {0} to Rock", mailChimpList.Name );
                                    ExceptionLogService.LogException( new Exception( message, ex ) );
                                }
                            }

                            try
                            {
                                UpdateMailChimpListDefinedValue( mailChimpList, ref mailChimpListValue, rockContext );
                            }
                            catch ( Exception ex )
                            {
                                string message = String.Format( "Error Updating {0}'s Defined Value", mailChimpList.Name );
                                ExceptionLogService.LogException( new Exception( message, ex ) );
                            }
                        }
                        catch ( Exception ex )
                        {
                            string message = String.Format( "Error Loading {0}", mailChimpList.Name );
                            ExceptionLogService.LogException( new Exception( message, ex ) );
                        }

                    }

                    try
                    {
                        // Look for any DefinedValues in Rock that are no longer in Mail Chimp and remove them.
                        var mailChimpListValuesToRemove = mailChimpListValues
                                                           .Where( x => !mailChimpListCollection.Any( y => y.WebId == x.ForeignId && x.ForeignKey == MailChimp.Constants.ForeignKey )
                                                           && mailChimpListDefinedValueIds.Contains( x.Id )
                                                           );

                        definedValueService.DeleteRange( mailChimpListValuesToRemove );

                        rockContext.SaveChanges();
                    }
                    catch ( Exception ex )
                    {
                        string message = String.Format( "Error Removing deleted lists from Rock" );
                        ExceptionLogService.LogException( new Exception( message, ex ) );
                    }

                    return definedValueService.GetByDefinedTypeGuid( MailChimp.SystemGuid.SystemDefinedTypes.MAIL_CHIMP_AUDIENCES.AsGuid() )
                                           .Where( v => mailChimpListDefinedValueIds.Contains( v.Id ) )
                                           .ToList();
                }
                catch ( Exception ex )
                {
                    string message = String.Format( "Error Grabbing Mailchimp Lists from Mailchimp" );
                    ExceptionLogService.LogException( new Exception( message, ex ) );
                }
            }

            return null;
        }

        public List<DefinedValue> GetMailChimpMergeFields( DefinedValueCache mailChimpListValue )
        {
            RockContext rockContext = new RockContext();
            DefinedValueService definedValueService = new DefinedValueService( rockContext );
            AttributeValueService attributeValueService = new AttributeValueService( rockContext );

            List<DefinedValue> mailChimpMergeFieldValues = null;
            List<int?> mailChimpMergeFieldDefinedValueIds = new List<int?>();

            if ( _mailChimpAccount is null || _apiKey.IsNullOrWhiteSpace() )
            {
                var newException = new Exception( "The Helper Class has not been properly intialized with a valid Mail Chimp Account Defined Value, or the Mail Chimp Account does not have an APIKEY." );
                ExceptionLogService.LogException( newException );
            }
            else
            {
                try
                {

                    mailChimpMergeFieldDefinedValueIds = attributeValueService.GetByAttributeId( AttributeCache.Get( MailChimp.SystemGuid.Attribute.MAIL_CHIMP_MERGE_FIELD_AUDIENCE_ATTRIBUTE ).Id )
                                                  .Where( av => av.Value.Equals( mailChimpListValue.Guid.ToString() ) )
                                                  .Select( av => av.EntityId )
                                                  .ToList();

                    mailChimpMergeFieldValues = definedValueService.GetByDefinedTypeGuid( MailChimp.SystemGuid.SystemDefinedTypes.MAIL_CHIMP_MERGE_FIELDS.AsGuid() )
                        .Where( v => mailChimpMergeFieldDefinedValueIds.Contains( v.Id ) ).ToList();

                }
                catch ( Exception ex )
                {
                    string message = String.Format( "Error Grabbing Mail Chimp Merge Fields from Rock. This is most likely due to the configured Mailchimp account being invalid or closed. Please check your configuration to verify you are using an active Mailchimp account." );
                    ExceptionLogService.LogException( new Exception( message ) );
                }

                try
                {

                    var mailChimpListId = mailChimpListValue.GetAttributeValue( AttributeCache.Get( MailChimp.SystemGuid.Attribute.MAIL_CHIMP_AUDIENCE_ID_ATTRIBUTE ).Key );
                    var mailChimpMergeFieldCollection = _mailChimpManager.MergeFields.GetAllAsync( mailChimpListId ).Result
                            .Where( x => !MailChimp.Constants.MERGE_TAGS_TO_IGNORE.Any( x2 => x2 == x.Tag ) );

                    // Loop over each Merge Field from Mail Chimp and attempt to find the related Defined Value in Rock.  Then Update / Add those defined values into Rock
                    foreach ( var mailChimpMergeField in mailChimpMergeFieldCollection )
                    {
                        try
                        {
                            var mailChimpMergeFieldValue = mailChimpMergeFieldValues.Where( x => x.ForeignId == mailChimpMergeField.MergeId &&
                                                                                x.ForeignKey == MailChimp.Constants.ForeignKey )
                                                                    .FirstOrDefault();
                            if ( mailChimpMergeFieldValue is null )
                            {
                                try
                                {
                                    mailChimpMergeFieldValue = new DefinedValue();
                                    mailChimpMergeFieldValue.ForeignId = mailChimpMergeField.MergeId;
                                    mailChimpMergeFieldValue.ForeignKey = MailChimp.Constants.ForeignKey;
                                    mailChimpMergeFieldValue.IsSystem = true;
                                    mailChimpMergeFieldValue.DefinedTypeId = DefinedTypeCache.Get( MailChimp.SystemGuid.SystemDefinedTypes.MAIL_CHIMP_MERGE_FIELDS.AsGuid() ).Id;
                                    mailChimpMergeFieldValue.Value = mailChimpMergeField.Tag;


                                    definedValueService.Add( mailChimpMergeFieldValue );
                                    rockContext.SaveChanges();

                                }
                                catch ( Exception ex )
                                {
                                    string message = String.Format( "Error Adding {0} to Rock", mailChimpMergeField.Name );
                                    ExceptionLogService.LogException( new Exception( message, ex ) );
                                }
                            }

                            try
                            {
                                UpdateMailChimpMergeFieldDefinedValue( mailChimpMergeField, mailChimpListValue.Guid, ref mailChimpMergeFieldValue, rockContext );
                            }
                            catch ( Exception ex )
                            {
                                string message = String.Format( "Error Updating {0}'s Defined Value", mailChimpMergeField.Name );
                                ExceptionLogService.LogException( new Exception( message, ex ) );
                            }
                        }
                        catch ( Exception ex )
                        {
                            string message = String.Format( "Error Loading {0}", mailChimpMergeField.Name );
                            ExceptionLogService.LogException( new Exception( message, ex ) );
                        }

                    }

                    try
                    {
                        // Look for any DefinedValues in Rock that are no longer in Mail Chimp and remove them.
                        var mailChimpMergeFieldValuesToRemove = mailChimpMergeFieldValues
                                                            .Where( x => !mailChimpMergeFieldCollection.Any( y => y.MergeId == x.ForeignId && x.ForeignKey == MailChimp.Constants.ForeignKey )
                                                            && mailChimpMergeFieldDefinedValueIds.Contains( x.Id )
                                                            );

                        definedValueService.DeleteRange( mailChimpMergeFieldValuesToRemove );
                    }
                    catch ( Exception ex )
                    {
                        string message = String.Format( "Error Removing deleted lists from Rock" );
                        ExceptionLogService.LogException( new Exception( message, ex ) );
                    }

                    rockContext.SaveChanges();


                    return definedValueService.GetByDefinedTypeGuid( MailChimp.SystemGuid.SystemDefinedTypes.MAIL_CHIMP_MERGE_FIELDS.AsGuid() )
                                           .Where( v => mailChimpMergeFieldDefinedValueIds.Contains( v.Id ) )
                                           .ToList();
                }
                catch ( Exception ex )
                {
                    string message = String.Format( "Error Grabbing Mailchimp Lists from Mailchimp" );
                    ExceptionLogService.LogException( new Exception( message, ex ) );
                }
            }

            return null;
        }

        public void SyncMembers( DefinedValueCache mailChimpList, MailChimpSyncSettings mailchimpSyncSettings )
        {
            Dictionary<int, MCModels.Member> mailChimpMemberLookUp = new Dictionary<int, MCModels.Member>();
            var mailChimpListIdAttributeKey = AttributeCache.Get( MailChimp.SystemGuid.Attribute.MAIL_CHIMP_AUDIENCE_ID_ATTRIBUTE.AsGuid() ).Key;
            var mailChimpListId = mailChimpList.GetAttributeValue( mailChimpListIdAttributeKey );
            Dictionary<int, DefinedValue> mailChimpTags = new DefinedValueService( new RockContext() )
                    .Queryable()
                    .AsNoTracking()
                    .Where( dv => dv.DefinedType.Guid == MailchimpTagDefinedTypeGuid && dv.ForeignId.HasValue )
                    .ToDictionary( dv => dv.ForeignId.Value, dv => dv );

            DateTime? dateLimit = null;
            if ( mailchimpSyncSettings.DaysToSyncUpdates.HasValue )
            {
                dateLimit = RockDateTime.Now.AddDays( mailchimpSyncSettings.DaysToSyncUpdates.Value * -1 );
            }

            try
            {
                // First, Fetch the Records
                int offset = 0;
                bool moreRecordsToFetch = true;
                var memberRequest = new MemberRequest();
                var mailChimpMembers = new List<MCModels.Member>();
                memberRequest.Limit = 1000;

                if ( dateLimit.HasValue )
                {
                    memberRequest.SinceLastChanged = dateLimit.ToISO8601DateString();
                }
                try
                {
                    while ( moreRecordsToFetch )
                    {
                        memberRequest.Offset = offset;

                        var result = _mailChimpManager.Members.GetAllAsync( mailChimpListId, memberRequest ).Result;

                        if ( result.Count() > 0 )
                        {
                            mailChimpMembers.AddRange( result );
                            offset += 1000;
                        }
                        else
                        {
                            moreRecordsToFetch = false;
                        }

                    }
                }
                catch ( Exception ex )
                {
                    string message = String.Format( "Error occurred pulling records from Mailchimp Audience '{0}'", mailChimpList.Value );
                    ExceptionLogService.LogException( new Exception( message, ex ) );
                }

                var mailChimpMembersNotAdded = new List<Member>();

                //Get all Groups that have an attribute set to this Mail Chimp List's Defined Value.
                var groupIds = new AttributeValueService( new RockContext() ).Queryable().AsNoTracking()
                    .Where( x => x.Value.Equals( mailChimpList.Guid.ToString(), StringComparison.OrdinalIgnoreCase ) &&
                                 x.Attribute.EntityType.FriendlyName == Rock.Model.Group.FriendlyTypeName )
                    .Select( x => x.EntityId )
                    .Where( x => x.HasValue )
                    .Distinct()
                    .ToList();

                //Match all the mailChimpMembers to people in Rock.
                foreach ( var member in mailChimpMembers )
                {
                    try
                    {
                        RockContext rockContext = new RockContext();
                        GroupMemberService groupMemberService = new GroupMemberService( rockContext );
                        GroupService groupService = new GroupService( rockContext );
                        rockContext.Database.CommandTimeout = 600;

                        var rockPerson = GetRockPerson( member, mailchimpSyncSettings );
                        if ( rockPerson != null )
                        {
                            mailChimpMemberLookUp.AddOrIgnore( rockPerson.Id, member );

                            SyncPerson( rockPerson.Id, member, mailChimpListId, groupIds, mailchimpSyncSettings, mailChimpTags );
                        }
                        else
                        {
                            mailChimpMembersNotAdded.Add( member );
                        }
                    }
                    catch ( Exception ex )
                    {
                        string message = String.Format( "Error Grabbing record #{0} with email {1} for Mailchimp Audience '{2}'", member.Id, member.EmailAddress, mailChimpList.Value );
                        ExceptionLogService.LogException( new Exception( message, ex ) );
                    }
                }

                if ( mailChimpMembersNotAdded.Any() )
                {
                    ExceptionLogService.LogException( new Exception( mailChimpMembersNotAdded.Count().ToString() + " Mailchimp Members not added." ) );
                }


                if ( groupIds.Any() )
                {
                    foreach ( var groupId in groupIds.Where( g => g.HasValue ).Distinct().ToList() )
                    {
                        try
                        {
                            RockContext rockContext = new RockContext();
                            GroupService groupService = new GroupService( rockContext );
                            rockContext.Database.CommandTimeout = 600;

                            var memberList = groupService
                                .Queryable()
                                .Where( g => g.Id == groupId.Value )
                                .SelectMany( g => g.Members )
                                .Where( gm => !dateLimit.HasValue || gm.ModifiedDateTime >= dateLimit || gm.Person.ModifiedDateTime >= dateLimit )
                                .ToList();

                            if ( memberList.Any() )
                            {
                                foreach ( var groupMember in memberList )
                                {
                                    try
                                    {
                                        if ( !mailChimpMemberLookUp.ContainsKey( groupMember.PersonId ) )
                                        {
                                            if ( mailchimpSyncSettings.RockToMailChimpSettings.Contains( SyncPrivileges.AddRecordToList ) )
                                            {
                                                AddPersonToMailChimp( groupMember, mailChimpListId, groupIds, mailchimpSyncSettings );
                                            }
                                        }
                                    }
                                    catch ( Exception ex )
                                    {
                                        string message = String.Format( "Error Adding Person #{0} to Mailchimp Audience '{1}'", groupMember.Person.Id, mailChimpList.Value );
                                        ExceptionLogService.LogException( new Exception( message, ex ) );
                                    }
                                }
                            }
                        }
                        catch ( Exception ex )
                        {
                            string message = String.Format( "Error occurred adding members of Group #{0} to Mailchimp Audience '{1}'", groupId, mailChimpList.Value );
                            ExceptionLogService.LogException( new Exception( message, ex ) );
                        }

                    }
                }
            }
            catch ( Exception ex )
            {
                string message = String.Format( "Error occurred importing Mailchimp Audience '{0}'", mailChimpList.Value );
                ExceptionLogService.LogException( new Exception( message, ex ) );
            }

        }

        private bool AddPersonToMailChimp( GroupMember groupMember, string mailChimpListId, List<int?> groupIds, MailChimpSyncSettings mailchimpSyncSettings )
        {
            bool foundMember = false;
            bool addedPerson = false;
            MCModels.Member member = null;

            if ( groupMember.Person.Email.IsNotNullOrWhiteSpace() )
            {
                try
                {
                    foundMember = _mailChimpManager.Members.ExistsAsync( mailChimpListId, groupMember.Person.Email, null, false ).Result;

                    if ( !foundMember && mailchimpSyncSettings.RockToMailChimpSettings.Contains( SyncPrivileges.AddNewRecord ) )
                    {
                        RockContext rockContext = new RockContext();
                        var emailTypeId = DefinedValueCache.Get( Rock.SystemGuid.DefinedValue.PERSON_SEARCH_KEYS_EMAIL ).Id;
                        var EmailAddresses = groupMember.Person.GetPersonSearchKeys( rockContext ).AsNoTracking().Where( k => k.SearchTypeValueId == emailTypeId ).Select( x => x.SearchValue );
                        foreach ( var email in EmailAddresses )
                        {
                            try
                            {
                                foundMember = _mailChimpManager.Members.ExistsAsync( mailChimpListId, email, null, false ).Result;
                                if ( foundMember )
                                {
                                    // if the Person is found using an alternate email address, their email address needs to be updated in mail chimp.
                                    member = new MCModels.Member
                                    {
                                        EmailAddress = groupMember.Person.Email,
                                        Status = GetMailChimpMemberStatus( groupMember ),
                                        Id = _mailChimpManager.Members.Hash( email )
                                    };
                                    break;
                                }
                            }
                            catch ( Exception ex )
                            {
                                ExceptionLogService.LogException( ex );
                            }
                        }
                    }
                }
                catch ( Exception ex )
                {
                    ExceptionLogService.LogException( ex );
                }

            }

            SyncPerson( groupMember.Person.Id, member, mailChimpListId, groupIds, mailchimpSyncSettings );

            return addedPerson;
        }

        private void UpdateMailChimpListDefinedValue( MCModels.List mailChimpList, ref DefinedValue mailChimpListValue, RockContext rockContext )
        {
            mailChimpListValue.Value = mailChimpList.Name;
            mailChimpListValue.Description = mailChimpList.SubscribeUrlLong;

            var mailChimpAccountAttribute = AttributeCache.Get( MailChimp.SystemGuid.Attribute.MAIL_CHIMP_AUDIENCE_ACCOUNT_ATTRIBUTE );
            var mailChimpIdAttribute = AttributeCache.Get( MailChimp.SystemGuid.Attribute.MAIL_CHIMP_AUDIENCE_ID_ATTRIBUTE );

            Rock.Attribute.Helper.SaveAttributeValue( mailChimpListValue, mailChimpAccountAttribute, _mailChimpAccount.Guid.ToString(), rockContext );
            Rock.Attribute.Helper.SaveAttributeValue( mailChimpListValue, mailChimpIdAttribute, mailChimpList.Id, rockContext );
        }

        private void UpdateMailChimpMergeFieldDefinedValue( MCModels.MergeField mailChimpMergeField, Guid mailChimpListValueGuid, ref DefinedValue mailChimpMergeFieldValue, RockContext rockContext )
        {
            mailChimpMergeFieldValue.Value = mailChimpMergeField.Tag;
            mailChimpMergeFieldValue.Description = mailChimpMergeField.Name;

            var mailChimpListAttriubte = AttributeCache.Get( MailChimp.SystemGuid.Attribute.MAIL_CHIMP_MERGE_FIELD_AUDIENCE_ATTRIBUTE );

            Rock.Attribute.Helper.SaveAttributeValue( mailChimpMergeFieldValue, mailChimpListAttriubte, mailChimpListValueGuid.ToString(), rockContext );

            rockContext.SaveChanges();

        }

        private Person GetRockPerson( MCModels.Member member, MailChimpSyncSettings mailchimpSyncSettings )
        {
            Person person = null;
            RockContext rockContext = new RockContext();
            PersonService personService = new PersonService( rockContext );

            var firstName = member.MergeFields["FNAME"].ToString().Left( 50 );
            var lastName = member.MergeFields["LNAME"].ToString().Left( 50 );
            var email = member.EmailAddress;
            var mailchimpForeignKey = String.Format( "Mailchimp_{0}", member.Id );

            string emailNote = null;
            bool isEmailActive = GetIsEmailActive( member.Status, out emailNote );

            // Check if there's a person in the DB who has already been created with a foreign key.  This will only match people added via the mail chimp plugin.
            person = personService.Queryable().AsNoTracking().Where( p => p.ForeignKey == mailchimpForeignKey ).FirstOrDefault();

            if ( person == null )
            {
                var personQuery = new PersonService.PersonMatchQuery( firstName, lastName, email, null, null, null, null, null );
                // Use find persons vs find person because if there are multile matches, we'll just use the first match vs creating a new person.
                person = personService.FindPersons( personQuery, false ).OrderBy( p => p.Id ).FirstOrDefault();
            }

            if ( person == null )
            {
                person = personService.Queryable().AsNoTracking().Where( p => p.Email == email ).OrderBy( p => p.Id ).FirstOrDefault();
            }


            if ( person == null && mailchimpSyncSettings.MailChimpToRockSettings.Contains( SyncPrivileges.AddNewRecord ) )
            {
                // Add New Person
                person = new Person();
                person.FirstName = firstName.FixCase();
                person.LastName = lastName.FixCase();
                person.IsEmailActive = isEmailActive;
                person.Email = email;
                person.EmailPreference = GetRockEmailPrefernce( member.Status );
                person.EmailNote = emailNote;
                person.RecordTypeValueId = DefinedValueCache.Get( Rock.SystemGuid.DefinedValue.PERSON_RECORD_TYPE_PERSON.AsGuid() ).Id;
                person.ForeignKey = mailchimpForeignKey;

                if ( !person.Email.IsValidEmail() )
                {
                    ExceptionLogService.LogException( new Exception( "Could not Add Mailchimp Member because their email address isn't valid(" + person.Email + ")" ) );
                    return null;
                }

                var familyGroup = PersonService.SaveNewPerson( person, rockContext, null, false );
                if ( familyGroup != null && familyGroup.Members.Any() )
                {
                    person = familyGroup.Members.Select( m => m.Person ).First();
                }

            }

            rockContext.SaveChanges();


            return person;
        }

        private int SyncPerson( int personId
            , MCModels.Member mailChimpMember
            , string mailChimpListId
            , List<int?> groupIds
            , MailChimpSyncSettings mailchimpSyncSettings
            , Dictionary<int, DefinedValue> mailChimpTags = null )
        {
            var rockContext = new RockContext();
            var personService = new PersonService( rockContext );
            var groupService = new GroupService( rockContext );
            var definedValueService = new DefinedValueService( rockContext );
            var groupMemberService = new GroupMemberService( rockContext );
            var recordsUpdated = 0;

            string oldFirstName = "";
            string oldLastName = "";
            MCModels.Status oldStatus = MCModels.Status.Undefined;

            var person = personService.Get( personId );
            var groupMembers = groupMemberService.Queryable()
                                    .Where( m => m.PersonId == personId && groupIds.Contains( m.GroupId ) )
                                    .OrderByDescending( m => m.ModifiedDateTime )
                                    .ToList();

            if ( mailchimpSyncSettings.MailChimpToRockSettings.Contains( SyncPrivileges.AddRecordToList ) )
            {

                var groupsToAddMembers = groupIds.Where( g => !groupMembers.Any( m => m.GroupId == g.Value ) );
                if ( groupsToAddMembers.Any() )
                {
                    foreach ( var groupId in groupsToAddMembers )
                    {
                        var group = groupService.Get( groupId.Value );
                        try
                        {
                            var groupMember = new GroupMember { PersonId = personId, GroupId = group.Id };
                            groupMember.GroupRoleId = group.GroupType.DefaultGroupRoleId ?? group.GroupType.Roles.First().Id;
                            groupMemberService.Add( groupMember );

                            if ( groupMembers.Any() )
                            {
                                groupMember.GroupMemberStatus = groupMembers.FirstOrDefault().GroupMemberStatus;
                            }
                            else if ( mailChimpMember != null )
                            {
                                groupMember.GroupMemberStatus = GetRockGroupMemberStatus( mailChimpMember.Status );
                            }
                            else
                            {
                                groupMember.GroupMemberStatus = GroupMemberStatus.Active;
                            }

                            groupMembers.Add( groupMember );
                            //groupMember.GroupMemberStatus = GetRockGroupMemberStatus( member.Status );

                        }
                        catch ( Exception ex )
                        {
                            string message = String.Format( "Error Adding Person #{0} to Group '{1}'", personId, group.Name );
                            ExceptionLogService.LogException( new Exception( message, ex ) );
                        }
                    }
                }
            }

            var isNewMailChimpRecord = false;
            if ( mailChimpMember == null )
            {
                isNewMailChimpRecord = true;
                if ( mailchimpSyncSettings.RockToMailChimpSettings.Contains( SyncPrivileges.AddNewRecord ) )
                {
                    if ( person.Email.IsNotNullOrWhiteSpace() )
                    {
                        mailChimpMember = new MCModels.Member
                        {
                            EmailAddress = person.Email,
                            Status = GetMailChimpMemberStatus( groupMembers.FirstOrDefault() ),
                            StatusIfNew = GetMailChimpMemberStatus( groupMembers.FirstOrDefault() )
                        };
                    }
                    else
                    {
                        Exception ex = new Exception( person.FullName + " was not synced to Mail Chimp because they do not have an email address" );
                        ExceptionLogService.LogException( ex );
                    }

                }
            }
            else
            {
                // Set the orginal values on the MailChimp Member to compare to later to see if there are any changes
                oldFirstName = mailChimpMember.MergeFields.ContainsKey( "FNAME" ) ? mailChimpMember.MergeFields["FNAME"].ToString().Left( 50 ) : "";
                oldLastName = mailChimpMember.MergeFields.ContainsKey( "LNAME" ) ? mailChimpMember.MergeFields["LNAME"].ToString().Left( 50 ) : "";
                oldStatus = mailChimpMember.Status;

                // Set the MailChimpMember's LastChanged time to a nullable datetime to compare with the Group Member's Modified Date Time
                var lastChanged = string.IsNullOrEmpty( mailChimpMember.LastChanged ) ? ( DateTime? ) null : DateTime.Parse( mailChimpMember.LastChanged );

                if ( mailchimpSyncSettings.RockToMailChimpSettings.Contains( SyncPrivileges.UpdateExistingRecord ) )
                {
                    if ( !mailChimpMember.EmailAddress.Equals( person.Email, StringComparison.OrdinalIgnoreCase ) )
                    {
                        mailChimpMember.EmailAddress = person.Email;
                    }

                    // If the Group Member has been modified more recently, use it's subscription status.
                    if ( groupMembers.FirstOrDefault() != null && groupMembers.FirstOrDefault().ModifiedDateTime > lastChanged )
                    {
                        mailChimpMember.Status = GetMailChimpMemberStatus( groupMembers.FirstOrDefault() );
                    }

                }

                if ( mailchimpSyncSettings.MailChimpToRockSettings.Contains( SyncPrivileges.UpdateExistingRecord ) )
                {
                    // If the Email Addresses Match, Check the Mail Chimp's Email Status to Update the Rock record.
                    // There's a chance they won't match, because Rock matches on Search Keys which could have contained the old email address.
                    // If They Don't Match, Update Mail Chimp to the Rock Person's Email Address
                    if ( mailChimpMember.EmailAddress.Equals( person.Email, StringComparison.OrdinalIgnoreCase ) )
                    {
                        string emailNote;
                        person.EmailPreference = GetRockEmailPrefernce( mailChimpMember.Status );
                        person.IsEmailActive = GetIsEmailActive( mailChimpMember.Status, out emailNote );
                        person.EmailNote = emailNote;
                    }

                    // Update each Group member's Status From the Mailchimp Member's Status
                    foreach ( var member in groupMembers )
                    {
                        member.GroupMemberStatus = GetRockGroupMemberStatus( mailChimpMember.Status );
                    }
                }
            }

            try
            {
                var tagAttribute = AttributeCache.Get( SystemGuid.Attribute.PERSON_MAIL_CHIMP_TAGS.AsGuid() );
                if ( mailchimpSyncSettings.MailChimpToRockSettings.Contains( SyncPrivileges.ImportTags ) &&
                    tagAttribute != null &&
                    _mailChimpAccount != null &&
                    mailChimpMember != null &&
                    mailChimpListId.IsNotNullOrWhiteSpace()
                    )
                {
                    List<TagResponse> tagList = new List<TagResponse>();
                    List<string> errorMessages = new List<string>();
                    if ( !MailchimpDirectApi.GetTagsForUser( _mailChimpAccount, mailChimpListId, mailChimpMember.Id, out tagList, errorMessages ) )
                    {
                        errorMessages.Add( String.Format( "Could not get mailchimp tags for Person #{0}", personId ) );
                        HandleErrorMessages( errorMessages );
                    }
                    else
                    {
                        var definedValueList = new List<DefinedValue>();
                        person.LoadAttributes();
                        foreach ( var tag in tagList )
                        {
                            DefinedValue definedValue = null;
                            if ( mailChimpTags == null )
                            {
                                mailChimpTags = new DefinedValueService( new RockContext() )
                                        .Queryable()
                                        .AsNoTracking()
                                        .Where( dv => dv.DefinedType.Guid == MailchimpTagDefinedTypeGuid && dv.ForeignId.HasValue )
                                        .ToDictionary( dv => dv.ForeignId.Value, dv => dv );
                            }
                            if ( mailChimpTags.ContainsKey( tag.Id ) )
                            {
                                definedValue = mailChimpTags[tag.Id];
                            }
                            if ( definedValue == null )
                            {
                                definedValue = definedValueService
                                    .Queryable()
                                    .AsNoTracking()
                                    .Where( dv => dv.ForeignId == tag.Id && dv.DefinedType.Guid == MailchimpTagDefinedTypeGuid )
                                    .FirstOrDefault();
                                if ( definedValue == null )
                                {
                                    try
                                    {
                                        var tagValue = new DefinedValue();
                                        tagValue.ForeignId = tag.Id;
                                        tagValue.ForeignKey = MailChimp.Constants.ForeignKey;
                                        tagValue.IsSystem = true;
                                        tagValue.DefinedTypeId = DefinedTypeCache.Get( MailchimpTagDefinedTypeGuid ).Id;
                                        tagValue.Value = tag.Name;
                                        definedValueService.Add( tagValue );
                                        rockContext.SaveChanges();
                                        definedValue = definedValueService.Get( tagValue.Guid );
                                    }
                                    catch ( Exception ex )
                                    {
                                        string message = String.Format( "Error Adding Tag {0} to Rock", tag.Name );
                                        ExceptionLogService.LogException( new Exception( message, ex ) );
                                    }
                                }
                                if ( definedValue != null && definedValue.ForeignId.HasValue )
                                {
                                    mailChimpTags.AddOrReplace( definedValue.ForeignId.Value, definedValue );
                                }
                            }
                            if ( definedValue != null )
                            {
                                definedValueList.Add( definedValue );
                            }
                        }
                        person.SetAttributeValue( tagAttribute.Key, definedValueList.Select( dv => dv.Guid ).ToList().AsDelimited( "," ) );
                        person.SaveAttributeValue( tagAttribute.Key, rockContext );
                    }
                }
            }
            catch ( Exception ex )
            {
                string message = String.Format( "Error Importing Tags to Rock" );
                ExceptionLogService.LogException( new Exception( message, ex ) );
            }

            // At this point, all the changes in Rock are done, so Save any changes
            recordsUpdated = rockContext.SaveChanges();

            try
            {
                if ( mailChimpMember != null )
                {
                    if (
                        ( mailchimpSyncSettings.RockToMailChimpSettings.Contains( SyncPrivileges.UpdateExistingRecord ) && !isNewMailChimpRecord ) ||
                        ( mailchimpSyncSettings.RockToMailChimpSettings.Contains( SyncPrivileges.AddNewRecord ) && isNewMailChimpRecord )
                        )
                    {
                        // Check to see if there are actually any changes in the record before pushing it to mailchimp
                        if ( oldFirstName != person.NickName || oldLastName != person.LastName || oldStatus != mailChimpMember.Status || UpdateMergeFields( ref mailChimpMember, groupMembers, person ) > 0 /* || UpdateAddress( ref mailChimpMember, person, rockContext ) */ )
                        {
                            mailChimpMember.MergeFields.AddOrReplace( "FNAME", person.NickName );
                            mailChimpMember.MergeFields.AddOrReplace( "LNAME", person.LastName );

                            var result = _mailChimpManager.Members.AddOrUpdateAsync( mailChimpListId, mailChimpMember ).Result;
                        }
                    }
                }
            }
            catch ( System.AggregateException e )
            {
                foreach ( var ex in e.InnerExceptions )
                {
                    if ( ex is MCNet.Core.MailChimpException )
                    {
                        var mailChimpException = ex as MCNet.Core.MailChimpException;
                        ExceptionLogService.LogException( new Exception( mailChimpException.Message + mailChimpException.Detail ) );
                    }
                    else
                    {
                        ExceptionLogService.LogException( ex );
                    }
                }
            }
            catch ( Exception ex )
            {
                ExceptionLogService.LogException( ex );
            }

            return recordsUpdated;
        }

        private EmailPreference GetRockEmailPrefernce( MCModels.Status mailChimpEmailStatus )
        {
            EmailPreference preference = EmailPreference.EmailAllowed;

            switch ( mailChimpEmailStatus )
            {
                case MCModels.Status.Unsubscribed:
                    preference = EmailPreference.NoMassEmails;
                    break;
                case MCModels.Status.Cleaned:
                    preference = EmailPreference.DoNotEmail;
                    break;
            }

            return preference;
        }

        private bool GetIsEmailActive( MCModels.Status mailChimpEmailStatus, out string emailNote )
        {
            bool isActive = true;
            emailNote = null;

            switch ( mailChimpEmailStatus )
            {
                case MCModels.Status.Cleaned:
                    isActive = false;
                    emailNote = "Email was marked as cleaned in Mail Chimp";
                    break;
            }

            return isActive;
        }

        private GroupMemberStatus GetRockGroupMemberStatus( MCModels.Status mailChimpEmailStatus )
        {
            GroupMemberStatus memberStatus = GroupMemberStatus.Inactive;

            switch ( mailChimpEmailStatus )
            {
                case MCModels.Status.Subscribed:
                    memberStatus = GroupMemberStatus.Active;
                    break;
                    //case MCModels.Status.Pending:
                    //    memberStatus = GroupMemberStatus.Pending;
                    //    break;
            }

            return memberStatus;
        }

        private MCModels.Status GetMailChimpMemberStatus( GroupMember groupMember )
        {
            MCModels.Status memberStatus = Status.Subscribed;

            if ( groupMember.IsArchived )
            {
                memberStatus = Status.Unsubscribed;
            }
            else
            {
                if ( groupMember.GroupMemberStatus == GroupMemberStatus.Inactive )
                {
                    memberStatus = Status.Unsubscribed;
                }
                else if ( groupMember.GroupMemberStatus == GroupMemberStatus.Pending )
                {
                    memberStatus = Status.Subscribed;
                }
            }

            return memberStatus;
        }

        private int UpdateMergeFields( ref MCModels.Member member, List<GroupMember> members, Person person )
        {
            int valuesUpdated = 0;

            RockContext rockContext = new RockContext();
            DefinedValueService definedValueService = new DefinedValueService( rockContext );

            var mergeFieldValues = definedValueService.GetByDefinedTypeGuid( MailChimp.SystemGuid.SystemDefinedTypes.MAIL_CHIMP_MERGE_FIELDS.AsGuid() );

            var mergeFields = new Dictionary<string, object>();
            mergeFields.Add( "Person", person );
            mergeFields.Add( "Members", members );

            foreach ( var definedValue in mergeFieldValues )
            {
                definedValue.LoadAttributes();

                var lava = definedValue.GetAttributeValue( AttributeCache.Get( MailChimp.SystemGuid.Attribute.MAIL_CHIMP_MERGE_FIELD_LAVA ).Key );
                var enabledLavaCommands = definedValue.GetAttributeValue( AttributeCache.Get( MailChimp.SystemGuid.Attribute.MAIL_CHIMP_MERGE_FIELD_ENABLED_LAVA_COMMANDS ).Key );

                if ( lava.IsNotNullOrWhiteSpace() )
                {
                    var newValue = lava.ResolveMergeFields( mergeFields, null, enabledLavaCommands );
                    if ( !member.MergeFields.ContainsKey( definedValue.Value ) || newValue != member.MergeFields[definedValue.Value].ToString() )
                    {
                        member.MergeFields.AddOrReplace( definedValue.Value, newValue );
                        valuesUpdated++;
                    }
                }
            }

            return valuesUpdated;
        }

        private bool UpdateAddress( ref MCModels.Member mailChimpMember, Person rockPerson, RockContext rockContext )
        {
            /* This code isn't working currently.  It needs to be adjusted.  The Address isn't actually updating. */
            bool addressUpdated = false;

            var personAddress = rockPerson.GetHomeLocation( rockContext );
            if ( personAddress != null )
            {
                var mcAddressJsonString = mailChimpMember.MergeFields["ADDRESS"].ToString();

                if ( mcAddressJsonString != null )
                {

                    MCModels.Address mcAddress = Newtonsoft.Json.JsonConvert.DeserializeObject<MCModels.Address>( mcAddressJsonString );

                    if ( mcAddress != null )
                    {
                        if ( mcAddress.Address1 != personAddress.Street1
                        || mcAddress.Address2 != personAddress.Street2
                        || mcAddress.City != personAddress.City
                        || mcAddress.Province != personAddress.State
                        || mcAddress.PostalCode != personAddress.PostalCode
                        || mcAddress.Country != personAddress.Country
                        )
                        {
                            addressUpdated = true;
                        }
                    }
                    else
                    {
                        addressUpdated = true;
                    }
                }

                if ( addressUpdated )
                {
                    var mcAddress = new MCModels.Address()
                    {
                        Address1 = personAddress.Street1,
                        Address2 = personAddress.Street2,
                        City = personAddress.City,
                        Province = personAddress.State,
                        PostalCode = personAddress.PostalCode,
                        Country = personAddress.Country
                    };

                    mailChimpMember.MergeFields.AddOrReplace( "ADDRESS", mcAddress.ToJson() );
                }

            }

            return addressUpdated;

        }
        private void HandleErrorMessages( List<string> errorMessages )
        {
            if ( errorMessages.Any() )
            {
                StringBuilder sb = new StringBuilder();
                sb.Append( errorMessages );
                ExceptionLogService.LogException( new Exception( sb.ToString() ) );
            }
        }

    }

    public class MailChimpSyncSettings
    {
        public int? DaysToSyncUpdates { get; set; }
        public List<SyncPrivileges> MailChimpToRockSettings { get; set; }
        public List<SyncPrivileges> RockToMailChimpSettings { get; set; }
    }

    public enum SyncPrivileges
    {
        AddNewRecord = 0,
        UpdateExistingRecord = 1,
        AddRecordToList = 2,
        ImportTags = 3
    }
}
