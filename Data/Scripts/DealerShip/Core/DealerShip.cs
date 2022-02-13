using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using Sandbox.Game.SessionComponents;
using Sandbox.Game;
using Sandbox.Game.World.Generator;
using VRage.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game.World;
using VRageMath;

namespace Razmods.Dealership
{
    using Entities.Blocks;
    using Razmods.Dealership.Colors;
    using Sandbox.Game.Entities;
    using Sandbox.Game.SessionComponents.Clipboard;
    using SpaceEngineers.Game.ModAPI;

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
        int STATIONUPDATETICK = 300;
        int currenTick = 0;
        int currenUpdateTick = 0;

        public ColorHelper colorHelper = new ColorHelper();
        bool dataLoaded = false;
        public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
        {
            base.Init(sessionComponent);
            bIsServer = MyAPIGateway.Multiplayer.IsServer;
            colorHelper.ConstructColorMap();
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
            MyLog.Default.WriteLineAndConsole("Station Data Loading....");
            if (MyAPIGateway.Utilities.FileExistsInWorldStorage(stationDataFile, typeof(string)))
            {
                var reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(stationDataFile, typeof(string));
                if (reader != null)
                {
                    string data = reader.ReadToEnd();
                    knownStations = MyAPIGateway.Utilities.SerializeFromXML<KnownStations>(data);
                    if (knownStations != null)
                    {
                        MyLog.Default.WriteLineAndConsole("Station Data Loaded");
                    }
                    else
                    {
                        MyLog.Default.WriteLineAndConsole("Station Data Was NULL");
                    }
                }
            }
            else
            {
                MyLog.Default.WriteLineAndConsole("Station Data Reader Null");
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



        }

        public void SetCallbacks()
        {
            MyAPIGateway.Entities.OnEntityAdd += Entities_OnEntityAdd;
            MyAPIGateway.Entities.OnEntityRemove += Entities_OnEntityRemove;

        }

        public override void UpdateAfterSimulation()
        {
            base.UpdateAfterSimulation();
            currenTick++;

            if (currenTick >= STATIONTICK)
            {
                //Update Station Listing
                GetStations();

                foreach (StationData station in stations.stations)
                {
                    //if there is loaded station data try to add it.
                    if (station.knownstation == null)
                    {
                        if (knownStations != null)
                        {
                            //MyLog.Default.WriteLineAndConsole("Try add Station Data for " + station.station.PrefabName);
                            KnownStation ks = knownStations.Stations.Find(s => s.stationID == station.station.Id);
                            if (ks != null)
                            {
                                //MyLog.Default.WriteLineAndConsole("Station Data added - Ships for Sale: " + ks.shipsForSale.Count);
                                station.shipsForSale = ks.shipsForSale;
                                station.knownstation = ks;
                            }
                        }

                    }
                    //If station has an entityID check if we have added its grid.

                    if (station.station.StationEntityId != 0)
                    {

                        if (station.stationGrid == null)
                        {
                            MyAPIGateway.Utilities.ShowMessage("Dealer", "Try to find Station Grid for : " + station.station.PrefabName);
                            //try to get station grid
                            IMyEntity ent = MyAPIGateway.Entities.GetEntityById(station.station.StationEntityId);
                            if (ent != null)
                            {
                                if (ent as IMyCubeGrid != null)
                                {
                                    station.stationGrid = ent as IMyCubeGrid;
                                    MyAPIGateway.Utilities.ShowMessage("Dealer", "Station Grid: " + station.stationGrid.DisplayName);
                                    station.stationGrid.OnClosing += StationGrid_OnClosing;
                                    station.stationGrid.PlayerPresenceTierChanged += StationGrid_PlayerPresenceTierChanged;
                                    station.isActive = true;
                                    GetStationShipSellBlocks(station);
                                    station.hasBlocks = true;
                                }
                            }
                        }

                        //
                    }
                   
                }

                currenTick = 0;
            }
        }

        public override void UpdateBeforeSimulation()
        {
            if (!bIsServer)
                return;
            currenUpdateTick++;
            if (currenUpdateTick >= STATIONUPDATETICK)
            {
                foreach (StationData station in stations.stations)
                {
                    station.Update();
                }
                currenUpdateTick = 0;
            }
            base.UpdateBeforeSimulation();
        }


        #region CallBacks

        private void StationGrid_PlayerPresenceTierChanged(IMyCubeGrid obj)
        {
            StationData sData = stations.stations.Find(s => s.station.StationEntityId == obj.EntityId);
            if (sData != null)
            {
                
                //MyAPIGateway.Utilities.ShowMessage("Dealer","Station Presence Changed "+obj.PlayerPresenceTier.ToString());
            }
        }

        private void StationGrid_OnClosing(IMyEntity obj)
        {
            if (obj as IMyCubeGrid != null)
            {
                IMyCubeGrid ent = obj as IMyCubeGrid;
                ent.PlayerPresenceTierChanged -= StationGrid_PlayerPresenceTierChanged;
                StationData sData = stations.stations.Find(s => s.station.StationEntityId == obj.EntityId);
                if(sData != null)
                {
                    if (sData.previewShip != null)
                    {
                        sData.previewShip.Close();
                        sData.previewShip = null;
                    }
                    sData.stationGrid = null;
                    sData.isActive = false;
                    sData.hasBlocks = false;
                }
            }

            obj.OnClosing -= StationGrid_OnClosing;
            
        }

        private void Entities_OnEntityRemove(IMyEntity entity)
        {
            if (entity == null)
                return;

        }

        public void Entities_OnEntityAdd(IMyEntity entity)
        {
            GetStations();
            if (entity == null)
                return;

            if (entity as IMyCubeGrid != null)
            {
              
                var grid = entity as IMyCubeGrid;
                if (grid != null)
                {
                    //see if grid id matches station entity id
                    StationData sData = stations.stations.Find(st => st.station.StationEntityId == grid.EntityId);
                    if(sData == null)
                    {
                        //try to get entity by coords - myobjectbuilder doesn't update until save sometimes.
                        sData = stations.stations.Find(st => st.station.Position == grid.GetPosition());
                        if(sData!=null)
                        {
                            sData.station.StationEntityId = grid.EntityId;
                        }
                    }
                    if (sData != null)
                    {
                        //MyAPIGateway.Utilities.ShowMessage("Dealer", "Station found for: " + grid.CustomName);
                        if (sData.knownstation == null)
                        {
                            //if we have saved data for station Make sure its added
                            if (knownStations != null)
                            {
                                //MyAPIGateway.Utilities.ShowMessage("Dealer", "Add Station Data: " + sData.station.PrefabName);
                                MyLog.Default.WriteLineAndConsole("Try add Station Data for " + sData.station.PrefabName);
                                KnownStation ks = knownStations.Stations.Find(s => s.stationID == sData.station.Id);
                                if (ks != null)
                                {
                                    //MyAPIGateway.Utilities.ShowMessage("Dealer", "Station Data added - Ships for Sale: " + ks.shipsForSale.Count);
                                    MyLog.Default.WriteLineAndConsole("Station Data added - Ships for Sale: " + ks.shipsForSale.Count);
                                    sData.shipsForSale = ks.shipsForSale;
                                    sData.knownstation = ks;
                                }
                            }
                        }
                        //if station grid is not applied, apply it here
                        if (sData.stationGrid == null)
                        {
                            sData.stationGrid = grid;
                            //Entities_OnEntityAdd callbacks
                            sData.stationGrid.OnClosing += StationGrid_OnClosing;
                            sData.stationGrid.PlayerPresenceTierChanged += StationGrid_PlayerPresenceTierChanged;
                            
                            if (sData.stationGrid != null)
                            {
                                
                                sData.isActive = true;
                                //get the station blocks
                                GetStationShipSellBlocks(sData);
                                //do a quick update
                                sData.Update();
                            }
                        }
                        
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
                                sData.smallShipConnector.UpdateTimerTriggered += sData.SmallShipConnector_UpdateTimerTriggered;
                                //MyAPIGateway.Utilities.ShowMessage("Dealer", "Got small connector");
                            }
                        }
                        else if (cblock.DisplayNameText.Equals("[SHIPSELL_LARGE]"))
                        {
                            //MyLog.Default.WriteLineAndConsole("DEALERSHIP: FOUND SHIP SELL BLOCK");
                            if (cblock as IMyShipConnector != null)
                            {
                                sData.largeShipConnector = cblock as IMyShipConnector;
                                sData.largeShipConnector.UpdateTimerTriggered += sData.LargeShipConnector_UpdateTimerTriggered;
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
                        else if (cblock.DisplayNameText.Equals("[SHIPLCDLEFT]"))
                        {
                            //MyLog.Default.WriteLineAndConsole("DEALERSHIP: FOUND INFO PANEL");
                            if (cblock as IMyTextPanel != null)
                            {
                                sData.shipLeftPanel = cblock as IMyTextPanel;
                                sData.shipLeftPanel.UpdateTimerTriggered += sData.ShipLeftPanel_UpdateTimerTriggered;
                                //sData.shipLeftPanel.SurfaceSize
                                //MyAPIGateway.Utilities.ShowMessage("Dealer", "Got left info Panel");
                            }
                        }
                        else if (cblock.DisplayNameText.Equals("[SHIPLCDRIGHT]"))
                        {
                            //MyLog.Default.WriteLineAndConsole("DEALERSHIP: FOUND INFO PANEL");
                            if (cblock as IMyTextPanel != null)
                            {
                                sData.shipRightPanel = cblock as IMyTextPanel;
                                
                                sData.shipRightPanel.UpdateTimerTriggered += sData.ShipRightPanel_UpdateTimerTriggered;
                                //MyAPIGateway.Utilities.ShowMessage("Dealer", "Got right info Panel");
                            }
                        }
                        else if (cblock.DisplayNameText.Equals("[SHIPVENDOR]"))
                        {
                            //MyLog.Default.WriteLineAndConsole("DEALERSHIP: FOUND TERMINAL");
                            if (cblock as IMyTerminalBlock != null)
                            {
                                
                                //MyAPIGateway.Utilities.ShowMessage("Dealer", "Found Vendor Panel");
                                IMyButtonPanel panel = cblock as IMyButtonPanel;

                                if(panel != null)
                                {
                                    //MyAPIGateway.Utilities.ShowMessage("Dealer", "Found Button Panel");
                                    sData.shipSellTerminal = panel;
                                    sData.shipSellTerminal.ButtonPressed += sData.Panel_ButtonPressed;
                                    sData.shipSellTerminal.AnyoneCanUse = true;
                                    
                                }
                                //MyAPIGateway.Utilities.ShowMessage("Dealer", "Got vendor terminal");
                            }
                        }
                    }
                }
            }
            sData.Update();
        }



        public void GetStations()
        {
            List<MyObjectBuilder_Station> stationsList = GetAllStations();

            foreach (MyObjectBuilder_Station stationobj in stationsList)
            {
                StationData sData = stations.stations.Find(s => s.station.Id == stationobj.Id);
                if (sData == null)
                {
                    sData = new StationData();
                    sData.station = stationobj;
                    stations.stations.Add(sData);
                } else
                {
                    sData.station = stationobj;
                }
              
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

        public int CalculateItemMinimalPrice(MyDefinitionId itemId, float baseCostProductionSpeedMultiplier, int minimalPrice)
        {
            MyComponentDefinition definition;
            if (MyDefinitionManager.Static.TryGetDefinition<MyComponentDefinition>(itemId, out definition) && definition.MinimalPricePerUnit != -1)
            {
                MyAPIGateway.Utilities.ShowMessage("Dealer:", "Min Price: " + definition.MinimalPricePerUnit);
                minimalPrice += definition.MinimalPricePerUnit;
                return minimalPrice;
            }

            MyBlueprintDefinitionBase definition2;
            if (!MyDefinitionManager.Static.TryGetBlueprintDefinitionByResultId(itemId, out definition2))
            {                
                return minimalPrice;
            }
            
            float num = definition.IsIngot ? 1f : MyAPIGateway.Session.AssemblerEfficiencyMultiplier;
            int num2 = 0;
            MyBlueprintDefinitionBase.Item[] prerequisites = definition2.Prerequisites;
            for (int i = 0; i < prerequisites.Length; i++)
            {
                MyBlueprintDefinitionBase.Item item = prerequisites[i];
                int minimalPrice2 = 0;
                minimalPrice = CalculateItemMinimalPrice(item.Id, baseCostProductionSpeedMultiplier, minimalPrice2);
                float num3 = (float)item.Amount / num;
                num2 += (int)((float)minimalPrice2 * num3);
            }

            float num4 = definition.IsIngot ? MyAPIGateway.Session.RefinerySpeedMultiplier : MyAPIGateway.Session.AssemblerSpeedMultiplier;
            for (int j = 0; j < definition2.Results.Length; j++)
            {
                MyBlueprintDefinitionBase.Item item2 = definition2.Results[j];
                if (item2.Id == itemId)
                {
                    float num5 = (float)item2.Amount;
                    if (num5 != 0f)
                    {
                        float num6 = 1f + (float)Math.Log(definition2.BaseProductionTimeInSeconds + 1f) * baseCostProductionSpeedMultiplier / num4;
                        minimalPrice += (int)((float)num2 * (1f / num5) * num6);
                        break;
                    }

                    MyLog.Default.WriteToLogAndAssert("Amount is 0 for - " + item2.Id);
                }
            }
            return minimalPrice;
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
                    MyLog.Default.WriteLineAndConsole("Check Station: " + kstation.stationID);
                    foreach (var s in stations)
                    {

                        

                        if (s.station.Id == kstation.stationID)
                        {
                            MyLog.Default.WriteLineAndConsole("Station: " + s.station.PrefabName + " Data Loaded.");
                            MyLog.Default.WriteLineAndConsole("Station: Has " + kstation.shipsForSale.Count + " ships for sale.");
                            s.shipsForSale = kstation.shipsForSale;
                            s.knownstation = kstation;
                        }
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
                    station.knownstation.shipsForSale = station.shipsForSale;
                    cstations.Stations.Add(station.knownstation);
                } else
                {
                    //might as well update
                    station.knownstation.factionID = station.station.FactionId;
                    station.knownstation.stationID = station.station.Id;
                    station.knownstation.entityID = station.station.StationEntityId;
                    station.knownstation.shipsForSale = station.shipsForSale;
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
        public List<ShipForSale> shipsForSale = new List<ShipForSale>();
    }

    public class ShipInformation
    {
        public IMyCubeGrid shipGrid = null;
        public int shipPrice = 0;
        public string shipdetails = "";
    }

    [System.Serializable]
    public class ShipForSale
    {
        public string shipName = "";
        public string blueprintName = "";
        public string shipdetails = "";
        public int price = 0;
        public Vector3D position = Vector3D.Zero;
        public Vector3D orientation = Vector3D.Zero;
    }

    public class StationData
    {

        public StationData()
        {
            leftScreenmessages.Add(
                "BUY/SELL\n" +
                "SHIPS\n" +
                "HERE!");

            leftScreenmessages.Add(
                "BEST DEALS\n" +
                "IN THIS\n" +
                "SECTOR!");

            leftScreenmessages.Add(
                "SEE OUR\n" +
                "SELECTION\n" +
                "OF SHIPS!");

            leftScreenmessages.Add(
                "OVER 250,000\n" +
                "HAPPY BUYERS\n" +
                "& COUNTING!");
        }

        public MyObjectBuilder_Station station = null;
        public IMyCubeGrid stationGrid = null;
        public bool isActive = false;
        public KnownStation knownstation = null;
        public IMyTextPanel shipInfoPanel = null;
        public IMyTextPanel shipLeftPanel = null;
        public IMyTextPanel shipRightPanel = null;
        public IMyButtonPanel shipSellTerminal = null;
        public IMyShipConnector smallShipConnector = null;
        public IMyShipConnector largeShipConnector = null;
        public IMyProjector largeProjector = null;
        public IMyProjector smallProjector = null;
        public IMyCubeGrid previewShip = null;
        public bool hasBlocks = false;
        ShipInformation shipInfo = null;
        int currentShip = 0;
        public List<ShipForSale> shipsForSale = new List<ShipForSale>();
        int currentLeftMessage = 0;
        int currentRightMessage = 0;
        int despawntime = 0;
        bool pressedNav = false;
        List<string> leftScreenmessages = new List<string>();

        public void Update()
        {
            ShipRightPanel_UpdateTimerTriggered(null);
            ShipLeftPanel_UpdateTimerTriggered(null);
            pressedNav = false;
            despawntime++;
            if(despawntime==40)
            {
                if(previewShip != null)
                {
                    previewShip.Close();
                }
                despawntime = 0;
            }
        }

        public void ShipRightPanel_UpdateTimerTriggered(IMyFunctionalBlock obj)
        {
            //MyAPIGateway.Utilities.ShowMessage("LCD", "RIGHT TICK");

            if (currentRightMessage < shipsForSale.Count)
            {
                if (shipRightPanel != null)
                {
                    if(previewShip != null)
                    {
                        shipRightPanel.WriteText("Ship For Sale #" + (currentShip + 1) + " \n " + shipsForSale[currentShip].shipName + "\n ONLY $" + shipsForSale[currentShip].price + "!! \n \n" + shipsForSale[currentShip].shipdetails);

                    }
                    else 
                    { 
                        if (shipsForSale.Count > 0)
                        {
                            shipRightPanel.WriteText("Ship For Sale #" + (currentRightMessage + 1) + " \n "+ shipsForSale[currentRightMessage].shipName+ "\n ONLY $"+ shipsForSale[currentRightMessage].price+ "!! \n \n" + shipsForSale[currentRightMessage].shipdetails);
                            currentRightMessage++;
                            if (currentRightMessage >= shipsForSale.Count)
                            {
                                currentRightMessage = 0;
                            }
                        } else
                        {
                            shipRightPanel.WriteText("WE BUY SHIPS!");
                        }
                    }
                }
            } else if(shipsForSale.Count == 0)
            {
                if (shipRightPanel != null)
                {
                    if (previewShip != null)
                    {
                        shipRightPanel.WriteText("Ship For Sale #" + (currentShip + 1) + " \n " + shipsForSale[currentShip].shipName + "\n ONLY $" + shipsForSale[currentShip].price + "!! \n \n" + shipsForSale[currentShip].shipdetails);

                    }
                    else
                    {
                        shipRightPanel.WriteText("Get a $5000 bonus when you sell your Ship! \n Our stock is empty! Terms apply. \n \n It's a great time to sell your Ship. \n Instant estimates. ");
                    }
                }
            }


        }

        public void ShipLeftPanel_UpdateTimerTriggered(IMyFunctionalBlock obj)
        {
            //MyAPIGateway.Utilities.ShowMessage("LCD", "LEFT TICK");

            if(currentLeftMessage< leftScreenmessages.Count)
            {
                if(shipLeftPanel!=null)
                {
                    if (previewShip != null)
                    {
                        shipLeftPanel.WriteText("DEMO SHIP\nWILL DESPAWN\nIN 2 MINS");
                    }
                    else
                    {                   
                        shipLeftPanel.WriteText(leftScreenmessages[currentLeftMessage]);
                        currentLeftMessage++;
                        if (currentLeftMessage >= leftScreenmessages.Count)
                        {
                            currentLeftMessage = 0;
                        }
                    }
                }
            }
                
            

        }

        public void Sell_The_Ship()
        {
            if(shipInfo!=null)
            {
                if(shipInfo.shipGrid!=null)
                {
                    if (shipInfo.shipGrid.IsRespawnGrid)
                        return;
                    if(shipInfo.shipGrid.BigOwners.Count>0)
                    {

                        long owner = shipInfo.shipGrid.BigOwners.FirstOrDefault();
                        List<IMyPlayer> players = new List<IMyPlayer>();
                        MyAPIGateway.Players.GetPlayers(players);

                        IMyPlayer mainOwner = players.Find(p => p.IdentityId == owner);

                        if(mainOwner != null)
                        {
                            Random random = new Random();
                            string blueprintName = shipInfo.shipGrid.CustomName + "_" + mainOwner.IdentityId + "_" + random.Next();

                            var ship = shipInfo.shipGrid.GetObjectBuilder();
                            string shipData = MyAPIGateway.Utilities.SerializeToXML(ship);
                            TextWriter tw = MyAPIGateway.Utilities.WriteFileInWorldStorage(blueprintName+".sbc", typeof(string));
                            tw.Write(shipData);
                            tw.Close();
                            MyLog.Default.WriteLineAndConsole("Ship Data Saved");
                            //MyVisualScriptLogicProvider.CreateLocalBlueprint(shipInfo.shipGrid.Name, blueprintName, shipInfo.shipGrid.CustomName);

                            ShipForSale sale = new ShipForSale();
                            sale.shipName = shipInfo.shipGrid.DisplayName;
                            sale.position = shipInfo.shipGrid.PositionComp.GetPosition();
                            sale.orientation = shipInfo.shipGrid.PositionComp.GetOrientation().Forward + shipInfo.shipGrid.PositionComp.GetOrientation().Up;
                            sale.blueprintName = blueprintName;
                            sale.price = shipInfo.shipPrice;
                            sale.shipdetails = shipInfo.shipdetails;
                            
                            int bonus = 0;
                            if(shipsForSale.Count ==0)
                            {
                                bonus = 5000;
                            }
                            shipsForSale.Add(sale);
                            mainOwner.RequestChangeBalance((long)(shipInfo.shipPrice*0.85)+bonus);
                            MyVisualScriptLogicProvider.ShowNotification("Sold " + shipInfo.shipGrid.CustomName + " for $" + ((shipInfo.shipPrice * 0.85) + bonus), 5000, "Green", mainOwner.IdentityId);
                            shipInfo.shipGrid.Close();
                            shipInfo = null;
                            
                        }

                        //MyVisualScriptLogicProvider.CreateLocalBlueprint()
                    }



                }
            }
        }

        public void Buy_The_Ship()
        {
            BoundingSphereD sphere = new BoundingSphereD(shipSellTerminal.GetPosition(), 2.0);
            List<IMyEntity> ents = MyAPIGateway.Entities.GetTopMostEntitiesInSphere(ref sphere);
            if(ents!=null)
            {
                foreach(IMyEntity ent in ents)
                {
                    
                    if(ent as IMyCharacter != null)
                    {
                        IMyCharacter character = ent as IMyCharacter;
                        if(character != null)
                        {
                            List<IMyPlayer> players = new List<IMyPlayer>();
                            MyAPIGateway.Players.GetPlayers(players);
                            IMyPlayer player = players.Find(p => p.Character.EntityId == character.EntityId);
                            if(player != null)
                            {
                                long balance = 0;
                                if(player.TryGetBalanceInfo(out balance))
                                {
                                    if(balance >= shipsForSale[currentShip].price)
                                    {
                                        previewShip.Close();
                                        previewShip = null;
                                        //MyAPIGateway.Utilities.ShowMessage("Dealer", "Remove Preview");
                                        if (MyAPIGateway.Utilities.FileExistsInWorldStorage(shipsForSale[currentShip].blueprintName + ".sbc", typeof(string)))
                                        {
                                            var reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(shipsForSale[currentShip].blueprintName + ".sbc", typeof(string));
                                            if (reader != null)
                                            {
                                                string data = reader.ReadToEnd();

                                                MyObjectBuilder_CubeGrid gridBuilder = MyAPIGateway.Utilities.SerializeFromXML<MyObjectBuilder_CubeGrid>(data);
                                                if (gridBuilder != null)
                                                {
                                                    MyAPIGateway.Entities.RemapObjectBuilder(gridBuilder);
                                                    //MyAPIGateway.Utilities.ShowMessage("Dealer", "Generate ship");
                                                    IMyEntity shent = MyAPIGateway.Entities.CreateFromObjectBuilder(gridBuilder);
                                                    //MyAPIGateway.Utilities.ShowMessage("Dealer", "Created Ship");
                                                    if (shent as IMyCubeGrid != null)
                                                    {
                                                        IMyCubeGrid grid = shent as IMyCubeGrid;
                                                        //grid.IsStatic = true;
                                                        //grid.Physics.Flags = RigidBodyFlag.RBF_STATIC;
                                                        grid.Physics.Deactivate();
                                                        grid.UpdateOwnership(0, false);
                                                        MyAPIGateway.Entities.AddEntity(grid, true);

                                                        //MyAPIGateway.Utilities.ShowMessage("Dealer", "Ship is Spawned");

                                                        player.RequestChangeBalance(-shipsForSale[currentShip].price);
                                                        MyVisualScriptLogicProvider.ShowNotification("Purchased " + shipsForSale[currentShip].shipName + " for $" + shipsForSale[currentShip].price+"!", 5000, "Green", player.IdentityId);
                                                        //MyAPIGateway.Utilities.ShowMessage("Dealer", "Ship Ownership Changed");
                                                        grid.ChangeGridOwnership(player.IdentityId, MyOwnershipShareModeEnum.Faction);
                                                        return;
                                                    
                                                    }
                                                }

                                            }
                                        }

                                    } else
                                    {
                                        MyVisualScriptLogicProvider.ShowNotification("Do Not have enough Money to purchase " + shipsForSale[currentShip].shipName + "!", 5000, "Red", player.IdentityId);

                                    }
                                }
                                return;
                            }
                        }
                    }
                }
            }
        }

        public void Panel_ButtonPressed(int button)
        {
            //MyAPIGateway.Utilities.ShowMessage("Dealer", "Button " + button + " pressed!");
            if (shipSellTerminal != null && !pressedNav)
            {
                
                switch (button)
                {
                    case 0:
                        currentShip--;
                        if (currentShip < 0)
                            currentShip = shipsForSale.Count-1;
                        Preview_Ship();
                        break;
                    case 1:
                        currentShip++;
                        if (currentShip > shipsForSale.Count-1)
                            currentShip = 0;

                        Preview_Ship();
                        break;
                    case 2:
                            Buy_The_Ship();
                        
                        break;
                    case 3:
                        if(previewShip==null)
                            Sell_The_Ship();
                        break;

                }
                pressedNav = true;
            }
        }

        private void Preview_Ship()
        {
            if(previewShip!=null)
            {
                previewShip.Close();
            }
            previewShip = null;
            if (currentShip > shipsForSale.Count - 1 || currentShip < 0)
                return;
            if (MyAPIGateway.Utilities.FileExistsInWorldStorage(shipsForSale[currentShip].blueprintName+".sbc", typeof(string)))
            {
                var reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(shipsForSale[currentShip].blueprintName + ".sbc", typeof(string));
                if (reader != null)
                {
                    string data = reader.ReadToEnd();
                    
                    MyObjectBuilder_CubeGrid gridBuilder = MyAPIGateway.Utilities.SerializeFromXML<MyObjectBuilder_CubeGrid>(data);
                    if (gridBuilder != null)
                    {
                        IMyEntity ent = MyAPIGateway.Entities.CreateFromObjectBuilder(gridBuilder);
                        
                        if (ent as IMyCubeGrid != null)
                        {
                            
                            previewShip = ent as IMyCubeGrid;
                            previewShip.IsRespawnGrid = true;

                            previewShip.Render.Transparency = 0.1f;
                            previewShip.Transparent = true;
                            previewShip.IsStatic = true;
                            previewShip.Physics.Flags = RigidBodyFlag.RBF_STATIC;
                            previewShip.Physics.Deactivate();
                            previewShip.UpdateOwnership(0, false);
                            previewShip.OnClosing += PreviewShip_OnClosing;
                            previewShip.OnGridChanged += PreviewShip_OnGridChanged;
                            previewShip.OnIsStaticChanged += PreviewShip_OnIsStaticChanged;
                            
                            MyAPIGateway.Entities.AddEntity(previewShip,true);

                            despawntime = 0;
                        }
                    }

                }
            }
            
        }

        private void PreviewShip_OnIsStaticChanged(IMyCubeGrid arg1, bool arg2)
        {
            previewShip.IsStatic = true;
        }

        private void PreviewShip_OnClosing(IMyEntity obj)
        {
            if(obj as IMyCubeGrid != null)
            {
                IMyCubeGrid ship = obj as IMyCubeGrid;
                ship.OnClosing -= PreviewShip_OnClosing;
                ship.OnGridChanged -= PreviewShip_OnGridChanged;
                ship.OnIsStaticChanged -= PreviewShip_OnIsStaticChanged;
                
            }

        }

        private void PreviewShip_OnGridChanged(IMyCubeGrid obj)
        {


            var cockpits = previewShip.GetFatBlocks<IMyCockpit>();
            foreach (var cockpit in cockpits)
            {
                if(cockpit != null)
                {
                    if(cockpit.Pilot!=null)
                    {
                        cockpit.Pilot.Kill();
                        //cockpit.Pilot.Die();
                        MyVisualScriptLogicProvider.ShowNotificationToAll("Unauthorized Pilot Detected!",5000, "Red");
                        //previewShip.Close();
                        return;
                    }


                }
                

            }
        }

        void SetShipInfo(IMyCubeGrid grid)
        {
            shipInfo = new ShipInformation();
            shipInfo.shipGrid = grid;

            if (shipInfo.shipGrid != null)
            {
                shipInfoPanel.WriteText("Ship Connected: " + shipInfo.shipGrid.CustomName + "\n");
                if (shipInfo.shipGrid.IsRespawnGrid)
                {
                    shipInfoPanel.FontColor = Color.Red;
                    shipInfoPanel.WriteText("Cannot Sell Respawn Ship! \n",true);
                } else
                {
                    shipInfoPanel.FontColor = Color.White;
                }
                



                var blocks = shipInfo.shipGrid.GetFatBlocks<IMyCubeBlock>();
                
                int calculatedprice = 0;
                List<MyObjectBuilder_CubeGrid> shipgrids = new List<MyObjectBuilder_CubeGrid>();
                shipgrids.Add(shipInfo.shipGrid.GetObjectBuilder(true) as MyObjectBuilder_CubeGrid);



                //calculatedprice += DealerShip.main.CalculateItemMinimalPrice(myObjectBuilder_ShipBlueprintDefinition.Id, 1, 0);


                foreach (var block in blocks)
                {
                    var blockobj = block.GetObjectBuilderCubeBlock();

                    MyCubeBlockDefinition blockdef = MyDefinitionManager.Static.GetCubeBlockDefinition(block.BlockDefinition);
                   
                    if (blockdef != null)
                    {
                        int modifier = 1;
                        if(shipInfo.shipGrid.GridSizeEnum == MyCubeSize.Large)
                        {
                            modifier = 200;
                        } else
                        {
                            modifier = 10;
                        }
                        calculatedprice += blockdef.PCU* modifier;

                    }



                }


                shipInfo.shipPrice = calculatedprice;
                shipInfo.shipdetails = "";
                shipInfoPanel.WriteText("Sell: $" + (int)(shipInfo.shipPrice*0.85) + "\n", true);
                shipInfoPanel.WriteText("Blocks: " + blocks.Count() + "\n", true);
                shipInfo.shipdetails += "Blocks: " + blocks.Count() + "\n";
                var cargos = shipInfo.shipGrid.GetFatBlocks<IMyCargoContainer>();
                shipInfoPanel.WriteText("Cargo: " + cargos.Count() + "\n", true);
                shipInfo.shipdetails += "Cargo: " + cargos.Count() + "\n";
                var gastanks = shipInfo.shipGrid.GetFatBlocks<IMyGasTank>();
                shipInfoPanel.WriteText("Hydrogen Tanks: " + gastanks.Count() + "\n", true);
                shipInfo.shipdetails += "Hydrogen Tanks: " + gastanks.Count() + "\n";
                var batteries = shipInfo.shipGrid.GetFatBlocks<IMyBatteryBlock>();
                shipInfoPanel.WriteText("Batteries: " + batteries.Count() + "\n", true);
                shipInfo.shipdetails += "Batteries: " + batteries.Count() + "\n";
                var reactors = shipInfo.shipGrid.GetFatBlocks<IMyReactor>();
                shipInfoPanel.WriteText("Reactors: " + reactors.Count() + "\n", true);
                shipInfo.shipdetails += "Reactors: " + reactors.Count() + "\n";
                var refinery = shipInfo.shipGrid.GetFatBlocks<IMyRefinery>();
                shipInfoPanel.WriteText("Refineries: " + refinery.Count() + "\n", true);
                shipInfo.shipdetails += "Refineries: " + refinery.Count() + "\n";
                var assemblers = shipInfo.shipGrid.GetFatBlocks<IMyAssembler>();
                shipInfoPanel.WriteText("Assemblers: " + assemblers.Count() + "\n", true);
                shipInfo.shipdetails += "Assemblers: " + assemblers.Count() + "\n";



            }
        }
        
        public void SmallShipConnector_UpdateTimerTriggered(IMyFunctionalBlock obj)
        {
            if(smallShipConnector != null)
            {
                if(smallShipConnector.Status == Sandbox.ModAPI.Ingame.MyShipConnectorStatus.Connected)
                {
                    //MyAPIGateway.Utilities.ShowMessage("DEALER", "SMALL SHIP CONNECTED");
                    if(shipInfoPanel!=null && smallShipConnector.OtherConnector!=null && shipInfo == null)
                    {
                        IMyCubeGrid grid = smallShipConnector.OtherConnector.Parent as IMyCubeGrid;
                        if(grid != null)
                        {
                            SetShipInfo(grid);
                        }
                    }
                }
                if (largeShipConnector.Status != Sandbox.ModAPI.Ingame.MyShipConnectorStatus.Connected && smallShipConnector.Status != Sandbox.ModAPI.Ingame.MyShipConnectorStatus.Connected)
                {
                    shipInfo = null;
                    if(shipInfoPanel != null)
                    {
                        shipInfoPanel.WriteText("No Ship:");
                    }
                }
            }
        }

        public void LargeShipConnector_UpdateTimerTriggered(IMyFunctionalBlock obj)
        {
            if (largeShipConnector != null)
            {
                if (largeShipConnector.Status == Sandbox.ModAPI.Ingame.MyShipConnectorStatus.Connected)
                {
                    //MyAPIGateway.Utilities.ShowMessage("DEALER", "SMALL SHIP CONNECTED");
                    if (shipInfoPanel != null && largeShipConnector.OtherConnector != null && shipInfo == null)
                    {
                        IMyCubeGrid grid = largeShipConnector.OtherConnector.Parent as IMyCubeGrid;
                        if (grid != null)
                        {
                            SetShipInfo(grid);
                        }
                    }
                }
                if (largeShipConnector.Status != Sandbox.ModAPI.Ingame.MyShipConnectorStatus.Connected && smallShipConnector.Status != Sandbox.ModAPI.Ingame.MyShipConnectorStatus.Connected)
                {
                    shipInfo = null;
                    if (shipInfoPanel != null)
                    {
                        shipInfoPanel.WriteText("No Ship:");
                    }
                }
            }
        }
    }


}

