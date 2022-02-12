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
    using Razmods.Dealership.Colors;
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
        int currenTick = 0;
        public ColorHelper colorHelper = new ColorHelper();

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
            if (MyAPIGateway.Utilities.FileExistsInWorldStorage(stationDataFile, typeof(KnownStations)))
            {
                var reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(stationDataFile, typeof(KnownStations));
                if (reader != null)
                {
                    string data = reader.ReadToEnd();
                    knownStations = MyAPIGateway.Utilities.SerializeFromXML<KnownStations>(data);
                    if(knownStations != null)
                    {
                        stations.SetSaveData(knownStations);
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
                        VRage.ModAPI.IMyEntity ent = MyAPIGateway.Entities.GetEntityById(station.station.StationEntityId);
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
                                //sData.shipLeftPanel.SurfaceSize
                                //MyAPIGateway.Utilities.ShowMessage("Dealer", "Got info Panel");
                            }
                        }
                        else if (cblock.DisplayNameText.Equals("[SHIPLCDRIGHT]"))
                        {
                            //MyLog.Default.WriteLineAndConsole("DEALERSHIP: FOUND INFO PANEL");
                            if (cblock as IMyTextPanel != null)
                            {
                                sData.shipRightPanel = cblock as IMyTextPanel;
                                //MyAPIGateway.Utilities.ShowMessage("Dealer", "Got info Panel");
                            }
                        }
                        else if (cblock.DisplayNameText.Equals("[SHIPVENDOR]"))
                        {
                            //MyLog.Default.WriteLineAndConsole("DEALERSHIP: FOUND TERMINAL");
                            if (cblock as IMyTerminalBlock != null)
                            {
                                MyAPIGateway.Utilities.ShowMessage("Dealer", "Found Vendor Panel");
                                IMyButtonPanel panel = cblock as IMyButtonPanel;
                                if(panel != null)
                                {
                                    MyAPIGateway.Utilities.ShowMessage("Dealer", "Found Button Panel");
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
                    StationData station = null;
                    try
                    {
                        station = stations.Find(s => s.station.StationEntityId == kstation.entityID);
                    } catch { }
                    
                    if(station != null)
                    {
                        station.shipsForSale = kstation.shipsForSale;
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

        public bool hasBlocks = false;
        ShipInformation shipInfo = null;
        public List<ShipForSale> shipsForSale = new List<ShipForSale> ();
        public void Sell_The_Ship()
        {
            if(shipInfo!=null)
            {
                if(shipInfo.shipGrid!=null)
                {

                    if(shipInfo.shipGrid.BigOwners.Count>0)
                    {

                        long owner = shipInfo.shipGrid.BigOwners[0];
                        List<IMyPlayer> players = new List<IMyPlayer>();
                        MyAPIGateway.Players.GetPlayers(players);

                        IMyPlayer mainOwner = players.Find(p => p.IdentityId == owner);

                        if(mainOwner != null)
                        {
                            Random random = new Random();
                            string blueprintName = shipInfo.shipGrid.CustomName + "_" + mainOwner.IdentityId + "_" + random.Next();
                            MyVisualScriptLogicProvider.CreateLocalBlueprint(shipInfo.shipGrid.Name, blueprintName, shipInfo.shipGrid.CustomName);

                            ShipForSale sale = new ShipForSale();
                            sale.shipName = shipInfo.shipGrid.DisplayName;
                            sale.position = shipInfo.shipGrid.PositionComp.GetPosition();
                            sale.orientation = shipInfo.shipGrid.PositionComp.GetOrientation().Forward + shipInfo.shipGrid.PositionComp.GetOrientation().Up;
                            sale.blueprintName = blueprintName;
                            sale.price = shipInfo.shipPrice;
                            sale.shipdetails = shipInfo.shipdetails;
                            shipsForSale.Add(sale);
                            mainOwner.RequestChangeBalance(shipInfo.shipPrice);
                            MyVisualScriptLogicProvider.ShowNotification("Sold " + shipInfo.shipGrid.CustomName + " for $" + shipInfo.shipPrice, 5000, "Green", mainOwner.IdentityId);
                            shipInfo.shipGrid.Close();
                            shipInfo = null;
                            
                        }

                        //MyVisualScriptLogicProvider.CreateLocalBlueprint()
                    }



                }
            }
        }

        public void Panel_ButtonPressed(int button)
        {
            MyAPIGateway.Utilities.ShowMessage("Dealer", "Button " + button + " pressed!");
            if (shipSellTerminal != null)
            {
                
                switch (button)
                {
                    case 0:
                        break;
                    case 1:
                        break;
                    case 2:
                        break;
                    case 3:
                        Sell_The_Ship();
                        break;

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
                        calculatedprice += blockdef.PCU*10;

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
                        IMyCubeGrid grid = smallShipConnector.OtherConnector.Parent as IMyCubeGrid;
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

