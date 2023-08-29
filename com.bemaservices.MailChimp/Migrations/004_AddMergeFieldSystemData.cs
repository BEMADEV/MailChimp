using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rock.Plugin;

using com.bemaservices.MailChimp.SystemGuid;

namespace com.bemaservices.MailChimp.Migrations
{
    [MigrationNumber( 4, "1.9.4" )]
    public class AddMergeFieldSystemData : Migration
    {
        /// <summary>
        /// The commands to run to migrate plugin to the specific version
        /// </summary>
        public override void Up()
        {
            RockMigrationHelper.AddDefinedType( "Communication", "MailChimp Merge Fields", "", com.bemaservices.MailChimp.SystemGuid.SystemDefinedTypes.MAIL_CHIMP_MERGE_FIELDS, @"" );
            var mailChimpMergeFieldDefinedTypeId = SqlScalar( String.Format( "Select Top 1 Id from DefinedType Where Guid = '{0}'", com.bemaservices.MailChimp.SystemGuid.SystemDefinedTypes.MAIL_CHIMP_MERGE_FIELDS ) ).ToString();
            RockMigrationHelper.AddDefinedTypeAttribute( com.bemaservices.MailChimp.SystemGuid.SystemDefinedTypes.MAIL_CHIMP_MERGE_FIELDS, "59D5A94C-94A0-4630-B80A-BB25697D74C7", "MailChimp Audience", "MailChimpAudience", "", 1034, false, "", false, true, com.bemaservices.MailChimp.SystemGuid.Attribute.MAIL_CHIMP_MERGE_FIELD_AUDIENCE_ATTRIBUTE );
            RockMigrationHelper.AddDefinedTypeAttribute( com.bemaservices.MailChimp.SystemGuid.SystemDefinedTypes.MAIL_CHIMP_MERGE_FIELDS, "1D0D3794-C210-48A8-8C68-3FBEC08A6BA5", "Lava Template", "LavaTemplate", "", 1035, true, "", false, false, com.bemaservices.MailChimp.SystemGuid.Attribute.MAIL_CHIMP_MERGE_FIELD_LAVA );
            RockMigrationHelper.AddDefinedTypeAttribute( com.bemaservices.MailChimp.SystemGuid.SystemDefinedTypes.MAIL_CHIMP_MERGE_FIELDS, "4BD9088F-5CC6-89B1-45FC-A2AAFFC7CC0D", "Enabled Lava Commands", "EnabledLavaCommands", "", 1036, false, "", false, false, com.bemaservices.MailChimp.SystemGuid.Attribute.MAIL_CHIMP_MERGE_FIELD_ENABLED_LAVA_COMMANDS );
            RockMigrationHelper.AddAttributeQualifier( com.bemaservices.MailChimp.SystemGuid.Attribute.MAIL_CHIMP_MERGE_FIELD_AUDIENCE_ATTRIBUTE, "allowmultiple", "False", "3944F235-6109-4401-A453-A34EF9F3C77E" );
            RockMigrationHelper.AddAttributeQualifier( com.bemaservices.MailChimp.SystemGuid.Attribute.MAIL_CHIMP_MERGE_FIELD_AUDIENCE_ATTRIBUTE, "definedtype", mailChimpMergeFieldDefinedTypeId, "9FF49383-ACCA-4548-9B54-A826EC1AA4DE" );
            RockMigrationHelper.AddAttributeQualifier( com.bemaservices.MailChimp.SystemGuid.Attribute.MAIL_CHIMP_MERGE_FIELD_AUDIENCE_ATTRIBUTE, "displaydescription", "False", "93E13E42-6FD4-4483-8650-184CD593DDA3" );
            RockMigrationHelper.AddAttributeQualifier( com.bemaservices.MailChimp.SystemGuid.Attribute.MAIL_CHIMP_MERGE_FIELD_AUDIENCE_ATTRIBUTE, "enhancedselection", "False", "2C77ADCC-7071-44E7-9030-AA67D6698FAD" );
            RockMigrationHelper.AddAttributeQualifier( com.bemaservices.MailChimp.SystemGuid.Attribute.MAIL_CHIMP_MERGE_FIELD_AUDIENCE_ATTRIBUTE, "includeInactive", "False", "8051B44C-2600-4AC6-9D09-9160A11080C1" );
            RockMigrationHelper.AddAttributeQualifier( com.bemaservices.MailChimp.SystemGuid.Attribute.MAIL_CHIMP_MERGE_FIELD_LAVA, "editorHeight", "300", "52C122A9-FF7E-4D80-B76C-68BD33CA727B" );
            RockMigrationHelper.AddAttributeQualifier( com.bemaservices.MailChimp.SystemGuid.Attribute.MAIL_CHIMP_MERGE_FIELD_LAVA, "editorMode", "3", "CC3AABC4-23C5-40DD-B3B9-508F7748F32A" );
            RockMigrationHelper.AddAttributeQualifier( com.bemaservices.MailChimp.SystemGuid.Attribute.MAIL_CHIMP_MERGE_FIELD_LAVA, "editorTheme", "0", "1A90B0F4-6A4C-4A4B-BEB3-4B71A454065D" );




            // Page: Audience Detail
            RockMigrationHelper.UpdateBlockType( "Mail Chimp Merge Field List", "A block to display the list of merge fields on a MailChimp Audience.", "~/Plugins/com_bemaservices/MailChimp/MailChimpMergeFieldList.ascx", "BEMA Services > MailChimp", "0153CFC5-74F6-4186-AE62-74FC1DA89D4F" );
            // Add Block to Page: Audience Detail, Site: Rock RMS
            RockMigrationHelper.AddBlock( true, "A784E99F-F27F-4ACC-87E9-F4B15FB70479", "", "0153CFC5-74F6-4186-AE62-74FC1DA89D4F", "Mail Chimp Merge Field List", "Main", "", "", 2, "267085D9-F973-4E2A-B06C-E6A43D09F5C5" );
            // Attrib for BlockType: Mail Chimp Group List:core.CustomGridColumnsConfig
            RockMigrationHelper.UpdateBlockTypeAttribute( "0153CFC5-74F6-4186-AE62-74FC1DA89D4F", "9C204CD0-1233-41C5-818A-C5DA439445AA", "core.CustomGridColumnsConfig", "core.CustomGridColumnsConfig", "", "", 0, @"", "A38F5C92-B9E4-40B7-9B70-F069F3438516" );
            // Attrib for BlockType: Mail Chimp Group List:core.CustomGridEnableStickyHeaders
            RockMigrationHelper.UpdateBlockTypeAttribute( "0153CFC5-74F6-4186-AE62-74FC1DA89D4F", "1EDAFDED-DFE6-4334-B019-6EECBA89E05A", "core.CustomGridEnableStickyHeaders", "core.CustomGridEnableStickyHeaders", "", "", 0, @"False", "E4267790-57CA-4168-9311-C25C975962DC" );

        }

        /// <summary>
        /// The commands to undo a migration from a specific version
        /// </summary>
        public override void Down()
        {

            RockMigrationHelper.DeleteBlock( "267085D9-F973-4E2A-B06C-E6A43D09F5C5" );
            RockMigrationHelper.DeleteBlockType( "0153CFC5-74F6-4186-AE62-74FC1DA89D4F" );

            RockMigrationHelper.DeleteAttribute( com.bemaservices.MailChimp.SystemGuid.Attribute.MAIL_CHIMP_MERGE_FIELD_AUDIENCE_ATTRIBUTE );
            RockMigrationHelper.DeleteAttribute( com.bemaservices.MailChimp.SystemGuid.Attribute.MAIL_CHIMP_MERGE_FIELD_LAVA );
            RockMigrationHelper.DeleteDefinedType( com.bemaservices.MailChimp.SystemGuid.SystemDefinedTypes.MAIL_CHIMP_AUDIENCES ); // MailChimp Accounts
        }
    }
}
