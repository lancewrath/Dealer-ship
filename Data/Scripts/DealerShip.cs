using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Components.BankingAndCurrency;
using VRage.ModAPI;
using VRage.Utils;

namespace Dealer_Ship.Data.Scripts
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class DealerShip : MySessionComponentBase
    {
        public static DealerShip main = null;
        public StationsData stations = new StationsData();
        bool bIsServer = false;
        bool bInitialized = false;
        public string stationDataFile = "StationData.xml";
        KnownStations knownStations = null;
        int STATIONTICK = 600;
        int currenTick = 0;

        public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
        {
            base.Init(sessionComponent);
            bIsServer = MyAPIGateway.Multiplayer.IsServer;
            
            main = this;
            stations = new StationsData();
            if (!bIsServer)
                return;

            if (!bInitialized)
            {
                Setup();
                SetCallbacks();
                bInitialized = true;
            }
        }

        public override void BeforeStart()
        {
            base.BeforeStart();
            if (!bIsServer)
                return;


        }

        public override void LoadData()
        {
            base.LoadData();
            if (MyAPIGateway.Utilities.FileExistsInWorldStorage(stationDataFile, typeof(KnownStations)))
            {
                var reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(stationDataFile, typeof(KnownStations));
                if (reader != null)
                {
                    string data = reader.ReadToEnd();
                    knownStations = MyAPIGateway.Utilities.SerializeFromXML<KnownStations>(data);
                    if(knownStations != null)
                    {
                        MyLog.Default.WriteLineAndConsole("Station Data Loaded");
                    }
                    

                }
            }
        }

        public override void SaveData()
        {
            base.SaveData();
            KnownStations kstations = stations.GetSaveData();
            if(kstations != null)
            {
                string stationdata = MyAPIGateway.Utilities.SerializeToXML(kstations);
                TextWriter tw = MyAPIGateway.Utilities.WriteFileInWorldStorage(stationDataFile, typeof(string));
                tw.Write(stationdata);
                tw.Close();
                MyLog.Default.WriteLineAndConsole("Station Data Saved");
            }
        }

        public void Setup()
        {
            GetStations();
            //HashSet<IMyEntity> entities = new HashSet<IMyEntity>();
            //MyAPIGateway.Entities.GetEntities(entities);

            foreach (StationData station in stations.stations)
            {
                //add callbacks
                if (station.station.StationEntityId!=0)
                {
                    
                    if (station.stationGrid != null)
                    {
                        //MyAPIGateway.Utilities.ShowMessage("Dealer", "Station Grid: " + station.stationGrid.DisplayName);
                        station.stationGrid.OnClosing += StationGrid_OnClosing;
                        station.stationGrid.PlayerPresenceTierChanged += StationGrid_PlayerPresenceTierChanged;
                        station.isActive = true;
                        //GetStationShipSellBlocks(station);
                    } else
                    {
                        //try to get station grid
                        IMyEntity ent = MyAPIGateway.Entities.GetEntityById(station.station.StationEntityId);
                        if(ent!=null)
                        {
                            if (ent as IMyCubeGrid != null)
                            {
                                station.stationGrid = ent as IMyCubeGrid;
                                //MyAPIGateway.Utilities.ShowMessage("Dealer", "Station Grid: " + station.stationGrid.DisplayName);
                                station.stationGrid.OnClosing += StationGrid_OnClosing;
                                station.stationGrid.PlayerPresenceTierChanged += StationGrid_PlayerPresenceTierChanged;
                                station.isActive = true;
                            }
                        }
                    }
                    
                    //
                }
            }

        }

        public void SetCallbacks()
        {
            MyAPIGateway.Entities.OnEntityAdd += Entities_OnEntityAdd;
            MyAPIGateway.Entities.OnEntityRemove += Entities_OnEntityRemove;

        }


        public override void UpdateBeforeSimulation()
        {
            if (!bIsServer)
                return;



            currenTick++;
            if (currenTick >= STATIONTICK)
            {
                List<StationData> activeStations = stations.stations.FindAll(s => s.isActive && s.station.StationEntityId!=0);
                if (activeStations != null)
                {
                    foreach (StationData station in activeStations)
                    {
                        if (station.isActive && !station.hasBlocks)
                        {
                            if (station.stationGrid != null)
                            {
                                try
                                {
                                    //MyAPIGateway.Utilities.ShowMessage("Dealer", "Try Get Station Blocks");
                                    GetStationShipSellBlocks(station);
                                    station.hasBlocks = true;
                                } catch(Exception e) {
                                    //MyAPIGateway.Utilities.ShowMessage("Dealer", "Error: "+e.Message);
                                }
                            }
                        }
                    }
                }
                currenTick = 0;
            }

            base.UpdateBeforeSimulation();
        }


        #region CallBacks

        private void StationGrid_PlayerPresenceTierChanged(IMyCubeGrid obj)
        {

            StationData sData = stations.stations.Find(s => s.stationGrid == obj);
            if (sData != null)
            {
                if (obj.PlayerPresenceTier == MyUpdateTiersPlayerPresence.Normal)
                {
                    sData.isActive = true;
                    if (sData.hasBlocks == false)
                    {
                        sData.isActive = true;
                    }
                }
                else
                {
                    sData.isActive = false;
                    sData.hasBlocks = false;
                }

            }
        }

        private void StationGrid_OnClosing(IMyEntity obj)
        {
            if (obj as IMyCubeGrid != null)
            {
                IMyCubeGrid ent = obj as IMyCubeGrid;
                ent.PlayerPresenceTierChanged -= StationGrid_PlayerPresenceTierChanged;
            }

            obj.OnClosing -= StationGrid_OnClosing;
        }

        private void Entities_OnEntityRemove(IMyEntity entity)
        {
            if (entity == null)
                return;

            MyAPIGateway.Entities.OnEntityAdd -= Entities_OnEntityAdd;
            MyAPIGateway.Entities.OnEntityRemove -= Entities_OnEntityRemove;
        }

        public void Entities_OnEntityAdd(IMyEntity entity)
        {
            if (entity == null)
                return;

            if (entity as IMyCubeGrid != null)
            {
                var grid = entity as IMyCubeGrid;
                if (grid != null)
                {
                    MyObjectBuilder_Station station;
                    TryGetStation(grid, out station);
                    if (station != null)
                    {
                        //MyAPIGateway.Utilities.ShowMessage("Dealer", "Found Station: " + station.PrefabName);
                        StationData sData = new StationData();
                        sData.station = station;
                        sData.stationGrid = grid;
                        sData.stationGrid.OnClosing += StationGrid_OnClosing;
                        sData.stationGrid.PlayerPresenceTierChanged += StationGrid_PlayerPresenceTierChanged;
                        if(sData.stationGrid!=null)
                        {
                            //MyAPIGateway.Utilities.ShowMessage("Dealer", "Station not null: " + sData.stationGrid.DisplayName);
                            sData.isActive = true;
                            GetStationShipSellBlocks(sData);
                        }
                        stations.stations.Add(sData);
                    }
                }
            }

        }
        #endregion

        #region GetandCheck

        public void GetStationShipSellBlocks(StationData sData)
        {
            if (sData == null)
                return;

            if (sData.stationGrid == null)
                return;

            if (sData.stationGrid.Transparent)
            {
                return;
            }

            List<IMySlimBlock> blocks = new List<IMySlimBlock>();
            sData.stationGrid.GetBlocks(blocks);
            MyLog.Default.WriteLineAndConsole("DEALERSHIP: Got Station Blocks");
            if (blocks == null)
                return;
            //MyLog.Default.WriteLineAndConsole("DEALERSHIP: Blocks NOT NULL");
            foreach (var block in blocks)
            {
                if (block != null)
                {
                    
                    var cblock = block.FatBlock;

                    if (cblock != null)
                    {
                        //MyLog.Default.WriteLineAndConsole("DEALERSHIP: CBLOCK: "+cblock.Name);
                        if (cblock.DisplayNameText.Equals("[SHIPSELL_SMALL]"))
                        {
                            //MyLog.Default.WriteLineAndConsole("DEALERSHIP: FOUND SHIP SELL BLOCK");
                            if (cblock as IMyShipConnector != null)
                            {
                                sData.smallShipConnector = cblock as IMyShipConnector;
                                //MyAPIGateway.Utilities.ShowMessage("Dealer", "Got small connector");
                            }
                        }
                        else if (cblock.DisplayNameText.Equals("[SHIPSELL_LARGE]"))
                        {
                            //MyLog.Default.WriteLineAndConsole("DEALERSHIP: FOUND SHIP SELL BLOCK");
                            if (cblock as IMyShipConnector != null)
                            {
                                sData.largeShipConnector = cblock as IMyShipConnector;
                                //MyAPIGateway.Utilities.ShowMessage("Dealer", "Got large connector");
                            }
                        }
                        else if (cblock.DisplayNameText.Equals("[SHIPINFO]"))
                        {
                            //MyLog.Default.WriteLineAndConsole("DEALERSHIP: FOUND INFO PANEL");
                            if (cblock as IMyTextPanel != null)
                            {
                                sData.shipInfoPanel = cblock as IMyTextPanel;
                                //MyAPIGateway.Utilities.ShowMessage("Dealer", "Got info Panel");
                            }
                        }
                        else if (cblock.DisplayNameText.Equals("[SHIPVENDOR]"))
                        {
                            //MyLog.Default.WriteLineAndConsole("DEALERSHIP: FOUND TERMINAL");
                            if (cblock as IMyTerminalBlock != null)
                            {
                                sData.shipSellTerminal = cblock as IMyTerminalBlock;
                                //MyAPIGateway.Utilities.ShowMessage("Dealer", "Got vendor terminal");
                            }
                        }
                    }
                }
            }

        }

        public void GetStations()
        {
            List<MyObjectBuilder_Station> stationsList = GetAllStations();
            foreach (MyObjectBuilder_Station stationobj in stationsList)
            {
                StationData sData = new StationData();
                sData.station = stationobj;
                IMyEntity ent;
                if (MyAPIGateway.Entities.TryGetEntityById(sData.station.StationEntityId, out ent))
                {
                    if (ent != null)
                    {
                        if (ent as IMyCubeGrid != null)
                        {
                            
                            sData.stationGrid = ent as IMyCubeGrid;
                            MyAPIGateway.Utilities.ShowMessage("Dealer", "Found Station Grid: " + sData.stationGrid);
                        }
                    }
                }
                stations.stations.Add(sData);
            }
            if (knownStations != null)
            {
                stations.SetSaveData(knownStations);
            }

        }

        public bool IsBountyFaction(IMyFaction fo)
        {
            if (fo == null)
                return false;

            var factionobj = MyAPIGateway.Session.Factions.GetObjectBuilder();
            if (factionobj != null)
            {
                var faction = factionobj.Factions.Find(f => f.FactionId == fo.FactionId);
                if (faction != null)
                {

                    if (faction.FactionType == MyFactionTypes.Miner)
                    {
                        if (faction.Name.Contains("[B]"))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public bool IsBountyFaction(MyObjectBuilder_Faction faction)
        {
            if (faction == null)
                return false;

            if (faction.FactionType == MyFactionTypes.Miner)
            {
                if (faction.Name.Contains("[B]"))
                {
                    return true;
                }
            }
            return false;
        }


        public List<MyObjectBuilder_Station> GetAllStations()
        {
            List<MyObjectBuilder_Station> stations = new List<MyObjectBuilder_Station>();
            foreach (var fo in MyAPIGateway.Session.Factions.Factions)
            {
                stations.AddRange(GetFactionStations(fo.Value));
            }
            return stations;
        }

        public List<MyObjectBuilder_Station> GetFactionStations(IMyFaction fo)
        {
            List<MyObjectBuilder_Station> stations = new List<MyObjectBuilder_Station>();
            if (fo != null)
            {
                var factionobj = MyAPIGateway.Session.Factions.GetObjectBuilder();
                if (factionobj != null)
                {
                    var faction = factionobj.Factions.Find(f => f.FactionId == fo.FactionId);
                    if (faction != null)
                    {

                        return faction.Stations;
                    }
                }
            }

            return stations;
        }

        public MyObjectBuilder_Station GetStationByID(long id)
        {
            foreach (var faction in MyAPIGateway.Session.Factions.Factions)
            {
                List<MyObjectBuilder_Station> stations = GetFactionStations(faction.Value);
                if (stations != null)
                {
                    MyObjectBuilder_Station sobj = stations.Find(s => s.Id == id);
                    if (sobj != null)
                        return sobj;
                }
            }
            return null;
        }

        public bool TryGetStation(IMyCubeGrid grid, out MyObjectBuilder_Station station)
        {

            if (grid == null)
            {
                station = null;
                return false;
            }

            var factionobj = MyAPIGateway.Session.Factions.GetObjectBuilder();
            if (factionobj != null)
            {

                IMyFaction fo = MyAPIGateway.Session.Factions.TryGetPlayerFaction(grid.BigOwners.FirstOrDefault());
                if (fo != null)
                {
                    var faction = factionobj.Factions.Find(f => f.FactionId == fo.FactionId);
                    if (faction != null)
                    {
                        station = faction.Stations.Find(s => s.StationEntityId == grid.EntityId);
                        if (station != null)
                        {

                            return true;
                        }

                    }
                }
            }
            station = null;
            return false;
        }
        #endregion

    }


    public class StationsData
    {
        public List<StationData> stations = new List<StationData>();


        public void SetSaveData(KnownStations cstations)
        {
            if(cstations!=null)
            {
                foreach (KnownStation kstation in cstations.Stations)
                {
                    StationData station = null;
                    try
                    {
                        station = stations.Find(s => s.station.StationEntityId == kstation.entityID);
                    } catch { }
                    
                    if(station != null)
                    {
                        station.knownstation = kstation;
                    }
                }
            }
        }

        public KnownStations GetSaveData()
        {
            KnownStations cstations = new KnownStations();

            foreach (StationData station in stations)
            {
                if (station.knownstation == null)
                {
                    station.knownstation = new KnownStation();
                    station.knownstation.factionID = station.station.FactionId;
                    station.knownstation.stationID = station.station.Id;
                    station.knownstation.entityID = station.station.StationEntityId;
                    cstations.Stations.Add(station.knownstation);
                } else
                {
                    //might as well update
                    station.knownstation.factionID = station.station.FactionId;
                    station.knownstation.stationID = station.station.Id;
                    station.knownstation.entityID = station.station.StationEntityId;
                    cstations.Stations.Add(station.knownstation);
                }
            }

            return cstations;
        }

    }


    [System.Serializable]
    public class KnownStations
    {
        public List<KnownStation> Stations = new List<KnownStation>();
    }

    [System.Serializable]
    public class KnownStation
    {
        public long stationID = 0;
        public long entityID = 0;
        public long factionID = 0;

    }

    public class StationData
    {
        public MyObjectBuilder_Station station = null;
        public IMyCubeGrid stationGrid = null;
        public bool isActive = false;
        public KnownStation knownstation = null;
        public IMyTextPanel shipInfoPanel = null;
        public IMyTerminalBlock shipSellTerminal = null;
        public IMyShipConnector smallShipConnector = null;
        public IMyShipConnector largeShipConnector = null;
        public bool hasBlocks = false;
    }


}

