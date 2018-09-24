﻿// ----------------------------------------------------------------------------------
//
// Copyright Microsoft Corporation
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ----------------------------------------------------------------------------------

namespace Microsoft.Azure.Commands.Network
{
    using AutoMapper;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Management.Automation;
    using System.Security;
    using Microsoft.Azure.Commands.Network.Models;
    using Microsoft.Azure.Commands.ResourceManager.Common.Tags;
    using Microsoft.Azure.Management.Network;
    using Microsoft.WindowsAzure.Commands.Common;
    using MNM = Microsoft.Azure.Management.Network.Models;
    using Microsoft.Azure.Commands.ResourceManager.Common.ArgumentCompleters;
    using Microsoft.Azure.Management.Internal.Resources.Utilities.Models;

    [Cmdlet("Update",
        ResourceManager.Common.AzureRMConstants.AzureRMPrefix + "VirtualWan",
        DefaultParameterSetName = CortexParameterSetNames.ByVirtualWanName,
        SupportsShouldProcess = true),
        OutputType(typeof(PSVirtualWan))]
    public class UpdateAzureRmVirtualWanCommand : VirtualWanBaseCmdlet
    {
        [Parameter(
            ParameterSetName = CortexParameterSetNames.ByVirtualWanName,
            Mandatory = true,
            HelpMessage = "The resource group name.")]
        [ResourceGroupCompleter]
        [ValidateNotNullOrEmpty]
        public string ResourceGroupName { get; set; }

        [Alias("ResourceName", "VirtualWanName")]
        [Parameter(
            ParameterSetName = CortexParameterSetNames.ByVirtualWanName,
            Mandatory = true,
            HelpMessage = "The resource name.")]
        [ValidateNotNullOrEmpty]
        public string Name { get; set; }

        [Alias("VirtualWan")]
        [Parameter(
            ParameterSetName = CortexParameterSetNames.ByVirtualWanObject,
            Mandatory = true,
            ValueFromPipeline = true,
            HelpMessage = "The virtual wan object to be modified")]
        [ValidateNotNullOrEmpty]
        public PSVirtualWan InputObject { get; set; }

        [Alias("VirtualWanId")]
        [Parameter(
            ParameterSetName = CortexParameterSetNames.ByVirtualWanResourceId,
            Mandatory = true,
            ValueFromPipelineByPropertyName = true,
            HelpMessage = "The Azure resource ID for the virtual wan.")]
        [ResourceIdCompleter("Microsoft.Network/virtualWan")]
        [ValidateNotNullOrEmpty]
        public string ResourceId { get; set; }

        [Parameter(
           Mandatory = false,
           HelpMessage = "The name of the selected security provider.")]
        public string SecurityProviderName { get; set; }

        [Parameter(
            Mandatory = false,
            HelpMessage = "Local breakout category for office 365 traffic.")]
        [ValidateSet(MNM.OfficeTrafficCategory.All, MNM.OfficeTrafficCategory.None, MNM.OfficeTrafficCategory.Optimize, MNM.OfficeTrafficCategory.OptimizeAndAllow)]
        public string Office365LocalBreakoutCategory { get; set; }

        [Parameter(
            Mandatory = false,
            HelpMessage = "Allow vnet to vnet traffic for VirtualWan.")]
        public bool? AllowVnetToVnetTraffic { get; set; }

        [Parameter(
            Mandatory = false,
            HelpMessage = "Allow branch to branch traffic for VirtualWan.")]
        public bool? AllowBranchToBranchTraffic { get; set; }

        [Parameter(
            Mandatory = false,
            ValueFromPipeline = true,
            HelpMessage = "The list of PSP2SVpnServerConfigurations that are associated with this VirtualWan.")]
        public PSP2SVpnServerConfiguration[] P2SVpnServerConfiguration { get; set; }

        [Parameter(
            Mandatory = false,
            HelpMessage = "A hashtable which represents resource tags.")]
        public Hashtable Tag { get; set; }

        [Parameter(
            Mandatory = false,
            HelpMessage = "Do not ask for confirmation if you want to overrite a resource")]
        public SwitchParameter Force { get; set; }

        [Parameter(
            Mandatory = false,
            HelpMessage = "Run cmdlet in the background")]
        public SwitchParameter AsJob { get; set; }

        public override void Execute()
        {
            base.Execute();

            ConfirmAction(
                Properties.Resources.SettingResourceMessage,
                this.Name,
                () =>
                {
                    WriteVerbose(String.Format(Properties.Resources.CreatingLongRunningOperationMessage, this.ResourceGroupName, this.Name));
                    WriteObject(this.UpdateVirtualWan());
                });
        }

        private PSVirtualWan UpdateVirtualWan()
        {
            PSVirtualWan virtualWanToUpdate = null;
            if (ParameterSetName.Equals(CortexParameterSetNames.ByVirtualWanObject, StringComparison.OrdinalIgnoreCase))
            {
                virtualWanToUpdate = this.InputObject;
                this.ResourceGroupName = this.InputObject.ResourceGroupName;
                this.Name = this.InputObject.Name;
            }
            else
            {
                if (ParameterSetName.Equals(CortexParameterSetNames.ByVirtualWanResourceId, StringComparison.OrdinalIgnoreCase))
                {
                    var parsedResourceId = new ResourceIdentifier(this.ResourceId);
                    this.Name = parsedResourceId.ResourceName;
                    this.ResourceGroupName = parsedResourceId.ResourceGroupName;
                }

                virtualWanToUpdate = GetVirtualWan(this.ResourceGroupName, this.Name);
            }

            if (virtualWanToUpdate == null)
            {
                throw new PSArgumentException(Properties.Resources.VirtualWanNotFound);
            }

            if (this.SecurityProviderName != null)
            {
                virtualWanToUpdate.SecurityProviderName = this.SecurityProviderName;
            }

            if (this.Office365LocalBreakoutCategory != null)
            {
                virtualWanToUpdate.Office365LocalBreakoutCategory = this.Office365LocalBreakoutCategory;
            }

            if (this.AllowBranchToBranchTraffic.HasValue)
            {
                virtualWanToUpdate.AllowBranchToBranchTraffic = this.AllowBranchToBranchTraffic.Value;
            }

            if (this.AllowVnetToVnetTraffic.HasValue)
            {
                virtualWanToUpdate.AllowVnetToVnetTraffic = this.AllowVnetToVnetTraffic.Value;
            }

            // Modify the P2SVpnServerConfigurations if present
            if (this.P2SVpnServerConfiguration != null && this.P2SVpnServerConfiguration.Length != 0)

            {
                virtualWanToUpdate.P2SVpnServerConfigurations = new List<PSP2SVpnServerConfiguration>(this.P2SVpnServerConfiguration);
            }

            var virtualWanModel = NetworkResourceManagerProfile.Mapper.Map<MNM.VirtualWAN>(virtualWanToUpdate);
            virtualWanModel.Tags = TagsConversionHelper.CreateTagDictionary(this.Tag, validate: true);

            this.VirtualWanClient.CreateOrUpdate(this.ResourceGroupName, this.Name, virtualWanModel);

            return this.GetVirtualWan(this.ResourceGroupName, this.Name);
        }
    }
}