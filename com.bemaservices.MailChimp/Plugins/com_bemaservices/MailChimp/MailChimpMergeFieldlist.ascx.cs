﻿// <copyright>
// Copyright by the Spark Development Network
//
// Licensed under the Rock Community License (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.rockrms.com/license
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>
//
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Web.UI;
using System.Web.UI.WebControls;

using Rock;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;
using Rock.Web.UI.Controls;
using Rock.Attribute;
using Rock.Web.UI;
using Rock.Security;

namespace com.bemaservices.MailChimp
{
    /// <summary>
    /// Template block for developers to use to start a new block.
    /// </summary>
    [DisplayName( "Mail Chimp Merge Field List" )]
    [Category( "BEMA Services > MailChimp" )]
    [Description( "A Block to display the list of MailChimp audiences" )]
    public partial class MailChimpMergeFieldlist : RockBlock, ICustomGridColumns, ISecondaryBlock
    {
        #region Private Variables

        private DefinedType _definedType = null;

        #endregion

        #region Base Control Methods

        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.Init" /> event.
        /// </summary>
        /// <param name="e">An <see cref="T:System.EventArgs" /> object that contains the event data.</param>
        protected override void OnInit( EventArgs e )
        {
            base.OnInit( e );

            // this event gets fired after block settings are updated. it's nice to repaint the screen if these settings would alter it
            this.BlockUpdated += Block_BlockUpdated;
            this.AddConfigurationUpdateTrigger( upnlSettings );

            int definedTypeId = InitForDefinedType();

            _definedType = new DefinedTypeService( new RockContext() ).Get( definedTypeId );

            if ( _definedType != null )
            {
                gDefinedValues.DataKeyNames = new string[] { "Id" };
                // gDefinedValues.Actions.ShowAdd = true;
                //gDefinedValues.Actions.AddClick += gDefinedValues_Add;
                gDefinedValues.GridRebind += gDefinedValues_GridRebind;
                gDefinedValues.GridReorder += gDefinedValues_GridReorder;

                bool canAddEditDelete = false; //IsUserAuthorized( Authorization.EDIT );
                gDefinedValues.Actions.ShowAdd = canAddEditDelete;
                gDefinedValues.IsDeleteEnabled = canAddEditDelete;

                AddAttributeColumns();

                var deleteField = new DeleteField();
                gDefinedValues.Columns.Add( deleteField );
                deleteField.Click += gDefinedValues_Delete;

                modalValue.SaveClick += btnSaveValue_Click;
                modalValue.OnCancelScript = string.Format( "$('#{0}').val('');", hfDefinedValueId.ClientID );
            }
        }

        /// <summary>
        /// Initialize items for the grid based on the configured or given defined type.
        /// </summary>
        private int InitForDefinedType()
        {
            Guid definedTypeGuid;
            int definedTypeId = 0;

            // A configured defined type takes precedence over any definedTypeId param value that is passed in.
            if ( Guid.TryParse( com.bemaservices.MailChimp.SystemGuid.SystemDefinedTypes.MAIL_CHIMP_MERGE_FIELDS, out definedTypeGuid ) )
            {
                definedTypeId = DefinedTypeCache.Get( definedTypeGuid ).Id;
            }

            return definedTypeId;
        }

        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.Load" /> event.
        /// </summary>
        /// <param name="e">The <see cref="T:System.EventArgs" /> object that contains the event data.</param>
        protected override void OnLoad( EventArgs e )
        {
            base.OnLoad( e );

            if ( !Page.IsPostBack )
            {
                if ( _definedType != null )
                {
                    ShowDetail();
                }
                else
                {
                    pnlList.Visible = false;
                }
            }
        }

        #endregion

        #region Events

        /// <summary>
        /// Handles the BlockUpdated event of the control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void Block_BlockUpdated( object sender, EventArgs e )
        {
            InitForDefinedType();

            if ( _definedType != null )
            {
                ShowDetail();
            }
            else
            {
                pnlList.Visible = false;
            }
        }

        /// <summary>
        /// Handles the Add event of the gDefinedValues control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        protected void gDefinedValues_Add( object sender, EventArgs e )
        {
            gDefinedValues_ShowEdit( 0 );
        }

