using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rock.Plugin;
using com.bemaservices.MailChimp.SystemGuid;
using Rock;
namespace com.bemaservices.MailChimp.Migrations
{
    [MigrationNumber( 5, "1.12.5" )]
    public class AddTags : Migration
    {
        /// <summary>
        /// The commands to run to migrate plugin to the specific version
        /// </summary>
        public override void Up()
        {
            // Add Server Prefix to Accounts
            var mailChimpAccountDefinedTypeId = SqlScalar( String.Format( "Select Top 1 Id from DefinedType Where Guid = '{0}'", SystemDefinedTypes.MAIL_CHIMP_ACCOUNTS ) ).ToStringSafe();
            RockMigrationHelper.AddDefinedTypeAttribute( SystemDefinedTypes.MAIL_CHIMP_ACCOUNTS, Rock.SystemGuid.FieldType.TEXT, "Server Prefix", "ServerPrefix", "The beginning xyz section in https://xyz.admin.mailchimp.com/ when logged into Mailchimp", 1033, true, "us19", false, true, SystemGuid.Attribute.MAIL_CHIMP_ACCOUNT_SERVER_PREFIX_ATTRIBUTE );
            // Add Tag Defined Type
            RockMigrationHelper.AddDefinedType( "Communication", "Mailchimp Tags", "", SystemDefinedTypes.MAIL_CHIMP_TAGS, @"" );
            // Add Person Attribute Category
            RockMigrationHelper.UpdatePersonAttributeCategory( "Mailchimp", "fa fa-envelope", "", "6286D41F-6AB3-41B5-9645-89E93483BF27" );
            // Add Person Attribute
            RockMigrationHelper.UpdatePersonAttribute( "59D5A94C-94A0-4630-B80A-BB25697D74C7", "6286D41F-6AB3-41B5-9645-89E93483BF27", "Mailchimp Tags", "MailchimpTags", "", "", 0, "", SystemGuid.Attribute.PERSON_MAIL_CHIMP_TAGS );
            RockMigrationHelper.UpdateAttributeQualifier( SystemGuid.Attribute.PERSON_MAIL_CHIMP_TAGS, "definedtype", @"109", "C993367B-0214-4CA8-8035-3F3509E4EBE4" );
            RockMigrationHelper.UpdateAttributeQualifier( SystemGuid.Attribute.PERSON_MAIL_CHIMP_TAGS, "allowmultiple", @"True", "C6CF9FB3-CE34-4EE5-A084-546D48C34DC5" );
            RockMigrationHelper.UpdateAttributeQualifier( SystemGuid.Attribute.PERSON_MAIL_CHIMP_TAGS, "displaydescription", @"False", "AC2091B0-396C-4629-A906-026BC90444A1" );
            RockMigrationHelper.UpdateAttributeQualifier( SystemGuid.Attribute.PERSON_MAIL_CHIMP_TAGS, "enhancedselection", @"True", "B415C43B-24EF-4B29-86B4-9BBD108F4A93" );
            RockMigrationHelper.UpdateAttributeQualifier( SystemGuid.Attribute.PERSON_MAIL_CHIMP_TAGS, "includeInactive", @"False", "A111750A-81C2-43D4-AD90-CF07BA9A3683" );
            RockMigrationHelper.UpdateAttributeQualifier( SystemGuid.Attribute.PERSON_MAIL_CHIMP_TAGS, "AllowAddingNewValues", @"False", "F2392739-C49D-42C2-B036-DACA6BCBC983" );
            RockMigrationHelper.UpdateAttributeQualifier( SystemGuid.Attribute.PERSON_MAIL_CHIMP_TAGS, "RepeatColumns", @"", "9C9AA79D-2F29-4100-8710-B08397DA739D" );
            // Add Job Attribute
            RockMigrationHelper.AddOrUpdateEntityAttribute( "Rock.Model.ServiceJob", "1EDAFDED-DFE6-4334-B019-6EECBA89E05A", "Class", "com.bemaservices.MailChimp.Jobs.MailChimpSync", "Import Mailchimp Tags?", "Import Mailchimp Tags?", @"Whether or not to include Mailchimp Tags", 0, @"False", "1993D785-D5A5-45C5-B668-520D723E2E75", "ImportMailChimpTags" );
        }
        /// <summary>
        /// The commands to undo a migration from a specific version
        /// </summary>
        public override void Down()
        {
            RockMigrationHelper.DeleteAttribute( com.bemaservices.MailChimp.SystemGuid.Attribute.MAIL_CHIMP_ACCOUNT_SERVER_PREFIX_ATTRIBUTE ); // Domain Prefix
        }
    }
}