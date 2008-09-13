/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSim Project nor the
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
using System.Data;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Drawing;
using OpenMetaverse;
using log4net;
using Mono.Data.SqliteClient;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Data.SQLite
{
    /// <summary>
    /// A RegionData Interface to the SQLite database
    /// </summary>
    public class SQLiteRegionData : IRegionDataStore
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private const string primSelect = "select * from prims";
        private const string shapeSelect = "select * from primshapes";
        private const string itemsSelect = "select * from primitems";
        private const string terrainSelect = "select * from terrain limit 1";
        private const string landSelect = "select * from land";
        private const string landAccessListSelect = "select distinct * from landaccesslist";
        private const string regionbanListSelect = "select * from regionban";
        private const string regionSettingsSelect = "select * from regionsettings";

        private DataSet ds;
        private SqliteDataAdapter primDa;
        private SqliteDataAdapter shapeDa;
        private SqliteDataAdapter itemsDa;
        private SqliteDataAdapter terrainDa;
        private SqliteDataAdapter landDa;
        private SqliteDataAdapter landAccessListDa;
        private SqliteDataAdapter regionSettingsDa;

        private SqliteConnection m_conn;

        private String m_connectionString;

        // Temporary attribute while this is experimental

        /***********************************************************************
         *
         *  Public Interface Functions
         *
         **********************************************************************/

        /// <summary>
        /// See IRegionDataStore
        /// <list type="bullet">
        /// <item>Initialises RegionData Interface</item>
        /// <item>Loads and initialises a new SQLite connection and maintains it.</item>
        /// </list>
        /// </summary>
        /// <param name="connectionString">the connection string</param>
        public void Initialise(string connectionString)
        {
            m_connectionString = connectionString;

            ds = new DataSet();

            m_log.Info("[REGION DB]: Sqlite - connecting: " + connectionString);
            m_conn = new SqliteConnection(m_connectionString);
            m_conn.Open();



            SqliteCommand primSelectCmd = new SqliteCommand(primSelect, m_conn);
            primDa = new SqliteDataAdapter(primSelectCmd);
            //            SqliteCommandBuilder primCb = new SqliteCommandBuilder(primDa);

            SqliteCommand shapeSelectCmd = new SqliteCommand(shapeSelect, m_conn);
            shapeDa = new SqliteDataAdapter(shapeSelectCmd);
            // SqliteCommandBuilder shapeCb = new SqliteCommandBuilder(shapeDa);

            SqliteCommand itemsSelectCmd = new SqliteCommand(itemsSelect, m_conn);
            itemsDa = new SqliteDataAdapter(itemsSelectCmd);

            SqliteCommand terrainSelectCmd = new SqliteCommand(terrainSelect, m_conn);
            terrainDa = new SqliteDataAdapter(terrainSelectCmd);

            SqliteCommand landSelectCmd = new SqliteCommand(landSelect, m_conn);
            landDa = new SqliteDataAdapter(landSelectCmd);

            SqliteCommand landAccessListSelectCmd = new SqliteCommand(landAccessListSelect, m_conn);
            landAccessListDa = new SqliteDataAdapter(landAccessListSelectCmd);

            SqliteCommand regionSettingsSelectCmd = new SqliteCommand(regionSettingsSelect, m_conn);
            regionSettingsDa = new SqliteDataAdapter(regionSettingsSelectCmd);
            // This actually does the roll forward assembly stuff
            Assembly assem = GetType().Assembly;
            Migration m = new Migration(m_conn, assem, "RegionStore");
            m.Update();

            lock (ds)
            {
                ds.Tables.Add(createPrimTable());
                setupPrimCommands(primDa, m_conn);
                primDa.Fill(ds.Tables["prims"]);

                ds.Tables.Add(createShapeTable());
                setupShapeCommands(shapeDa, m_conn);

                ds.Tables.Add(createItemsTable());
                setupItemsCommands(itemsDa, m_conn);
                itemsDa.Fill(ds.Tables["primitems"]);

                ds.Tables.Add(createTerrainTable());
                setupTerrainCommands(terrainDa, m_conn);

                ds.Tables.Add(createLandTable());
                setupLandCommands(landDa, m_conn);

                ds.Tables.Add(createLandAccessListTable());
                setupLandAccessCommands(landAccessListDa, m_conn);

                ds.Tables.Add(createRegionSettingsTable());
                
                setupRegionSettingsCommands(regionSettingsDa, m_conn);

                // WORKAROUND: This is a work around for sqlite on
                // windows, which gets really unhappy with blob columns
                // that have no sample data in them.  At some point we
                // need to actually find a proper way to handle this.
                try
                {
                    shapeDa.Fill(ds.Tables["primshapes"]);
                }
                catch (Exception)
                {
                    m_log.Info("[REGION DB]: Caught fill error on primshapes table");
                }

                try
                {
                    terrainDa.Fill(ds.Tables["terrain"]);
                }
                catch (Exception)
                {
                    m_log.Info("[REGION DB]: Caught fill error on terrain table");
                }

                try
                {
                    landDa.Fill(ds.Tables["land"]);
                }
                catch (Exception)
                {
                    m_log.Info("[REGION DB]: Caught fill error on land table");
                }

                try
                {
                    landAccessListDa.Fill(ds.Tables["landaccesslist"]);
                }
                catch (Exception)
                {
                    m_log.Info("[REGION DB]: Caught fill error on landaccesslist table");
                }

                try
                {
                    regionSettingsDa.Fill(ds.Tables["regionsettings"]);
                }
                catch (Exception)
                {
                    m_log.Info("[REGION DB]: Caught fill error on regionsettings table");
                }
                return;
            }
        }

        public void StoreRegionSettings(RegionSettings rs)
        {
            lock (ds)
            {
                DataTable regionsettings = ds.Tables["regionsettings"];

                DataRow settingsRow = regionsettings.Rows.Find(rs.RegionUUID.ToString());
                if (settingsRow == null)
                {
                    settingsRow = regionsettings.NewRow();
                    fillRegionSettingsRow(settingsRow, rs);
                    regionsettings.Rows.Add(settingsRow);
                }
                else
                {
                    fillRegionSettingsRow(settingsRow, rs);
                }

                Commit();
            }
        }

        public RegionSettings LoadRegionSettings(UUID regionUUID)
        {
            lock (ds)
            {
                DataTable regionsettings = ds.Tables["regionsettings"];

                string searchExp = "regionUUID = '" + regionUUID.ToString() + "'";
                DataRow[] rawsettings = regionsettings.Select(searchExp);
                if (rawsettings.Length == 0)
                {
                    RegionSettings rs = new RegionSettings();
                    rs.RegionUUID = regionUUID;
                    rs.OnSave += StoreRegionSettings;

                    StoreRegionSettings(rs);

                    return rs;
                }
                DataRow row = rawsettings[0];

                RegionSettings newSettings = buildRegionSettings(row);
                newSettings.OnSave += StoreRegionSettings;

                return newSettings;
            }
        }

        /// <summary>
        /// Adds an object into region storage
        /// </summary>
        /// <param name="obj">the object</param>
        /// <param name="regionUUID">the region UUID</param>
        public void StoreObject(SceneObjectGroup obj, UUID regionUUID)
        {
            lock (ds)
            {
                foreach (SceneObjectPart prim in obj.Children.Values)
                {
                    if ((prim.GetEffectiveObjectFlags() & (uint)PrimFlags.Physics) == 0
                        && (prim.GetEffectiveObjectFlags() & (uint)PrimFlags.Temporary) == 0
                        && (prim.GetEffectiveObjectFlags() & (uint)PrimFlags.TemporaryOnRez) == 0)
                    {
                        m_log.Info("[REGION DB]: Adding obj: " + obj.UUID + " to region: " + regionUUID);
                        addPrim(prim, obj.UUID, regionUUID);
                    }
                    else if (prim.Stopped)
                    {
                        //m_log.Info("[DATASTORE]: " +
                                                 //"Adding stopped obj: " + obj.UUID + " to region: " + regionUUID);
                        //addPrim(prim, Util.ToRawUuidString(obj.UUID), Util.ToRawUuidString(regionUUID));
                    }
                    else
                    {
                        // m_log.Info("[DATASTORE]: Ignoring Physical obj: " + obj.UUID + " in region: " + regionUUID);
                    }
                }
            }

            Commit();
            // m_log.Info("[Dump of prims]: " + ds.GetXml());
        }

        /// <summary>
        /// Removes an object from region storage
        /// </summary>
        /// <param name="obj">the object</param>
        /// <param name="regionUUID">the region UUID</param>
        public void RemoveObject(UUID obj, UUID regionUUID)
        {
            m_log.InfoFormat("[REGION DB]: Removing obj: {0} from region: {1}", obj.Guid, regionUUID);

            DataTable prims = ds.Tables["prims"];
            DataTable shapes = ds.Tables["primshapes"];

            string selectExp = "SceneGroupID = '" + Util.ToRawUuidString(obj) + "'";
            lock (ds)
            {
                DataRow[] primRows = prims.Select(selectExp);
                foreach (DataRow row in primRows)
                {
                    // Remove shape rows
                    UUID uuid = new UUID((string) row["UUID"]);
                    DataRow shapeRow = shapes.Rows.Find(Util.ToRawUuidString(uuid));
                    if (shapeRow != null)
                    {
                        shapeRow.Delete();
                    }

                    RemoveItems(uuid);

                    // Remove prim row
                    row.Delete();
                }
            }

            Commit();
        }

        /// <summary>
        /// Remove all persisted items of the given prim.
        /// The caller must acquire the necessrary synchronization locks and commit or rollback changes.
        /// </summary>
        /// <param name="uuid">The item UUID</param>
        private void RemoveItems(UUID uuid)
        {
            DataTable items = ds.Tables["primitems"];

            String sql = String.Format("primID = '{0}'", uuid);
            DataRow[] itemRows = items.Select(sql);

            foreach (DataRow itemRow in itemRows)
            {
                itemRow.Delete();
            }
        }

        /// <summary>
        /// Load persisted objects from region storage.
        /// </summary>
        /// <param name="regionUUID">The region UUID</param>
        /// <returns>List of loaded groups</returns>
        public List<SceneObjectGroup> LoadObjects(UUID regionUUID)
        {
            Dictionary<UUID, SceneObjectGroup> createdObjects = new Dictionary<UUID, SceneObjectGroup>();

            List<SceneObjectGroup> retvals = new List<SceneObjectGroup>();

            DataTable prims = ds.Tables["prims"];
            DataTable shapes = ds.Tables["primshapes"];

            string byRegion = "RegionUUID = '" + Util.ToRawUuidString(regionUUID) + "'";
            string orderByParent = "ParentID ASC";

            lock (ds)
            {
                DataRow[] primsForRegion = prims.Select(byRegion, orderByParent);
                m_log.Info("[REGION DB]: " +
                                         "Loaded " + primsForRegion.Length + " prims for region: " + regionUUID);

                foreach (DataRow primRow in primsForRegion)
                {
                    try
                    {
                        SceneObjectPart prim = null;

                        string uuid = (string) primRow["UUID"];
                        string objID = (string) primRow["SceneGroupID"];
                        if (uuid == objID) //is new SceneObjectGroup ?
                        {
                            SceneObjectGroup group = new SceneObjectGroup();
                            prim = buildPrim(primRow);
                            DataRow shapeRow = shapes.Rows.Find(Util.ToRawUuidString(prim.UUID));
                            if (shapeRow != null)
                            {
                                prim.Shape = buildShape(shapeRow);
                            }
                            else
                            {
                                m_log.Info(
                                    "[REGION DB]: No shape found for prim in storage, so setting default box shape");
                                prim.Shape = PrimitiveBaseShape.Default;
                            }
                            group.AddPart(prim);
                            group.RootPart = prim;

                            createdObjects.Add(Util.ToRawUuidString(group.UUID), group);
                            retvals.Add(group);
                        }
                        else
                        {
                            prim = buildPrim(primRow);
                            DataRow shapeRow = shapes.Rows.Find(Util.ToRawUuidString(prim.UUID));
                            if (shapeRow != null)
                            {
                                prim.Shape = buildShape(shapeRow);
                            }
                            else
                            {
                                m_log.Info(
                                    "[REGION DB]: No shape found for prim in storage, so setting default box shape");
                                prim.Shape = PrimitiveBaseShape.Default;
                            }
                            createdObjects[new UUID(objID)].AddPart(prim);
                        }

                        LoadItems(prim);
                    }
                    catch (Exception e)
                    {
                        m_log.Error("[REGION DB]: Failed create prim object, exception and data follows");
                        m_log.Info("[REGION DB]: " + e.ToString());
                        foreach (DataColumn col in prims.Columns)
                        {
                            m_log.Info("[REGION DB]: Col: " + col.ColumnName + " => " + primRow[col]);
                        }
                    }
                }
            }
            return retvals;
        }

        /// <summary>
        /// Load in a prim's persisted inventory.
        /// </summary>
        /// <param name="prim">the prim</param>
        private void LoadItems(SceneObjectPart prim)
        {
            //m_log.DebugFormat("[DATASTORE]: Loading inventory for {0}, {1}", prim.Name, prim.UUID);

            DataTable dbItems = ds.Tables["primitems"];

            String sql = String.Format("primID = '{0}'", prim.UUID.ToString());
            DataRow[] dbItemRows = dbItems.Select(sql);

            IList<TaskInventoryItem> inventory = new List<TaskInventoryItem>();

            foreach (DataRow row in dbItemRows)
            {
                TaskInventoryItem item = buildItem(row);
                inventory.Add(item);

                //m_log.DebugFormat("[DATASTORE]: Restored item {0}, {1}", item.Name, item.ItemID);
            }

            prim.RestoreInventoryItems(inventory);

            // XXX A nasty little hack to recover the folder id for the prim (which is currently stored in
            // every item).  This data should really be stored in the prim table itself.
            if (dbItemRows.Length > 0)
            {
                prim.FolderID = inventory[0].ParentID;
            }
        }

        /// <summary>
        /// Store a terrain revision in region storage
        /// </summary>
        /// <param name="ter">terrain heightfield</param>
        /// <param name="regionID">region UUID</param>
        public void StoreTerrain(double[,] ter, UUID regionID)
        {
            lock (ds)
            {
                int revision = Util.UnixTimeSinceEpoch();

                // the following is an work around for .NET.  The perf
                // issues associated with it aren't as bad as you think.
                m_log.Info("[REGION DB]: Storing terrain revision r" + revision.ToString());
                String sql = "insert into terrain(RegionUUID, Revision, Heightfield)" +
                             " values(:RegionUUID, :Revision, :Heightfield)";

                using (SqliteCommand cmd = new SqliteCommand(sql, m_conn))
                {
                    cmd.Parameters.Add(new SqliteParameter(":RegionUUID", Util.ToRawUuidString(regionID)));
                    cmd.Parameters.Add(new SqliteParameter(":Revision", revision));
                    cmd.Parameters.Add(new SqliteParameter(":Heightfield", serializeTerrain(ter)));
                    cmd.ExecuteNonQuery();
                }

                // This is added to get rid of the infinitely growing
                // terrain databases which negatively impact on SQLite
                // over time.  Before reenabling this feature there
                // needs to be a limitter put on the number of
                // revisions in the database, as this old
                // implementation is a DOS attack waiting to happen.

                using (
                    SqliteCommand cmd =
                        new SqliteCommand("delete from terrain where RegionUUID=:RegionUUID and Revision < :Revision",
                                          m_conn))
                {
                    cmd.Parameters.Add(new SqliteParameter(":RegionUUID", Util.ToRawUuidString(regionID)));
                    cmd.Parameters.Add(new SqliteParameter(":Revision", revision));
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Load the latest terrain revision from region storage
        /// </summary>
        /// <param name="regionID">the region UUID</param>
        /// <returns>Heightfield data</returns>
        public double[,] LoadTerrain(UUID regionID)
        {
            lock (ds)
            {
                double[,] terret = new double[256,256];
                terret.Initialize();

                String sql = "select RegionUUID, Revision, Heightfield from terrain" +
                             " where RegionUUID=:RegionUUID order by Revision desc";

                using (SqliteCommand cmd = new SqliteCommand(sql, m_conn))
                {
                    cmd.Parameters.Add(new SqliteParameter(":RegionUUID", Util.ToRawUuidString(regionID)));

                    using (IDataReader row = cmd.ExecuteReader())
                    {
                        int rev = 0;
                        if (row.Read())
                        {
                            // TODO: put this into a function
                            MemoryStream str = new MemoryStream((byte[]) row["Heightfield"]);
                            BinaryReader br = new BinaryReader(str);
                            for (int x = 0; x < 256; x++)
                            {
                                for (int y = 0; y < 256; y++)
                                {
                                    terret[x, y] = br.ReadDouble();
                                }
                            }
                            rev = (int) row["Revision"];
                        }
                        else
                        {
                            m_log.Info("[REGION DB]: No terrain found for region");
                            return null;
                        }

                        m_log.Info("[REGION DB]: Loaded terrain revision r" + rev.ToString());
                    }
                }
                return terret;
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="globalID"></param>
        public void RemoveLandObject(UUID globalID)
        {
            lock (ds)
            {
                using (SqliteCommand cmd = new SqliteCommand("delete from land where UUID=:UUID", m_conn))
                {
                    cmd.Parameters.Add(new SqliteParameter(":UUID", Util.ToRawUuidString(globalID)));
                    cmd.ExecuteNonQuery();
                }

                using (SqliteCommand cmd = new SqliteCommand("delete from landaccesslist where LandUUID=:UUID", m_conn))
                {
                    cmd.Parameters.Add(new SqliteParameter(":UUID", Util.ToRawUuidString(globalID)));
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="parcel"></param>
        public void StoreLandObject(ILandObject parcel)
        {
            lock (ds)
            {
                DataTable land = ds.Tables["land"];
                DataTable landaccesslist = ds.Tables["landaccesslist"];

                DataRow landRow = land.Rows.Find(Util.ToRawUuidString(parcel.landData.GlobalID));
                if (landRow == null)
                {
                    landRow = land.NewRow();
                    fillLandRow(landRow, parcel.landData, parcel.regionUUID);
                    land.Rows.Add(landRow);
                }
                else
                {
                    fillLandRow(landRow, parcel.landData, parcel.regionUUID);
                }

                // I know this caused someone issues before, but OpenSim is unusable if we leave this stuff around
                using (SqliteCommand cmd = new SqliteCommand("delete from landaccesslist where LandUUID=:LandUUID", m_conn))
                {
                    cmd.Parameters.Add(new SqliteParameter(":LandUUID", Util.ToRawUuidString(parcel.landData.GlobalID)));
                    cmd.ExecuteNonQuery();
                }

                foreach (ParcelManager.ParcelAccessEntry entry in parcel.landData.ParcelAccessList)
                {
                    DataRow newAccessRow = landaccesslist.NewRow();
                    fillLandAccessRow(newAccessRow, entry, parcel.landData.GlobalID);
                    landaccesslist.Rows.Add(newAccessRow);
                }
            }

            Commit();
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="regionUUID"></param>
        /// <returns></returns>
        public List<LandData> LoadLandObjects(UUID regionUUID)
        {
            List<LandData> landDataForRegion = new List<LandData>();
            lock (ds)
            {
                DataTable land = ds.Tables["land"];
                DataTable landaccesslist = ds.Tables["landaccesslist"];
                string searchExp = "RegionUUID = '" + Util.ToRawUuidString(regionUUID) + "'";
                DataRow[] rawDataForRegion = land.Select(searchExp);
                foreach (DataRow rawDataLand in rawDataForRegion)
                {
                    LandData newLand = buildLandData(rawDataLand);
                    string accessListSearchExp = "LandUUID = '" + Util.ToRawUuidString(newLand.GlobalID) + "'";
                    DataRow[] rawDataForLandAccessList = landaccesslist.Select(accessListSearchExp);
                    foreach (DataRow rawDataLandAccess in rawDataForLandAccessList)
                    {
                        newLand.ParcelAccessList.Add(buildLandAccessData(rawDataLandAccess));
                    }

                    landDataForRegion.Add(newLand);
                }
            }
            return landDataForRegion;
        }

        /// <summary>
        ///
        /// </summary>
        public void Commit()
        {
            lock (ds)
            {
                primDa.Update(ds, "prims");
                shapeDa.Update(ds, "primshapes");

                itemsDa.Update(ds, "primitems");

                terrainDa.Update(ds, "terrain");
                landDa.Update(ds, "land");
                landAccessListDa.Update(ds, "landaccesslist");
                regionSettingsDa.Update(ds, "regionsettings");
                ds.AcceptChanges();
            }
        }

        /// <summary>
        /// See <see cref="Commit"/>
        /// </summary>
        public void Shutdown()
        {
            Commit();
        }

        /***********************************************************************
         *
         *  Database Definition Functions
         *
         *  This should be db agnostic as we define them in ADO.NET terms
         *
         **********************************************************************/

        /// <summary>
        ///
        /// </summary>
        /// <param name="dt"></param>
        /// <param name="name"></param>
        /// <param name="type"></param>
        private static void createCol(DataTable dt, string name, Type type)
        {
            DataColumn col = new DataColumn(name, type);
            dt.Columns.Add(col);
        }

        /// <summary>
        /// Creates the "terrain" table
        /// </summary>
        /// <returns>terrain table DataTable</returns>
        private static DataTable createTerrainTable()
        {
            DataTable terrain = new DataTable("terrain");

            createCol(terrain, "RegionUUID", typeof (String));
            createCol(terrain, "Revision", typeof (Int32));
            createCol(terrain, "Heightfield", typeof (Byte[]));

            return terrain;
        }

        /// <summary>
        /// Creates the "prims" table
        /// </summary>
        /// <returns>prim table DataTable</returns>
        private static DataTable createPrimTable()
        {
            DataTable prims = new DataTable("prims");

            createCol(prims, "UUID", typeof (String));
            createCol(prims, "RegionUUID", typeof (String));
            createCol(prims, "ParentID", typeof (Int32));
            createCol(prims, "CreationDate", typeof (Int32));
            createCol(prims, "Name", typeof (String));
            createCol(prims, "SceneGroupID", typeof (String));
            // various text fields
            createCol(prims, "Text", typeof (String));
            createCol(prims, "ColorR", typeof (Int32));
            createCol(prims, "ColorG", typeof (Int32));
            createCol(prims, "ColorB", typeof (Int32));
            createCol(prims, "ColorA", typeof (Int32));
            createCol(prims, "Description", typeof (String));
            createCol(prims, "SitName", typeof (String));
            createCol(prims, "TouchName", typeof (String));
            // permissions
            createCol(prims, "ObjectFlags", typeof (Int32));
            createCol(prims, "CreatorID", typeof (String));
            createCol(prims, "OwnerID", typeof (String));
            createCol(prims, "GroupID", typeof (String));
            createCol(prims, "LastOwnerID", typeof (String));
            createCol(prims, "OwnerMask", typeof (Int32));
            createCol(prims, "NextOwnerMask", typeof (Int32));
            createCol(prims, "GroupMask", typeof (Int32));
            createCol(prims, "EveryoneMask", typeof (Int32));
            createCol(prims, "BaseMask", typeof (Int32));
            // vectors
            createCol(prims, "PositionX", typeof (Double));
            createCol(prims, "PositionY", typeof (Double));
            createCol(prims, "PositionZ", typeof (Double));
            createCol(prims, "GroupPositionX", typeof (Double));
            createCol(prims, "GroupPositionY", typeof (Double));
            createCol(prims, "GroupPositionZ", typeof (Double));
            createCol(prims, "VelocityX", typeof (Double));
            createCol(prims, "VelocityY", typeof (Double));
            createCol(prims, "VelocityZ", typeof (Double));
            createCol(prims, "AngularVelocityX", typeof (Double));
            createCol(prims, "AngularVelocityY", typeof (Double));
            createCol(prims, "AngularVelocityZ", typeof (Double));
            createCol(prims, "AccelerationX", typeof (Double));
            createCol(prims, "AccelerationY", typeof (Double));
            createCol(prims, "AccelerationZ", typeof (Double));
            // quaternions
            createCol(prims, "RotationX", typeof (Double));
            createCol(prims, "RotationY", typeof (Double));
            createCol(prims, "RotationZ", typeof (Double));
            createCol(prims, "RotationW", typeof (Double));

            // sit target
            createCol(prims, "SitTargetOffsetX", typeof (Double));
            createCol(prims, "SitTargetOffsetY", typeof (Double));
            createCol(prims, "SitTargetOffsetZ", typeof (Double));

            createCol(prims, "SitTargetOrientW", typeof (Double));
            createCol(prims, "SitTargetOrientX", typeof (Double));
            createCol(prims, "SitTargetOrientY", typeof (Double));
            createCol(prims, "SitTargetOrientZ", typeof (Double));

            // click action
            createCol(prims, "ClickAction", typeof (Byte));

            // Add in contraints
            prims.PrimaryKey = new DataColumn[] {prims.Columns["UUID"]};

            return prims;
        }

        /// <summary>
        /// Creates "primshapes" table
        /// </summary>
        /// <returns>shape table DataTable</returns>
        private static DataTable createShapeTable()
        {
            DataTable shapes = new DataTable("primshapes");
            createCol(shapes, "UUID", typeof (String));
            // shape is an enum
            createCol(shapes, "Shape", typeof (Int32));
            // vectors
            createCol(shapes, "ScaleX", typeof (Double));
            createCol(shapes, "ScaleY", typeof (Double));
            createCol(shapes, "ScaleZ", typeof (Double));
            // paths
            createCol(shapes, "PCode", typeof (Int32));
            createCol(shapes, "PathBegin", typeof (Int32));
            createCol(shapes, "PathEnd", typeof (Int32));
            createCol(shapes, "PathScaleX", typeof (Int32));
            createCol(shapes, "PathScaleY", typeof (Int32));
            createCol(shapes, "PathShearX", typeof (Int32));
            createCol(shapes, "PathShearY", typeof (Int32));
            createCol(shapes, "PathSkew", typeof (Int32));
            createCol(shapes, "PathCurve", typeof (Int32));
            createCol(shapes, "PathRadiusOffset", typeof (Int32));
            createCol(shapes, "PathRevolutions", typeof (Int32));
            createCol(shapes, "PathTaperX", typeof (Int32));
            createCol(shapes, "PathTaperY", typeof (Int32));
            createCol(shapes, "PathTwist", typeof (Int32));
            createCol(shapes, "PathTwistBegin", typeof (Int32));
            // profile
            createCol(shapes, "ProfileBegin", typeof (Int32));
            createCol(shapes, "ProfileEnd", typeof (Int32));
            createCol(shapes, "ProfileCurve", typeof (Int32));
            createCol(shapes, "ProfileHollow", typeof (Int32));
            createCol(shapes, "State", typeof(Int32));
            // text TODO: this isn't right, but I'm not sure the right
            // way to specify this as a blob atm
            createCol(shapes, "Texture", typeof (Byte[]));
            createCol(shapes, "ExtraParams", typeof (Byte[]));

            shapes.PrimaryKey = new DataColumn[] {shapes.Columns["UUID"]};

            return shapes;
        }

        /// <summary>
        /// creates "primitems" table
        /// </summary>
        /// <returns>item table DataTable</returns>
        private static DataTable createItemsTable()
        {
            DataTable items = new DataTable("primitems");

            createCol(items, "itemID", typeof (String));
            createCol(items, "primID", typeof (String));
            createCol(items, "assetID", typeof (String));
            createCol(items, "parentFolderID", typeof (String));

            createCol(items, "invType", typeof (Int32));
            createCol(items, "assetType", typeof (Int32));

            createCol(items, "name", typeof (String));
            createCol(items, "description", typeof (String));

            createCol(items, "creationDate", typeof (Int64));
            createCol(items, "creatorID", typeof (String));
            createCol(items, "ownerID", typeof (String));
            createCol(items, "lastOwnerID", typeof (String));
            createCol(items, "groupID", typeof (String));

            createCol(items, "nextPermissions", typeof (UInt32));
            createCol(items, "currentPermissions", typeof (UInt32));
            createCol(items, "basePermissions", typeof (UInt32));
            createCol(items, "everyonePermissions", typeof (UInt32));
            createCol(items, "groupPermissions", typeof (UInt32));
            createCol(items, "flags", typeof (UInt32));

            items.PrimaryKey = new DataColumn[] { items.Columns["itemID"] };

            return items;
        }

        /// <summary>
        /// Creates "land" table
        /// </summary>
        /// <returns>land table DataTable</returns>
        private static DataTable createLandTable()
        {
            DataTable land = new DataTable("land");
            createCol(land, "UUID", typeof (String));
            createCol(land, "RegionUUID", typeof (String));
            createCol(land, "LocalLandID", typeof (UInt32));

            // Bitmap is a byte[512]
            createCol(land, "Bitmap", typeof (Byte[]));

            createCol(land, "Name", typeof (String));
            createCol(land, "Desc", typeof (String));
            createCol(land, "OwnerUUID", typeof (String));
            createCol(land, "IsGroupOwned", typeof (Boolean));
            createCol(land, "Area", typeof (Int32));
            createCol(land, "AuctionID", typeof (Int32)); //Unemplemented
            createCol(land, "Category", typeof (Int32)); //Enum OpenMetaverse.Parcel.ParcelCategory
            createCol(land, "ClaimDate", typeof (Int32));
            createCol(land, "ClaimPrice", typeof (Int32));
            createCol(land, "GroupUUID", typeof (string));
            createCol(land, "SalePrice", typeof (Int32));
            createCol(land, "LandStatus", typeof (Int32)); //Enum. OpenMetaverse.Parcel.ParcelStatus
            createCol(land, "LandFlags", typeof (UInt32));
            createCol(land, "LandingType", typeof (Byte));
            createCol(land, "MediaAutoScale", typeof (Byte));
            createCol(land, "MediaTextureUUID", typeof (String));
            createCol(land, "MediaURL", typeof (String));
            createCol(land, "MusicURL", typeof (String));
            createCol(land, "PassHours", typeof (Double));
            createCol(land, "PassPrice", typeof (UInt32));
            createCol(land, "SnapshotUUID", typeof (String));
            createCol(land, "UserLocationX", typeof (Double));
            createCol(land, "UserLocationY", typeof (Double));
            createCol(land, "UserLocationZ", typeof (Double));
            createCol(land, "UserLookAtX", typeof (Double));
            createCol(land, "UserLookAtY", typeof (Double));
            createCol(land, "UserLookAtZ", typeof (Double));
            createCol(land, "AuthbuyerID", typeof(String));

            land.PrimaryKey = new DataColumn[] {land.Columns["UUID"]};

            return land;
        }

        /// <summary>
        /// create "landaccesslist" table
        /// </summary>
        /// <returns>Landacceslist DataTable</returns>
        private static DataTable createLandAccessListTable()
        {
            DataTable landaccess = new DataTable("landaccesslist");
            createCol(landaccess, "LandUUID", typeof (String));
            createCol(landaccess, "AccessUUID", typeof (String));
            createCol(landaccess, "Flags", typeof (UInt32));

            return landaccess;
        }

        private static DataTable createRegionSettingsTable()
        {
            DataTable regionsettings = new DataTable("regionsettings");
            createCol(regionsettings, "regionUUID", typeof(String));
            createCol(regionsettings, "block_terraform", typeof (Int32));
            createCol(regionsettings, "block_fly", typeof (Int32));
            createCol(regionsettings, "allow_damage", typeof (Int32));
            createCol(regionsettings, "restrict_pushing", typeof (Int32));
            createCol(regionsettings, "allow_land_resell", typeof (Int32));
            createCol(regionsettings, "allow_land_join_divide", typeof (Int32));
            createCol(regionsettings, "block_show_in_search", typeof (Int32));
            createCol(regionsettings, "agent_limit", typeof (Int32));
            createCol(regionsettings, "object_bonus", typeof (Double));
            createCol(regionsettings, "maturity", typeof (Int32));
            createCol(regionsettings, "disable_scripts", typeof (Int32));
            createCol(regionsettings, "disable_collisions", typeof (Int32));
            createCol(regionsettings, "disable_physics", typeof (Int32));
            createCol(regionsettings, "terrain_texture_1", typeof(String));
            createCol(regionsettings, "terrain_texture_2", typeof(String));
            createCol(regionsettings, "terrain_texture_3", typeof(String));
            createCol(regionsettings, "terrain_texture_4", typeof(String));
            createCol(regionsettings, "elevation_1_nw", typeof (Double));
            createCol(regionsettings, "elevation_2_nw", typeof (Double));
            createCol(regionsettings, "elevation_1_ne", typeof (Double));
            createCol(regionsettings, "elevation_2_ne", typeof (Double));
            createCol(regionsettings, "elevation_1_se", typeof (Double));
            createCol(regionsettings, "elevation_2_se", typeof (Double));
            createCol(regionsettings, "elevation_1_sw", typeof (Double));
            createCol(regionsettings, "elevation_2_sw", typeof (Double));
            createCol(regionsettings, "water_height", typeof (Double));
            createCol(regionsettings, "terrain_raise_limit", typeof (Double));
            createCol(regionsettings, "terrain_lower_limit", typeof (Double));
            createCol(regionsettings, "use_estate_sun", typeof (Int32));
            createCol(regionsettings, "sandbox", typeof (Int32));
            createCol(regionsettings, "fixed_sun", typeof (Int32));
            createCol(regionsettings, "sun_position", typeof (Double));
            createCol(regionsettings, "covenant", typeof(String));
            regionsettings.PrimaryKey = new DataColumn[] { regionsettings.Columns["regionUUID"] };
            return regionsettings;
        }

        /***********************************************************************
         *
         *  Convert between ADO.NET <=> OpenSim Objects
         *
         *  These should be database independant
         *
         **********************************************************************/

        /// <summary>
        ///
        /// </summary>
        /// <param name="row"></param>
        /// <returns></returns>
        private SceneObjectPart buildPrim(DataRow row)
        {
            // TODO: this doesn't work yet because something more
            // interesting has to be done to actually get these values
            // back out.  Not enough time to figure it out yet.
            SceneObjectPart prim = new SceneObjectPart();
            prim.UUID = new UUID((String) row["UUID"]);
            // explicit conversion of integers is required, which sort
            // of sucks.  No idea if there is a shortcut here or not.
            prim.ParentID = Convert.ToUInt32(row["ParentID"]);
            prim.CreationDate = Convert.ToInt32(row["CreationDate"]);
            prim.Name = (String) row["Name"];
            // various text fields
            prim.Text = (String) row["Text"];
            prim.Color = Color.FromArgb(Convert.ToInt32(row["ColorA"]),
                                        Convert.ToInt32(row["ColorR"]),
                                        Convert.ToInt32(row["ColorG"]),
                                        Convert.ToInt32(row["ColorB"]));
            prim.Description = (String) row["Description"];
            prim.SitName = (String) row["SitName"];
            prim.TouchName = (String) row["TouchName"];
            // permissions
            prim.ObjectFlags = Convert.ToUInt32(row["ObjectFlags"]);
            prim.CreatorID = new UUID((String) row["CreatorID"]);
            prim.OwnerID = new UUID((String) row["OwnerID"]);
            prim.GroupID = new UUID((String) row["GroupID"]);
            prim.LastOwnerID = new UUID((String) row["LastOwnerID"]);
            prim.OwnerMask = Convert.ToUInt32(row["OwnerMask"]);
            prim.NextOwnerMask = Convert.ToUInt32(row["NextOwnerMask"]);
            prim.GroupMask = Convert.ToUInt32(row["GroupMask"]);
            prim.EveryoneMask = Convert.ToUInt32(row["EveryoneMask"]);
            prim.BaseMask = Convert.ToUInt32(row["BaseMask"]);
            // vectors
            prim.OffsetPosition = new Vector3(
                Convert.ToSingle(row["PositionX"]),
                Convert.ToSingle(row["PositionY"]),
                Convert.ToSingle(row["PositionZ"])
                );
            prim.GroupPosition = new Vector3(
                Convert.ToSingle(row["GroupPositionX"]),
                Convert.ToSingle(row["GroupPositionY"]),
                Convert.ToSingle(row["GroupPositionZ"])
                );
            prim.Velocity = new Vector3(
                Convert.ToSingle(row["VelocityX"]),
                Convert.ToSingle(row["VelocityY"]),
                Convert.ToSingle(row["VelocityZ"])
                );
            prim.AngularVelocity = new Vector3(
                Convert.ToSingle(row["AngularVelocityX"]),
                Convert.ToSingle(row["AngularVelocityY"]),
                Convert.ToSingle(row["AngularVelocityZ"])
                );
            prim.Acceleration = new Vector3(
                Convert.ToSingle(row["AccelerationX"]),
                Convert.ToSingle(row["AccelerationY"]),
                Convert.ToSingle(row["AccelerationZ"])
                );
            // quaternions
            prim.RotationOffset = new Quaternion(
                Convert.ToSingle(row["RotationX"]),
                Convert.ToSingle(row["RotationY"]),
                Convert.ToSingle(row["RotationZ"]),
                Convert.ToSingle(row["RotationW"])
                );

            try
            {
                prim.SitTargetPositionLL = new Vector3(
                                                         Convert.ToSingle(row["SitTargetOffsetX"]),
                                                         Convert.ToSingle(row["SitTargetOffsetY"]),
                                                         Convert.ToSingle(row["SitTargetOffsetZ"]));
                prim.SitTargetOrientationLL = new Quaternion(
                                                               Convert.ToSingle(
                                                                                row["SitTargetOrientX"]),
                                                               Convert.ToSingle(
                                                                                row["SitTargetOrientY"]),
                                                               Convert.ToSingle(
                                                                                row["SitTargetOrientZ"]),
                                                               Convert.ToSingle(
                                                                                row["SitTargetOrientW"]));
            }
            catch (InvalidCastException)
            {
                // Database table was created before we got here and now has null values :P
                m_conn.Open();
                SqliteCommand cmd =
                    new SqliteCommand("ALTER TABLE prims ADD COLUMN SitTargetOffsetX float NOT NULL default 0;", m_conn);
                cmd.ExecuteNonQuery();
                cmd =
                    new SqliteCommand("ALTER TABLE prims ADD COLUMN SitTargetOffsetY float NOT NULL default 0;", m_conn);
                cmd.ExecuteNonQuery();
                cmd =
                    new SqliteCommand("ALTER TABLE prims ADD COLUMN SitTargetOffsetZ float NOT NULL default 0;", m_conn);
                cmd.ExecuteNonQuery();
                cmd =
                    new SqliteCommand("ALTER TABLE prims ADD COLUMN SitTargetOrientW float NOT NULL default 0;", m_conn);
                cmd.ExecuteNonQuery();
                cmd =
                    new SqliteCommand("ALTER TABLE prims ADD COLUMN SitTargetOrientX float NOT NULL default 0;", m_conn);
                cmd.ExecuteNonQuery();
                cmd =
                    new SqliteCommand("ALTER TABLE prims ADD COLUMN SitTargetOrientY float NOT NULL default 0;", m_conn);
                cmd.ExecuteNonQuery();
                cmd =
                    new SqliteCommand("ALTER TABLE prims ADD COLUMN SitTargetOrientZ float NOT NULL default 0;", m_conn);
                cmd.ExecuteNonQuery();
            }
            prim.Color = Color.FromArgb(Convert.ToInt32(row["ColorR"]), Convert.ToInt32(row["ColorB"]), Convert.ToInt32(row["ColorG"]));

            prim.ClickAction = Convert.ToByte(row["ClickAction"]);
            return prim;
        }

        /// <summary>
        /// Build a prim inventory item from the persisted data.
        /// </summary>
        /// <param name="row"></param>
        /// <returns></returns>
        private static TaskInventoryItem buildItem(DataRow row)
        {
            TaskInventoryItem taskItem = new TaskInventoryItem();

            taskItem.ItemID        = new UUID((String)row["itemID"]);
            taskItem.ParentPartID  = new UUID((String)row["primID"]);
            taskItem.AssetID       = new UUID((String)row["assetID"]);
            taskItem.ParentID      = new UUID((String)row["parentFolderID"]);

            taskItem.InvType       = Convert.ToInt32(row["invType"]);
            taskItem.Type          = Convert.ToInt32(row["assetType"]);

            taskItem.Name          = (String)row["name"];
            taskItem.Description   = (String)row["description"];
            taskItem.CreationDate  = Convert.ToUInt32(row["creationDate"]);
            taskItem.CreatorID     = new UUID((String)row["creatorID"]);
            taskItem.OwnerID       = new UUID((String)row["ownerID"]);
            taskItem.LastOwnerID   = new UUID((String)row["lastOwnerID"]);
            taskItem.GroupID       = new UUID((String)row["groupID"]);

            taskItem.NextPermissions = Convert.ToUInt32(row["nextPermissions"]);
            taskItem.CurrentPermissions     = Convert.ToUInt32(row["currentPermissions"]);
            taskItem.BasePermissions      = Convert.ToUInt32(row["basePermissions"]);
            taskItem.EveryonePermissions  = Convert.ToUInt32(row["everyonePermissions"]);
            taskItem.GroupPermissions     = Convert.ToUInt32(row["groupPermissions"]);
            taskItem.Flags         = Convert.ToUInt32(row["flags"]);

            return taskItem;
        }

        /// <summary>
        /// Build a Land Data from the persisted data.
        /// </summary>
        /// <param name="row"></param>
        /// <returns></returns>
        private LandData buildLandData(DataRow row)
        {
            LandData newData = new LandData();

            newData.GlobalID = new UUID((String) row["UUID"]);
            newData.LocalID = Convert.ToInt32(row["LocalLandID"]);

            // Bitmap is a byte[512]
            newData.Bitmap = (Byte[]) row["Bitmap"];

            newData.Name = (String) row["Name"];
            newData.Description = (String) row["Desc"];
            newData.OwnerID = (String) row["OwnerUUID"];
            newData.IsGroupOwned = (Boolean) row["IsGroupOwned"];
            newData.Area = Convert.ToInt32(row["Area"]);
            newData.AuctionID = Convert.ToUInt32(row["AuctionID"]); //Unemplemented
            newData.Category = (Parcel.ParcelCategory) Convert.ToInt32(row["Category"]);
                //Enum OpenMetaverse.Parcel.ParcelCategory
            newData.ClaimDate = Convert.ToInt32(row["ClaimDate"]);
            newData.ClaimPrice = Convert.ToInt32(row["ClaimPrice"]);
            newData.GroupID = new UUID((String) row["GroupUUID"]);
            newData.SalePrice = Convert.ToInt32(row["SalePrice"]);
            newData.Status = (Parcel.ParcelStatus) Convert.ToInt32(row["LandStatus"]);
                //Enum. OpenMetaverse.Parcel.ParcelStatus
            newData.Flags = Convert.ToUInt32(row["LandFlags"]);
            newData.LandingType = (Byte) row["LandingType"];
            newData.MediaAutoScale = (Byte) row["MediaAutoScale"];
            newData.MediaID = new UUID((String) row["MediaTextureUUID"]);
            newData.MediaURL = (String) row["MediaURL"];
            newData.MusicURL = (String) row["MusicURL"];
            newData.PassHours = Convert.ToSingle(row["PassHours"]);
            newData.PassPrice = Convert.ToInt32(row["PassPrice"]);
            newData.SnapshotID = (String) row["SnapshotUUID"];
            try
            {

                newData.UserLocation =
                    new Vector3(Convert.ToSingle(row["UserLocationX"]), Convert.ToSingle(row["UserLocationY"]),
                                  Convert.ToSingle(row["UserLocationZ"]));
                newData.UserLookAt =
                    new Vector3(Convert.ToSingle(row["UserLookAtX"]), Convert.ToSingle(row["UserLookAtY"]),
                                  Convert.ToSingle(row["UserLookAtZ"]));

            }
            catch (InvalidCastException)
            {
                m_log.ErrorFormat("[PARCEL]: unable to get parcel telehub settings for {1}", newData.Name);
                newData.UserLocation = Vector3.Zero;
                newData.UserLookAt = Vector3.Zero;
            }
            newData.ParcelAccessList = new List<ParcelManager.ParcelAccessEntry>();
            UUID authBuyerID = UUID.Zero;

            try
            {
                UUID.TryParse((string)row["AuthbuyerID"], out authBuyerID);
            }
            catch (InvalidCastException)
            {
                // Database table was created before we got here and now has null values :P
                try
                {
                    m_conn.Open();
                    SqliteCommand cmd =
                        new SqliteCommand("ALTER TABLE land ADD COLUMN AuthbuyerID varchar(36) NOT NULL default  '00000000-0000-0000-0000-000000000000';", m_conn);
                    cmd.ExecuteNonQuery();
                    m_conn.Close();
                    m_conn.Dispose();

                    m_log.Error("[REGION DB]: The land table was recently updated.  You need to restart the simulator.  Exiting now.");

                    Thread.Sleep(10000);

                    // ICK!  but it's better then A thousand red SQLITE error messages!
                    Environment.Exit(0);

                }
                catch (Exception)
                {
                    // ICK!  but it's better then A thousand red SQLITE error messages!
                    m_log.Error("[REGION DB]: The land table was recently updated.  You need to restart the simulator");
                    Environment.Exit(0);
                }
            }

            return newData;
        }

        private RegionSettings buildRegionSettings(DataRow row)
        {
            RegionSettings newSettings = new RegionSettings();

            newSettings.RegionUUID = new UUID((string) row["regionUUID"]);
            newSettings.BlockTerraform = Convert.ToBoolean(row["block_terraform"]);
            newSettings.AllowDamage = Convert.ToBoolean(row["allow_damage"]);
            newSettings.BlockFly = Convert.ToBoolean(row["block_fly"]);
            newSettings.RestrictPushing = Convert.ToBoolean(row["restrict_pushing"]);
            newSettings.AllowLandResell = Convert.ToBoolean(row["allow_land_resell"]);
            newSettings.AllowLandJoinDivide = Convert.ToBoolean(row["allow_land_join_divide"]);
            newSettings.BlockShowInSearch = Convert.ToBoolean(row["block_show_in_search"]);
            newSettings.AgentLimit = Convert.ToInt32(row["agent_limit"]);
            newSettings.ObjectBonus = Convert.ToDouble(row["object_bonus"]);
            newSettings.Maturity = Convert.ToInt32(row["maturity"]);
            newSettings.DisableScripts = Convert.ToBoolean(row["disable_scripts"]);
            newSettings.DisableCollisions = Convert.ToBoolean(row["disable_collisions"]);
            newSettings.DisablePhysics = Convert.ToBoolean(row["disable_physics"]);
            newSettings.TerrainTexture1 = new UUID((String) row["terrain_texture_1"]);
            newSettings.TerrainTexture2 = new UUID((String) row["terrain_texture_2"]);
            newSettings.TerrainTexture3 = new UUID((String) row["terrain_texture_3"]);
            newSettings.TerrainTexture4 = new UUID((String) row["terrain_texture_4"]);
            newSettings.Elevation1NW = Convert.ToDouble(row["elevation_1_nw"]);
            newSettings.Elevation2NW = Convert.ToDouble(row["elevation_2_nw"]);
            newSettings.Elevation1NE = Convert.ToDouble(row["elevation_1_ne"]);
            newSettings.Elevation2NE = Convert.ToDouble(row["elevation_2_ne"]);
            newSettings.Elevation1SE = Convert.ToDouble(row["elevation_1_se"]);
            newSettings.Elevation2SE = Convert.ToDouble(row["elevation_2_se"]);
            newSettings.Elevation1SW = Convert.ToDouble(row["elevation_1_sw"]);
            newSettings.Elevation2SW = Convert.ToDouble(row["elevation_2_sw"]);
            newSettings.WaterHeight = Convert.ToDouble(row["water_height"]);
            newSettings.TerrainRaiseLimit = Convert.ToDouble(row["terrain_raise_limit"]);
            newSettings.TerrainLowerLimit = Convert.ToDouble(row["terrain_lower_limit"]);
            newSettings.UseEstateSun = Convert.ToBoolean(row["use_estate_sun"]);
            newSettings.Sandbox = Convert.ToBoolean(row["sandbox"]);
            newSettings.FixedSun = Convert.ToBoolean(row["fixed_sun"]);
            newSettings.SunPosition = Convert.ToDouble(row["sun_position"]);
            newSettings.Covenant = new UUID((String) row["covenant"]);

            return newSettings;
        }

        /// <summary>
        /// Build a land access entry from the persisted data.
        /// </summary>
        /// <param name="row"></param>
        /// <returns></returns>
        private static ParcelManager.ParcelAccessEntry buildLandAccessData(DataRow row)
        {
            ParcelManager.ParcelAccessEntry entry = new ParcelManager.ParcelAccessEntry();
            entry.AgentID = new UUID((string) row["AccessUUID"]);
            entry.Flags = (ParcelManager.AccessList) row["Flags"];
            entry.Time = new DateTime();
            return entry;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="val"></param>
        /// <returns></returns>
        private static Array serializeTerrain(double[,] val)
        {
            MemoryStream str = new MemoryStream(65536*sizeof (double));
            BinaryWriter bw = new BinaryWriter(str);

            // TODO: COMPATIBILITY - Add byte-order conversions
            for (int x = 0; x < 256; x++)
                for (int y = 0; y < 256; y++)
                    bw.Write(val[x, y]);

            return str.ToArray();
        }

//         private void fillTerrainRow(DataRow row, UUID regionUUID, int rev, double[,] val)
//         {
//             row["RegionUUID"] = regionUUID;
//             row["Revision"] = rev;

//             MemoryStream str = new MemoryStream(65536*sizeof (double));
//             BinaryWriter bw = new BinaryWriter(str);

//             // TODO: COMPATIBILITY - Add byte-order conversions
//             for (int x = 0; x < 256; x++)
//                 for (int y = 0; y < 256; y++)
//                     bw.Write(val[x, y]);

//             row["Heightfield"] = str.ToArray();
//         }

        /// <summary>
        ///
        /// </summary>
        /// <param name="row"></param>
        /// <param name="prim"></param>
        /// <param name="sceneGroupID"></param>
        /// <param name="regionUUID"></param>
        private static void fillPrimRow(DataRow row, SceneObjectPart prim, UUID sceneGroupID, UUID regionUUID)
        {
            row["UUID"] = Util.ToRawUuidString(prim.UUID);
            row["RegionUUID"] = Util.ToRawUuidString(regionUUID);
            row["ParentID"] = prim.ParentID;
            row["CreationDate"] = prim.CreationDate;
            row["Name"] = prim.Name;
            row["SceneGroupID"] = Util.ToRawUuidString(sceneGroupID);
            // the UUID of the root part for this SceneObjectGroup
            // various text fields
            row["Text"] = prim.Text;
            row["Description"] = prim.Description;
            row["SitName"] = prim.SitName;
            row["TouchName"] = prim.TouchName;
            // permissions
            row["ObjectFlags"] = prim.ObjectFlags;
            row["CreatorID"] = Util.ToRawUuidString(prim.CreatorID);
            row["OwnerID"] = Util.ToRawUuidString(prim.OwnerID);
            row["GroupID"] = Util.ToRawUuidString(prim.GroupID);
            row["LastOwnerID"] = Util.ToRawUuidString(prim.LastOwnerID);
            row["OwnerMask"] = prim.OwnerMask;
            row["NextOwnerMask"] = prim.NextOwnerMask;
            row["GroupMask"] = prim.GroupMask;
            row["EveryoneMask"] = prim.EveryoneMask;
            row["BaseMask"] = prim.BaseMask;
            // vectors
            row["PositionX"] = prim.OffsetPosition.X;
            row["PositionY"] = prim.OffsetPosition.Y;
            row["PositionZ"] = prim.OffsetPosition.Z;
            row["GroupPositionX"] = prim.GroupPosition.X;
            row["GroupPositionY"] = prim.GroupPosition.Y;
            row["GroupPositionZ"] = prim.GroupPosition.Z;
            row["VelocityX"] = prim.Velocity.X;
            row["VelocityY"] = prim.Velocity.Y;
            row["VelocityZ"] = prim.Velocity.Z;
            row["AngularVelocityX"] = prim.AngularVelocity.X;
            row["AngularVelocityY"] = prim.AngularVelocity.Y;
            row["AngularVelocityZ"] = prim.AngularVelocity.Z;
            row["AccelerationX"] = prim.Acceleration.X;
            row["AccelerationY"] = prim.Acceleration.Y;
            row["AccelerationZ"] = prim.Acceleration.Z;
            // quaternions
            row["RotationX"] = prim.RotationOffset.X;
            row["RotationY"] = prim.RotationOffset.Y;
            row["RotationZ"] = prim.RotationOffset.Z;
            row["RotationW"] = prim.RotationOffset.W;

            // Sit target
            Vector3 sitTargetPos = prim.SitTargetPositionLL;
            row["SitTargetOffsetX"] = sitTargetPos.X;
            row["SitTargetOffsetY"] = sitTargetPos.Y;
            row["SitTargetOffsetZ"] = sitTargetPos.Z;

            Quaternion sitTargetOrient = prim.SitTargetOrientationLL;
            row["SitTargetOrientW"] = sitTargetOrient.W;
            row["SitTargetOrientX"] = sitTargetOrient.X;
            row["SitTargetOrientY"] = sitTargetOrient.Y;
            row["SitTargetOrientZ"] = sitTargetOrient.Z;
            row["ColorR"] = Convert.ToInt32(prim.Color.R);
            row["ColorG"] = Convert.ToInt32(prim.Color.G);
            row["ColorB"] = Convert.ToInt32(prim.Color.B);
            row["ColorA"] = Convert.ToInt32(prim.Color.A);

            // click action
            row["ClickAction"] = prim.ClickAction;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="row"></param>
        /// <param name="taskItem"></param>
        private static void fillItemRow(DataRow row, TaskInventoryItem taskItem)
        {
            row["itemID"] = taskItem.ItemID;
            row["primID"] = taskItem.ParentPartID;
            row["assetID"] = taskItem.AssetID;
            row["parentFolderID"] = taskItem.ParentID;

            row["invType"] = taskItem.InvType;
            row["assetType"] = taskItem.Type;

            row["name"] = taskItem.Name;
            row["description"] = taskItem.Description;
            row["creationDate"] = taskItem.CreationDate;
            row["creatorID"] = taskItem.CreatorID;
            row["ownerID"] = taskItem.OwnerID;
            row["lastOwnerID"] = taskItem.LastOwnerID;
            row["groupID"] = taskItem.GroupID;
            row["nextPermissions"] = taskItem.NextPermissions;
            row["currentPermissions"] = taskItem.CurrentPermissions;
            row["basePermissions"] = taskItem.BasePermissions;
            row["everyonePermissions"] = taskItem.EveryonePermissions;
            row["groupPermissions"] = taskItem.GroupPermissions;
            row["flags"] = taskItem.Flags;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="row"></param>
        /// <param name="land"></param>
        /// <param name="regionUUID"></param>
        private static void fillLandRow(DataRow row, LandData land, UUID regionUUID)
        {
            row["UUID"] = Util.ToRawUuidString(land.GlobalID);
            row["RegionUUID"] = Util.ToRawUuidString(regionUUID);
            row["LocalLandID"] = land.LocalID;

            // Bitmap is a byte[512]
            row["Bitmap"] = land.Bitmap;

            row["Name"] = land.Name;
            row["Desc"] = land.Description;
            row["OwnerUUID"] = Util.ToRawUuidString(land.OwnerID);
            row["IsGroupOwned"] = land.IsGroupOwned;
            row["Area"] = land.Area;
            row["AuctionID"] = land.AuctionID; //Unemplemented
            row["Category"] = land.Category; //Enum OpenMetaverse.Parcel.ParcelCategory
            row["ClaimDate"] = land.ClaimDate;
            row["ClaimPrice"] = land.ClaimPrice;
            row["GroupUUID"] = Util.ToRawUuidString(land.GroupID);
            row["SalePrice"] = land.SalePrice;
            row["LandStatus"] = land.Status; //Enum. OpenMetaverse.Parcel.ParcelStatus
            row["LandFlags"] = land.Flags;
            row["LandingType"] = land.LandingType;
            row["MediaAutoScale"] = land.MediaAutoScale;
            row["MediaTextureUUID"] = Util.ToRawUuidString(land.MediaID);
            row["MediaURL"] = land.MediaURL;
            row["MusicURL"] = land.MusicURL;
            row["PassHours"] = land.PassHours;
            row["PassPrice"] = land.PassPrice;
            row["SnapshotUUID"] = Util.ToRawUuidString(land.SnapshotID);
            row["UserLocationX"] = land.UserLocation.X;
            row["UserLocationY"] = land.UserLocation.Y;
            row["UserLocationZ"] = land.UserLocation.Z;
            row["UserLookAtX"] = land.UserLookAt.X;
            row["UserLookAtY"] = land.UserLookAt.Y;
            row["UserLookAtZ"] = land.UserLookAt.Z;
            row["AuthbuyerID"] = Util.ToRawUuidString(land.AuthBuyerID);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="row"></param>
        /// <param name="entry"></param>
        /// <param name="parcelID"></param>
        private static void fillLandAccessRow(DataRow row, ParcelManager.ParcelAccessEntry entry, UUID parcelID)
        {
            row["LandUUID"] = Util.ToRawUuidString(parcelID);
            row["AccessUUID"] = Util.ToRawUuidString(entry.AgentID);
            row["Flags"] = entry.Flags;
        }

        private static void fillRegionSettingsRow(DataRow row, RegionSettings settings)
        {
            row["regionUUID"] = settings.RegionUUID.ToString();
            row["block_terraform"] = settings.BlockTerraform;
            row["block_fly"] = settings.BlockFly;
            row["allow_damage"] = settings.AllowDamage;
            row["restrict_pushing"] = settings.RestrictPushing;
            row["allow_land_resell"] = settings.AllowLandResell;
            row["allow_land_join_divide"] = settings.AllowLandJoinDivide;
            row["block_show_in_search"] = settings.BlockShowInSearch;
            row["agent_limit"] = settings.AgentLimit;
            row["object_bonus"] = settings.ObjectBonus;
            row["maturity"] = settings.Maturity;
            row["disable_scripts"] = settings.DisableScripts;
            row["disable_collisions"] = settings.DisableCollisions;
            row["disable_physics"] = settings.DisablePhysics;
            row["terrain_texture_1"] = settings.TerrainTexture1.ToString();
            row["terrain_texture_2"] = settings.TerrainTexture2.ToString();
            row["terrain_texture_3"] = settings.TerrainTexture3.ToString();
            row["terrain_texture_4"] = settings.TerrainTexture4.ToString();
            row["elevation_1_nw"] = settings.Elevation1NW;
            row["elevation_2_nw"] = settings.Elevation2NW;
            row["elevation_1_ne"] = settings.Elevation1NE;
            row["elevation_2_ne"] = settings.Elevation2NE;
            row["elevation_1_se"] = settings.Elevation1SE;
            row["elevation_2_se"] = settings.Elevation2SE;
            row["elevation_1_sw"] = settings.Elevation1SW;
            row["elevation_2_sw"] = settings.Elevation2SW;
            row["water_height"] = settings.WaterHeight;
            row["terrain_raise_limit"] = settings.TerrainRaiseLimit;
            row["terrain_lower_limit"] = settings.TerrainLowerLimit;
            row["use_estate_sun"] = settings.UseEstateSun;
            row["sandbox"] = settings.Sandbox;
            row["fixed_sun"] = settings.FixedSun;
            row["sun_position"] = settings.SunPosition;
            row["covenant"] = settings.Covenant.ToString();
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="row"></param>
        /// <returns></returns>
        private PrimitiveBaseShape buildShape(DataRow row)
        {
            PrimitiveBaseShape s = new PrimitiveBaseShape();
            s.Scale = new Vector3(
                Convert.ToSingle(row["ScaleX"]),
                Convert.ToSingle(row["ScaleY"]),
                Convert.ToSingle(row["ScaleZ"])
                );
            // paths
            s.PCode = Convert.ToByte(row["PCode"]);
            s.PathBegin = Convert.ToUInt16(row["PathBegin"]);
            s.PathEnd = Convert.ToUInt16(row["PathEnd"]);
            s.PathScaleX = Convert.ToByte(row["PathScaleX"]);
            s.PathScaleY = Convert.ToByte(row["PathScaleY"]);
            s.PathShearX = Convert.ToByte(row["PathShearX"]);
            s.PathShearY = Convert.ToByte(row["PathShearY"]);
            s.PathSkew = Convert.ToSByte(row["PathSkew"]);
            s.PathCurve = Convert.ToByte(row["PathCurve"]);
            s.PathRadiusOffset = Convert.ToSByte(row["PathRadiusOffset"]);
            s.PathRevolutions = Convert.ToByte(row["PathRevolutions"]);
            s.PathTaperX = Convert.ToSByte(row["PathTaperX"]);
            s.PathTaperY = Convert.ToSByte(row["PathTaperY"]);
            s.PathTwist = Convert.ToSByte(row["PathTwist"]);
            s.PathTwistBegin = Convert.ToSByte(row["PathTwistBegin"]);
            // profile
            s.ProfileBegin = Convert.ToUInt16(row["ProfileBegin"]);
            s.ProfileEnd = Convert.ToUInt16(row["ProfileEnd"]);
            s.ProfileCurve = Convert.ToByte(row["ProfileCurve"]);
            s.ProfileHollow = Convert.ToUInt16(row["ProfileHollow"]);
            try
            {
                s.State = Convert.ToByte(row["State"]);
            }
            catch (InvalidCastException)
            {
                m_conn.Open();
                SqliteCommand cmd =
                    new SqliteCommand("ALTER TABLE primshapes ADD COLUMN State Integer NOT NULL default 0;", m_conn);
                cmd.ExecuteNonQuery();
            }
            // text TODO: this isn't right] = but I'm not sure the right
            // way to specify this as a blob atm

            byte[] textureEntry = (byte[]) row["Texture"];
            s.TextureEntry = textureEntry;

            s.ExtraParams = (byte[]) row["ExtraParams"];
            return s;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="row"></param>
        /// <param name="prim"></param>
        private static void fillShapeRow(DataRow row, SceneObjectPart prim)
        {
            PrimitiveBaseShape s = prim.Shape;
            row["UUID"] = Util.ToRawUuidString(prim.UUID);
            // shape is an enum
            row["Shape"] = 0;
            // vectors
            row["ScaleX"] = s.Scale.X;
            row["ScaleY"] = s.Scale.Y;
            row["ScaleZ"] = s.Scale.Z;
            // paths
            row["PCode"] = s.PCode;
            row["PathBegin"] = s.PathBegin;
            row["PathEnd"] = s.PathEnd;
            row["PathScaleX"] = s.PathScaleX;
            row["PathScaleY"] = s.PathScaleY;
            row["PathShearX"] = s.PathShearX;
            row["PathShearY"] = s.PathShearY;
            row["PathSkew"] = s.PathSkew;
            row["PathCurve"] = s.PathCurve;
            row["PathRadiusOffset"] = s.PathRadiusOffset;
            row["PathRevolutions"] = s.PathRevolutions;
            row["PathTaperX"] = s.PathTaperX;
            row["PathTaperY"] = s.PathTaperY;
            row["PathTwist"] = s.PathTwist;
            row["PathTwistBegin"] = s.PathTwistBegin;
            // profile
            row["ProfileBegin"] = s.ProfileBegin;
            row["ProfileEnd"] = s.ProfileEnd;
            row["ProfileCurve"] = s.ProfileCurve;
            row["ProfileHollow"] = s.ProfileHollow;
            row["State"] = s.State;

            row["Texture"] = s.TextureEntry;
            row["ExtraParams"] = s.ExtraParams;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="prim"></param>
        /// <param name="sceneGroupID"></param>
        /// <param name="regionUUID"></param>
        private void addPrim(SceneObjectPart prim, UUID sceneGroupID, UUID regionUUID)
        {
            DataTable prims = ds.Tables["prims"];
            DataTable shapes = ds.Tables["primshapes"];

            DataRow primRow = prims.Rows.Find(Util.ToRawUuidString(prim.UUID));
            if (primRow == null)
            {
                primRow = prims.NewRow();
                fillPrimRow(primRow, prim, sceneGroupID, regionUUID);
                prims.Rows.Add(primRow);
            }
            else
            {
                fillPrimRow(primRow, prim, sceneGroupID, regionUUID);
            }

            DataRow shapeRow = shapes.Rows.Find(Util.ToRawUuidString(prim.UUID));
            if (shapeRow == null)
            {
                shapeRow = shapes.NewRow();
                fillShapeRow(shapeRow, prim);
                shapes.Rows.Add(shapeRow);
            }
            else
            {
                fillShapeRow(shapeRow, prim);
            }
        }

        /// <summary>
        /// see IRegionDatastore
        /// </summary>
        /// <param name="primID"></param>
        /// <param name="items"></param>
        public void StorePrimInventory(UUID primID, ICollection<TaskInventoryItem> items)
        {
            m_log.InfoFormat("[REGION DB]: Entered StorePrimInventory with prim ID {0}", primID);

            DataTable dbItems = ds.Tables["primitems"];

            // For now, we're just going to crudely remove all the previous inventory items
            // no matter whether they have changed or not, and replace them with the current set.
            lock (ds)
            {
                RemoveItems(primID);

                // repalce with current inventory details
                foreach (TaskInventoryItem newItem in items)
                {
//                    m_log.InfoFormat(
//                        "[DATASTORE]: ",
//                        "Adding item {0}, {1} to prim ID {2}",
//                        newItem.Name, newItem.ItemID, newItem.ParentPartID);

                    DataRow newItemRow = dbItems.NewRow();
                    fillItemRow(newItemRow, newItem);
                    dbItems.Rows.Add(newItemRow);
                }
            }

            Commit();
        }

        /***********************************************************************
         *
         *  SQL Statement Creation Functions
         *
         *  These functions create SQL statements for update, insert, and create.
         *  They can probably be factored later to have a db independant
         *  portion and a db specific portion
         *
         **********************************************************************/

        /// <summary>
        /// Create an insert command
        /// </summary>
        /// <param name="table">table name</param>
        /// <param name="dt">data table</param>
        /// <returns>the created command</returns>
        /// <remarks>
        /// This is subtle enough to deserve some commentary.
        /// Instead of doing *lots* and *lots of hardcoded strings
        /// for database definitions we'll use the fact that
        /// realistically all insert statements look like "insert
        /// into A(b, c) values(:b, :c) on the parameterized query
        /// front.  If we just have a list of b, c, etc... we can
        /// generate these strings instead of typing them out.
        /// </remarks>
        private static SqliteCommand createInsertCommand(string table, DataTable dt)
        {
            string[] cols = new string[dt.Columns.Count];
            for (int i = 0; i < dt.Columns.Count; i++)
            {
                DataColumn col = dt.Columns[i];
                cols[i] = col.ColumnName;
            }

            string sql = "insert into " + table + "(";
            sql += String.Join(", ", cols);
            // important, the first ':' needs to be here, the rest get added in the join
            sql += ") values (:";
            sql += String.Join(", :", cols);
            sql += ")";
            SqliteCommand cmd = new SqliteCommand(sql);

            // this provides the binding for all our parameters, so
            // much less code than it used to be
            foreach (DataColumn col in dt.Columns)
            {
                cmd.Parameters.Add(createSqliteParameter(col.ColumnName, col.DataType));
            }
            return cmd;
        }


        /// <summary>
        /// create an update command
        /// </summary>
        /// <param name="table">table name</param>
        /// <param name="pk"></param>
        /// <param name="dt"></param>
        /// <returns>the created command</returns>
        private static SqliteCommand createUpdateCommand(string table, string pk, DataTable dt)
        {
            string sql = "update " + table + " set ";
            string subsql = String.Empty;
            foreach (DataColumn col in dt.Columns)
            {
                if (subsql.Length > 0)
                {
                    // a map function would rock so much here
                    subsql += ", ";
                }
                subsql += col.ColumnName + "= :" + col.ColumnName;
            }
            sql += subsql;
            sql += " where " + pk;
            SqliteCommand cmd = new SqliteCommand(sql);

            // this provides the binding for all our parameters, so
            // much less code than it used to be

            foreach (DataColumn col in dt.Columns)
            {
                cmd.Parameters.Add(createSqliteParameter(col.ColumnName, col.DataType));
            }
            return cmd;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="dt">Data Table</param>
        /// <returns></returns>
        // private static string defineTable(DataTable dt)
        // {
        //     string sql = "create table " + dt.TableName + "(";
        //     string subsql = String.Empty;
        //     foreach (DataColumn col in dt.Columns)
        //     {
        //         if (subsql.Length > 0)
        //         {
        //             // a map function would rock so much here
        //             subsql += ",\n";
        //         }
        //         subsql += col.ColumnName + " " + sqliteType(col.DataType);
        //         if (dt.PrimaryKey.Length > 0 && col == dt.PrimaryKey[0])
        //         {
        //             subsql += " primary key";
        //         }
        //     }
        //     sql += subsql;
        //     sql += ")";
        //     return sql;
        // }

        /***********************************************************************
         *
         *  Database Binding functions
         *
         *  These will be db specific due to typing, and minor differences
         *  in databases.
         *
         **********************************************************************/

        ///<summary>
        /// This is a convenience function that collapses 5 repetitive
        /// lines for defining SqliteParameters to 2 parameters:
        /// column name and database type.
        ///
        /// It assumes certain conventions like :param as the param
        /// name to replace in parametrized queries, and that source
        /// version is always current version, both of which are fine
        /// for us.
        ///</summary>
        ///<returns>a built sqlite parameter</returns>
        private static SqliteParameter createSqliteParameter(string name, Type type)
        {
            SqliteParameter param = new SqliteParameter();
            param.ParameterName = ":" + name;
            param.DbType = dbtypeFromType(type);
            param.SourceColumn = name;
            param.SourceVersion = DataRowVersion.Current;
            return param;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="da"></param>
        /// <param name="conn"></param>
        private void setupPrimCommands(SqliteDataAdapter da, SqliteConnection conn)
        {
            da.InsertCommand = createInsertCommand("prims", ds.Tables["prims"]);
            da.InsertCommand.Connection = conn;

            da.UpdateCommand = createUpdateCommand("prims", "UUID=:UUID", ds.Tables["prims"]);
            da.UpdateCommand.Connection = conn;

            SqliteCommand delete = new SqliteCommand("delete from prims where UUID = :UUID");
            delete.Parameters.Add(createSqliteParameter("UUID", typeof (String)));
            delete.Connection = conn;
            da.DeleteCommand = delete;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="da"></param>
        /// <param name="conn"></param>
        private void setupItemsCommands(SqliteDataAdapter da, SqliteConnection conn)
        {
            da.InsertCommand = createInsertCommand("primitems", ds.Tables["primitems"]);
            da.InsertCommand.Connection = conn;

            da.UpdateCommand = createUpdateCommand("primitems", "itemID = :itemID", ds.Tables["primitems"]);
            da.UpdateCommand.Connection = conn;

            SqliteCommand delete = new SqliteCommand("delete from primitems where itemID = :itemID");
            delete.Parameters.Add(createSqliteParameter("itemID", typeof (String)));
            delete.Connection = conn;
            da.DeleteCommand = delete;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="da"></param>
        /// <param name="conn"></param>
        private void setupTerrainCommands(SqliteDataAdapter da, SqliteConnection conn)
        {
            da.InsertCommand = createInsertCommand("terrain", ds.Tables["terrain"]);
            da.InsertCommand.Connection = conn;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="da"></param>
        /// <param name="conn"></param>
        private void setupLandCommands(SqliteDataAdapter da, SqliteConnection conn)
        {
            da.InsertCommand = createInsertCommand("land", ds.Tables["land"]);
            da.InsertCommand.Connection = conn;

            da.UpdateCommand = createUpdateCommand("land", "UUID=:UUID", ds.Tables["land"]);
            da.UpdateCommand.Connection = conn;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="da"></param>
        /// <param name="conn"></param>
        private void setupLandAccessCommands(SqliteDataAdapter da, SqliteConnection conn)
        {
            da.InsertCommand = createInsertCommand("landaccesslist", ds.Tables["landaccesslist"]);
            da.InsertCommand.Connection = conn;
        }

        private void setupRegionSettingsCommands(SqliteDataAdapter da, SqliteConnection conn)
        {
            da.InsertCommand = createInsertCommand("regionsettings", ds.Tables["regionsettings"]);
            da.InsertCommand.Connection = conn;
            da.UpdateCommand = createUpdateCommand("regionsettings", "regionUUID=:regionUUID", ds.Tables["regionsettings"]);
            da.UpdateCommand.Connection = conn;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="da"></param>
        /// <param name="conn"></param>
        private void setupShapeCommands(SqliteDataAdapter da, SqliteConnection conn)
        {
            da.InsertCommand = createInsertCommand("primshapes", ds.Tables["primshapes"]);
            da.InsertCommand.Connection = conn;

            da.UpdateCommand = createUpdateCommand("primshapes", "UUID=:UUID", ds.Tables["primshapes"]);
            da.UpdateCommand.Connection = conn;

            SqliteCommand delete = new SqliteCommand("delete from primshapes where UUID = :UUID");
            delete.Parameters.Add(createSqliteParameter("UUID", typeof (String)));
            delete.Connection = conn;
            da.DeleteCommand = delete;
        }

        /***********************************************************************
         *
         *  Type conversion functions
         *
         **********************************************************************/

        /// <summary>
        /// Type conversion function
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private static DbType dbtypeFromType(Type type)
        {
            if (type == typeof (String))
            {
                return DbType.String;
            }
            else if (type == typeof (Int32))
            {
                return DbType.Int32;
            }
            else if (type == typeof (Double))
            {
                return DbType.Double;
            }
            else if (type == typeof (Byte))
            {
                return DbType.Byte;
            }
            else if (type == typeof (Double))
            {
                return DbType.Double;
            }
            else if (type == typeof (Byte[]))
            {
                return DbType.Binary;
            }
            else
            {
                return DbType.String;
            }
        }

    }
}