        /// <summary>
        /// Handles the Edit event of the gDefinedValues control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RowEventArgs" /> instance containing the event data.</param>
        protected void gDefinedValues_Edit( object sender, RowEventArgs e )
        {
            gDefinedValues_ShowEdit( e.RowKeyId );
        }

        /// <summary>
        /// Handles the Delete event of the gDefinedValues control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RowEventArgs" /> instance containing the event data.</param>
        protected void gDefinedValues_Delete( object sender, RowEventArgs e )
        {
            var rockContext = new RockContext();
            var definedValueService = new DefinedValueService( rockContext );

            DefinedValue value = definedValueService.Get( e.RowKeyId );

            if ( value != null )
            {
                string errorMessage;
                if ( !definedValueService.CanDelete( value, out errorMessage ) )
                {
                    mdGridWarningValues.Show( errorMessage, ModalAlertType.Information );
                    return;
                }

                definedValueService.Delete( value );
                rockContext.SaveChanges();
            }

            BindDefinedValuesGrid();
        }

        /// <summary>
        /// Handles the Click event of the btnSaveDefinedValue control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        protected void btnSaveValue_Click( object sender, EventArgs e )
        {
            DefinedValue definedValue;
            var rockContext = new RockContext();
            DefinedValueService definedValueService = new DefinedValueService( rockContext );

            int definedValueId = hfDefinedValueId.ValueAsInt();

            if ( definedValueId.Equals( 0 ) )
            {
                int definedTypeId = hfDefinedTypeId.ValueAsInt();
                definedValue = new DefinedValue { Id = 0 };
                definedValue.DefinedTypeId = definedTypeId;
                definedValue.IsSystem = false;

                var orders = definedValueService.Queryable()
                    .Where( d => d.DefinedTypeId == definedTypeId )
                    .Select( d => d.Order )
                    .ToList();

                definedValue.Order = orders.Any() ? orders.Max() + 1 : 0;
            }
            else
            {
                definedValue = definedValueService.Get( definedValueId );
            }

            definedValue.Value = tbValueName.Text;
            definedValue.Description = tbValueDescription.Text;
            definedValue.IsActive = cbValueActive.Checked;
            avcDefinedValueAttributes.GetEditValues( definedValue );

            if ( !Page.IsValid )
            {
                return;
            }

            if ( !definedValue.IsValid )
            {
                // Controls will render the error messages                    
                return;
            }

            rockContext.WrapTransaction( () =>
            {
                if ( definedValue.Id.Equals( 0 ) )
                {
                    definedValueService.Add( definedValue );
                }

                rockContext.SaveChanges();

                definedValue.SaveAttributeValues( rockContext );

            } );

            BindDefinedValuesGrid();

            hfDefinedValueId.Value = string.Empty;
            modalValue.Hide();
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Shows the detail.
        /// </summary>
        public void ShowDetail()
        {
            pnlList.Visible = true;

            hfDefinedTypeId.SetValue( _definedType.Id );
            BindDefinedValuesGrid();
        }

        /// <summary>
        /// Handles the GridRebind event of the gDefinedValues control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        protected void gDefinedValues_GridRebind( object sender, EventArgs e )
        {
            BindDefinedValuesGrid();
        }

        /// <summary>
        /// Handles the GridReorder event of the gDefinedValues control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="GridReorderEventArgs"/> instance containing the event data.</param>
        private void gDefinedValues_GridReorder( object sender, GridReorderEventArgs e )
        {
            int definedTypeId = hfDefinedTypeId.ValueAsInt();

            var rockContext = new RockContext();
            var definedValueService = new DefinedValueService( rockContext );
            var definedValues = definedValueService.Queryable().Where( a => a.DefinedTypeId == definedTypeId ).OrderBy( a => a.Order ).ThenBy( a => a.Value );
            var changedIds = definedValueService.Reorder( definedValues.ToList(), e.OldIndex, e.NewIndex );
            rockContext.SaveChanges();
            BindDefinedValuesGrid();
        }

        /// <summary>
        /// Binds the defined values grid.
        /// </summary>
        protected void AddAttributeColumns()
        {
            // Remove attribute columns
            foreach ( var column in gDefinedValues.Columns.OfType<AttributeField>().ToList() )
            {
                gDefinedValues.Columns.Remove( column );
            }

            if ( _definedType != null )
            {
                // Add attribute columns
                int entityTypeId = new DefinedValue().TypeId;
                string qualifier = _definedType.Id.ToString();
                foreach ( var attribute in new AttributeService( new RockContext() ).Queryable()
                    .Where( a =>
                        a.EntityTypeId == entityTypeId &&
                        a.IsGridColumn &&
                        a.EntityTypeQualifierColumn.Equals( "DefinedTypeId", StringComparison.OrdinalIgnoreCase ) &&
                        a.EntityTypeQualifierValue.Equals( qualifier ) )
                    .OrderBy( a => a.Order )
                    .ThenBy( a => a.Name ) )
                {
                    string dataFieldExpression = attribute.Key;
                    bool columnExists = gDefinedValues.Columns.OfType<AttributeField>().FirstOrDefault( a => a.DataField.Equals( dataFieldExpression ) ) != null;
                    if ( !columnExists )
                    {
                        AttributeField boundField = new AttributeField();
                        boundField.DataField = dataFieldExpression;
                        boundField.AttributeId = attribute.Id;
                        boundField.HeaderText = attribute.Name;

                        var attributeCache = Rock.Web.Cache.AttributeCache.Get( attribute.Id );
                        if ( attributeCache != null )
                        {
                            boundField.HeaderStyle.HorizontalAlign = attributeCache.FieldType.Field.AlignValue;
                            boundField.ItemStyle.HorizontalAlign = attributeCache.FieldType.Field.AlignValue;
                        }

                        gDefinedValues.Columns.Add( boundField );
                    }
                }
            }
        }

        /// <summary>
        /// Binds the defined values grid.
        /// </summary>
        protected void BindDefinedValuesGrid()
        {
            var mailChimpListValueCache = DefinedValueCache.Get( PageParameter( "ListId" ).AsInteger() );

            var mailChimpAccountValueCache = DefinedValueCache.Get( mailChimpListValueCache.GetAttributeValue( AttributeCache.Get( MailChimp.SystemGuid.Attribute.MAIL_CHIMP_AUDIENCE_ACCOUNT_ATTRIBUTE.AsGuid() ).Key ).AsGuid() );

            if ( _definedType != null && mailChimpListValueCache != null && mailChimpAccountValueCache != null )
            {
                Utility.MailChimpApi mailChimpApi = new Utility.MailChimpApi( mailChimpAccountValueCache );
                var mergeStatusFieldMessages = new List<string>();
                gDefinedValues.DataSource = mailChimpApi.GetMailChimpMergeFields( mailChimpListValueCache, out mergeStatusFieldMessages );
                gDefinedValues.DataBind();
            }
        }

        /// <summary>
        /// Shows the edit value.
        /// </summary>
        /// <param name="valueId">The value id.</param>
        protected void gDefinedValues_ShowEdit( int valueId )
        {
            ShowDefinedValueEdit( valueId );
        }

        private void ShowDefinedValueEdit( int valueId )
        {
            var definedType = DefinedTypeCache.Get( hfDefinedTypeId.ValueAsInt() );
            DefinedValue definedValue;

            modalValue.SubTitle = String.Format( "Id: {0}", valueId );

            if ( !valueId.Equals( 0 ) )
            {
                definedValue = new DefinedValueService( new RockContext() ).Get( valueId );
                if ( definedType != null )
                {
                    lActionTitleDefinedValue.Text = String.Format("Edit {0}", "value for " + definedType.Name );
                }
            }
            else
            {
                definedValue = new DefinedValue { Id = 0 };
                definedValue.DefinedTypeId = hfDefinedTypeId.ValueAsInt();
                if ( definedType != null )
                {
                    lActionTitleDefinedValue.Text = String.Format( "Edit {0}", "value for " + definedType.Name );
                }
            }


            hfDefinedValueId.SetValue( definedValue.Id );
            tbValueName.Text = definedValue.Value;
            tbValueDescription.Text = definedValue.Description;
            cbValueActive.Checked = definedValue.IsActive;

            avcDefinedValueAttributes.ValidationGroup = modalValue.ValidationGroup;
            avcDefinedValueAttributes.AddEditControls( definedValue );

            modalValue.Show();
        }

        #endregion

        #region ISecondaryBlock

        /// <summary>
        /// Sets the visible.
        /// </summary>
        /// <param name="visible">if set to <c>true</c> [visible].</param>
        public void SetVisible( bool visible )
        {
            pnlContent.Visible = visible;
        }

        #endregion
    }
}