/*
 * Copyright (c) Contributors, http://aurora-sim.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the Aurora-Sim Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using Aurora.Framework.Services;
using Aurora.Framework.Utilities;

namespace Aurora.DataManager.Migration.Migrators.Agent
{
    public class AgentMigrator_0 : Migrator
    {
        private static readonly List<SchemaDefinition> _schema = new List<SchemaDefinition>()
        {
            new SchemaDefinition("userdata",  
                new ColumnDefinition[]
                {
                    new ColumnDefinition {Name = "ID", Type = ColumnTypeDef.String45},
                    new ColumnDefinition {Name = "Key", Type = ColumnTypeDef.String50},
                    new ColumnDefinition {Name = "Value", Type = ColumnTypeDef.Text} 
                },
                new IndexDefinition[] 
                {
                    new IndexDefinition() { Fields = new string[] {"ID", "Key"}, Type = IndexType.Primary }
                }),
            new SchemaDefinition("userclassifieds",  
                new ColumnDefinition[]
                {
                    new ColumnDefinition {Name = "Name", Type = ColumnTypeDef.String50},
                    new ColumnDefinition {Name = "Category", Type = ColumnTypeDef.String50},
                    new ColumnDefinition {Name = "SimName", Type = ColumnTypeDef.String50},
                    new ColumnDefinition {Name = "OwnerUUID", Type = ColumnTypeDef.Char36},
                    new ColumnDefinition {Name = "ScopeID", Type = ColumnTypeDef.Char36DefaultZero},
                    new ColumnDefinition {Name = "ClassifiedUUID", Type = ColumnTypeDef.Char36},
                    new ColumnDefinition {Name = "Classified", Type = ColumnTypeDef.MediumText},
                    new ColumnDefinition {Name = "Price", Type = ColumnTypeDef.Integer11},
                    new ColumnDefinition {Name = "Keyword", Type = ColumnTypeDef.String128}
                },
                new IndexDefinition[] 
                {
                    new IndexDefinition() { Fields = new string[] {"ClassifiedUUID"}, Type = IndexType.Primary },
                    new IndexDefinition() { Fields = new string[] {"Name", "Category"}, Type = IndexType.Index },
                        new IndexDefinition() { Fields = new string[] {"OwnerUUID"}, Type = IndexType.Index },
                        new IndexDefinition() { Fields = new string[] {"Keyword"}, Type = IndexType.Index }
                }),
            new SchemaDefinition("userpicks",  
                new ColumnDefinition[]
                {
                    new ColumnDefinition {Name = "Name", Type = ColumnTypeDef.String50},
                    new ColumnDefinition {Name = "SimName", Type = ColumnTypeDef.String50},
                    new ColumnDefinition {Name = "OwnerUUID", Type = ColumnTypeDef.Char36},
                    new ColumnDefinition {Name = "PickUUID", Type = ColumnTypeDef.Char36},
                    new ColumnDefinition {Name = "Pick", Type = ColumnTypeDef.MediumText}
                },
                new IndexDefinition[] 
                {
                    new IndexDefinition() { Fields = new string[] {"PickUUID"}, Type = IndexType.Primary },
                    new IndexDefinition() { Fields = new string[] {"OwnerUUID"}, Type = IndexType.Index }
                }),
        };

        public AgentMigrator_0()
        {
            Version = new Version(0, 1, 0);
            MigrationName = "Agent";
            base.schema = _schema;
        }

        protected override void DoCreateDefaults(IDataConnector genericData)
        {
            EnsureAllTablesInSchemaExist(genericData);
        }

        protected override bool DoValidate(IDataConnector genericData)
        {
            return TestThatAllTablesValidate(genericData);
        }

        protected override void DoMigrate(IDataConnector genericData)
        {
            DoCreateDefaults(genericData);
        }

        protected override void DoPrepareRestorePoint(IDataConnector genericData)
        {
            CopyAllTablesToTempVersions(genericData);
        }

        public override void FinishedMigration(IDataConnector genericData)
        {
            QueryFilter filter = new QueryFilter();
            filter.andFilters["ClassifiedUUID"] = OpenMetaverse.UUID.Zero.ToString();
            genericData.Delete("userclassifieds", filter);
        }
    }
}