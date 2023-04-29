using System;
using System.Linq;
using System.Collections.Generic;
using VRageMath;
using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using VRage.Game.GUI.TextPanel;

namespace Script9
{
    public sealed class Program : MyGridProgram
    {

        //------------START--------------

        #region Интерфейсы

        private interface ISector
        {
            string SectorName { get; set; }
            List<Alarm> AlarmList { get; set; }
            void Monitoring();
        }

        private interface IGate
        {
            void OpenGate();
            void CloseGate();
            void ToggleGate();
            string GateState { get; }
        }

        private interface IRoof
        {
            void OpenRoof();
            void CloseRoof();
            void ToggleRoof();
            string RoofState { get; }
        }
        #endregion

        #region Вспомогательные классы
        internal class LightControl
        {
            readonly List<IMyLightingBlock> _lights = new List<IMyLightingBlock>();
            public LightControl(List<IMyLightingBlock> lights)
            {
                _lights = lights;
                foreach (IMyLightingBlock light in _lights)
                {
                    light.BlinkIntervalSeconds = 0;
                    light.BlinkLength = 0;
                    light.Intensity = 10;
                    light.Radius = 20;
                    light.Color = Color.White;
                }
            }

            // Вкл. свет
            public void TurnOnLights() => _lights.ForEach(light => light.Enabled = true);

            // Выкл. свет
            public void TurnOffLights() => _lights.ForEach(light => light.Enabled = false);

            // Вкл./Выкл. свет
            public void ToggleLights() => _lights.ForEach(light => light.Enabled = !light.Enabled);

            // Обновление цвета ламп и режима моргания
            public void UpdateLights(Color color, bool blink, float blinkInterval = 1, float blinkLength = 50F)
            {
                foreach (IMyLightingBlock light in _lights)
                {
                    if (light.Color != color) light.Color = color;
                    if (blink == true)
                    {
                        if (light.BlinkIntervalSeconds != blinkInterval) light.BlinkIntervalSeconds = blinkInterval;
                        if (light.BlinkLength != blinkLength) light.BlinkLength = blinkLength;
                    }
                    else
                    {
                        if (light.BlinkIntervalSeconds != 0) light.BlinkIntervalSeconds = 0;
                        if (light.BlinkLength != 0) light.BlinkLength = 0;
                    }
                }
            }
        }

        // Класс управления воротами
        internal class GateControl : IGate
        {
            public string GateState { get; set; }
            private readonly List<IMyAirtightHangarDoor> _gateDoors = new List<IMyAirtightHangarDoor>();

            public GateControl(List<IMyAirtightHangarDoor> gateDoors)
            {
                _gateDoors = gateDoors;
                GateState = "NA";
            }

            // Откр. ворота
            public void OpenGate() => _gateDoors.ForEach(door => door.OpenDoor());

            // Закр. ворота
            public void CloseGate() => _gateDoors.ForEach(door => door.CloseDoor());


            // Откр./Закр. ворота
            public void ToggleGate()
            {
                switch (GateState)
                {
                    case "ОТКРЫТО":
                    case "ОТКРЫВАЮТСЯ":
                        CloseGate();
                        break;
                    case "ЗАКРЫТО":
                    case "ЗАКРЫВАЮТСЯ":
                        OpenGate();
                        break;
                }
            }

            // Проверить состояние ворот на откр. или закр. и т.д.
            public void CheckGate()
            {
                GateState = "NA";
                GateState = _gateDoors.FindAll(door => door.Status == DoorStatus.Closing).Count == _gateDoors.Count ? "ЗАКРЫВАЮТСЯ" : GateState;
                GateState = _gateDoors.FindAll(door => door.Status == DoorStatus.Opening).Count == _gateDoors.Count ? "ОТКРЫВАЮТСЯ" : GateState;
                GateState = _gateDoors.FindAll(door => door.Status == DoorStatus.Open).Count == _gateDoors.Count ? "ОТКРЫТО" : GateState;
                GateState = _gateDoors.FindAll(door => door.Status == DoorStatus.Closed).Count == _gateDoors.Count ? "ЗАКРЫТО" : GateState;
            }
        }

        internal class RoofControl : IRoof
        {
            public string RoofState { get; set; }
            private readonly List<IMyMotorAdvancedStator> _roofHinges = new List<IMyMotorAdvancedStator>();
            private readonly float _openState; // Положение шарниров для состояния "ОТКРЫТО" в градусах
            private readonly float _closeState; // Положение шарниров для состояния "ЗАКРЫТО" в градусах

            public RoofControl(List<IMyMotorAdvancedStator> roofHinges, float openState = 0f, float closeState = -90f)
            {
                _roofHinges = roofHinges;
                _openState = openState;
                _closeState = closeState;
                RoofState = "NA";
            }

            // Открыть крышу
            public void OpenRoof() => _roofHinges.ForEach(hinge => hinge.TargetVelocityRPM = Math.Round(RadToDeg(hinge.Angle), 0) != _openState ? 1 : 0);

            // Закрыть крышу
            public void CloseRoof() => _roofHinges.ForEach(hinge => hinge.TargetVelocityRPM = Math.Round(RadToDeg(hinge.Angle), 0) != _closeState ? -1 : 0);

            // Откр./Закр. крышу
            public void ToggleRoof()
            {
                switch (RoofState)
                {
                    case "ОТКРЫТО":
                    case "ОТКРЫВАЮТСЯ":
                        CloseRoof();
                        break;
                    case "ЗАКРЫТО":
                    case "ЗАКРЫВАЮТСЯ":
                        OpenRoof();
                        break;
                }
            }

            // Проверить состояние крыши на откр. или закр. и т.д.
            public void CheckRoof()
            {
                RoofState = "NA";
                RoofState = _roofHinges.FindAll(hinge => hinge.TargetVelocityRPM < 0f && hinge.Enabled).Count == _roofHinges.Count ? "ЗАКРЫВАЮТСЯ" : RoofState;
                RoofState = _roofHinges.FindAll(hinge => hinge.TargetVelocityRPM > 0f && hinge.Enabled).Count == _roofHinges.Count ? "ОТКРЫВАЮТСЯ" : RoofState;
                RoofState = _roofHinges.FindAll(hinge => Math.Round(RadToDeg(hinge.Angle)) == _openState).Count == _roofHinges.Count ? "ОТКРЫТО" : RoofState;
                RoofState = _roofHinges.FindAll(hinge => Math.Round(RadToDeg(hinge.Angle)) == _closeState).Count == _roofHinges.Count ? "ЗАКРЫТО" : RoofState;
            }
        }

        internal class DisplayControl
        {
            readonly List<IMyTextPanel> _displays = new List<IMyTextPanel>();

            public DisplayControl(List<IMyTextPanel> displays)
            {
                _displays = displays;

                //Пред. настройка дисплеев
                foreach (IMyTextPanel display in _displays)
                {
                    display.FontSize = 4.0f;
                    display.Alignment = TextAlignment.CENTER;
                    display.TextPadding = 15;
                }
            }

            // Обновление содержимого дисплеев
            public void UpdateDisplays(string text, Color backgroundColor, Color fontColor)

            {
                foreach (IMyTextPanel display in _displays)
                {
                    display.WriteText(text, false);
                    if (display.BackgroundColor != backgroundColor) display.BackgroundColor = backgroundColor;
                    if (display.FontColor != fontColor) display.FontColor = fontColor;
                }
            }
        }

        // Класс сообщения об ошибки
        // TODO: Добавить длительность сообщения в тиках.
        internal class Alarm
        {
            readonly string _alarmText;
            readonly string _alarmZone;
            readonly string _alarmSound;

            public Alarm(string alarmText, string alarmZone, string alarmSound)
            {
                _alarmText = alarmText;
                _alarmZone = alarmZone;
                _alarmSound = alarmSound;
            }
            public string Text => _alarmText;
            public string Zone => _alarmZone;
            public string Sound => _alarmSound;
        }

        internal class InventorySystem
        {
            private readonly Program _program;
            private readonly List<IMyTerminalBlock> _blocksWithInventory = new List<IMyTerminalBlock>();
            private readonly List<IMyCargoContainer> _cargoContainers = new List<IMyCargoContainer>();
            private readonly List<IMyInventory> _allInventories = new List<IMyInventory>();
            private readonly List<IMyInventory> _containersInventories = new List<IMyInventory>();

            public InventorySystem(Program program, IMyTerminalBlock referenceBlock)
            {
                _program = program;
                _program.GridTerminalSystem.GetBlocksOfType(_blocksWithInventory, block => block.IsSameConstructAs(referenceBlock) && block.HasInventory);
                _cargoContainers = _blocksWithInventory.OfType<IMyCargoContainer>().ToList();
                _allInventories = _blocksWithInventory.Select(block => block.GetInventory()).ToList();
                _containersInventories = _cargoContainers.Select(block => block.GetInventory()).ToList();
            }

            public double CurrentVolumeTotal => SumCurrentVolumes(_allInventories);
            public double MaxVolumeTotal => SumMaxVolumes(_allInventories);
            public int FilledRatioTotal => FilledRatio(CurrentVolumeTotal, MaxVolumeTotal);
            public double CurrentVolumeCargo => SumCurrentVolumes(_containersInventories);
            public double MaxVolumeCargo => SumMaxVolumes(_containersInventories);
            public int FilledRatioCargo => FilledRatio(CurrentVolumeCargo, MaxVolumeCargo);

            private static double SumCurrentVolumes(List<IMyInventory> inventories) => inventories.Sum(inventory => inventory.CurrentVolume.RawValue);
            private static double SumMaxVolumes(List<IMyInventory> inventories) => inventories.Sum(inventory => inventory.MaxVolume.RawValue);
            private static int FilledRatio(double currentValue, double maxValue) => (int)Math.Round(currentValue / maxValue * 100, 2);
        }

        //Класс инфы об корабле
        internal class ShipInfo
        //TODO: Посмотреть, есть ли упрощенная запись. Слишком длинно.
        {
            readonly EnergySystem _shipEnergySystem;
            readonly InventorySystem _shipInventorySystem;
            readonly string _shipName;

            public ShipInfo(Program program, IMyTerminalBlock referenceBlock)
            {
                _shipName = referenceBlock.CubeGrid.CustomName;
                _shipEnergySystem = new EnergySystem(program, referenceBlock);
                _shipInventorySystem = new InventorySystem(program, referenceBlock);
            }

            public string ShipName => _shipName;
            public int BatteryLevel => _shipEnergySystem.BatteryLevel;
            public int HydrogenLevel => _shipEnergySystem.HydrogenTanksLevel;
            public int OxygenLevel => _shipEnergySystem.OxygenTanksLevel;
            public int CargoHoldTotalFilledRatio => _shipInventorySystem.FilledRatioTotal;
            public int CargoHoldContainersFilledRatio => _shipInventorySystem.FilledRatioCargo;

        }

        //Класс инфы об подключенном корабле и коннекторе
        internal class ConnectedShipInfo
        {
            public ConnectedShipInfo(string connectorName, MyShipConnectorStatus connectorStatus, Program program, IMyTerminalBlock referenceBlock = null)
            {
                ConnectorName = connectorName;
                ConnectorStatus = connectorStatus;
                if (referenceBlock != null) ShipInfo = new ShipInfo(program, referenceBlock);
            }

            public string ConnectorName { get; }

            public MyShipConnectorStatus ConnectorStatus { get; }

            public ShipInfo ShipInfo { get; }
        }

        internal class EnergyInfo
        {
            public EnergyInfo(int batteriesLevel, int hydrogenLevel, int oxygenLevel, int powerLoad)
            {
                BatteriesLevel = batteriesLevel;
                HydrogenLevel = hydrogenLevel;
                OxygenLevel = oxygenLevel;
                PowerLoad = powerLoad;
            }

            public int BatteriesLevel { get; }

            public int HydrogenLevel { get; }

            public int OxygenLevel { get; }

            public int PowerLoad { get; }
        }

        #endregion


        #region Вспомогательный функции
        // Конвертация из радиан в градусы
        public static float RadToDeg(float radValue)
        {
            return radValue * 180f / (float)Math.PI;
        }

        #endregion

        // Класс системы определния тревог
        internal class AlarmSystem
        {
            private readonly Program _program;
            private readonly List<IMyWarhead> _warheads = new List<IMyWarhead>();
            private readonly List<IMyLargeTurretBase> _turrets = new List<IMyLargeTurretBase>();
            bool _warheadDetected;

            internal AlarmSystem(Program program)
            {
                _program = program;
                _program.GridTerminalSystem.GetBlocksOfType(_turrets, turret => turret.IsSameConstructAs(_program.Me));
            }

            internal List<Alarm> CurrentAlarms { get; } = new List<Alarm>();

            private bool Detect_warheads()
            {
                _program.GridTerminalSystem.GetBlocksOfType(_warheads, warhead => warhead.IsSameConstructAs(_program.Me));
                _warheadDetected = _warheads.Count > 0;
                _warheads.ForEach(warhead => warhead.IsArmed = false);
                return _warheadDetected;
            }
            // TODO: Метод на поднятие тревоги если у турелей цель (НЕ ПРОВЕРЕН до конца. Есть подозрение, что не работает из-за WeaponCore)
            private bool Enemy_detected() => _turrets.FindAll(turret => turret.HasTarget).Count > 0;

            // TODO: Метод на поднятие тревоги если критически низкий уровень энергии на базе.
            // Отмена. Будет отдельный объект по энергосистеме базы. Метод будет получать инфу от туда

            public bool DetectAlarms()
            {
                CurrentAlarms.Clear();
                if (Detect_warheads()) CurrentAlarms.Add(new Alarm("БОЕГОЛОВКА", "БАЗА", "Weapon31"));
                if (Enemy_detected()) CurrentAlarms.Add(new Alarm("ВРАГИ В РАДИУСЕ\nПОРАЖЕНИЯ", "БАЗА", "SoundBlockEnemyDetected"));
                return CurrentAlarms.Count > 0;
            }
        }

        // TODO: Написать класс, который бы описывал объекты в производственном секторе
        // TODO: Написать класс, который бы описывал объекты генерации энергии и ее потребление 

        internal class EnergySystem
        {
            private readonly Program _program;
            readonly List<IMyPowerProducer> _powerProducers = new List<IMyPowerProducer>();
            readonly List<IMyBatteryBlock> _batteries = new List<IMyBatteryBlock>();
            readonly List<IMyTerminalBlock> _hydrogenEngines = new List<IMyTerminalBlock>();
            readonly List<IMyGasTank> _hydrogenTanks = new List<IMyGasTank>();
            readonly List<IMyGasTank> _oxygenTanks = new List<IMyGasTank>();
            private readonly string[] _hydrogenTanksSubtypes = { "LargeHydrogenTank", "LargeHydrogenTankIndustrial", "SmallHydrogenTankSmall", "LargeHydrogenTankSmall", "SmallHydrogenTank" };
            private readonly string[] _oxygenTanksSubtypes = { "", "OxygenTankSmall" };

            public EnergySystem(Program program, IMyTerminalBlock referenceBlock)
            {
                _program = program;
                _program.GridTerminalSystem.GetBlocksOfType(_powerProducers, producer => producer.IsSameConstructAs(referenceBlock));
                _program.GridTerminalSystem.GetBlocksOfType(_batteries, battery => battery.IsSameConstructAs(referenceBlock));
                _program.GridTerminalSystem.GetBlocksOfType(_hydrogenEngines, engine => engine.BlockDefinition.SubtypeName == "LargeHydrogenEngine" && engine.IsSameConstructAs(referenceBlock));
                _program.GridTerminalSystem.GetBlocksOfType(_hydrogenTanks, tank => _hydrogenTanksSubtypes.Contains(tank.BlockDefinition.SubtypeName) && tank.IsSameConstructAs(referenceBlock));
                _program.GridTerminalSystem.GetBlocksOfType(_oxygenTanks, tank => _oxygenTanksSubtypes.Contains(tank.BlockDefinition.SubtypeName) && tank.IsSameConstructAs(referenceBlock));
            }

            public int HydrogenTanksLevel => GetTanksLevel(_hydrogenTanks);
            public int OxygenTanksLevel => GetTanksLevel(_oxygenTanks);
            public double HydrogenTanksCurrentVolume => GetTanksCurrentVolume(_hydrogenTanks);
            public double HydrogenTanksMaxVolume => GetTanksMaxVolume(_hydrogenTanks);
            public double OxygenTanksCurrentVolume => GetTanksCurrentVolume(_oxygenTanks);
            public double OxygenTanksMaxVolume => GetTanksMaxVolume(_oxygenTanks);
            public float BatteryStoredPower => GetBatteryStoredPower();
            public float BatteryMaxStoredPower => GetBatteryMaxStoredPower();
            public int BatteryLevel => GetBatteryLevel();
            public int PowerLoad => GetPowerLoad();

            public EnergyInfo GetEnergyInfo() => new EnergyInfo(BatteryLevel, HydrogenTanksLevel, OxygenTanksLevel, PowerLoad);

            private float GetBatteryStoredPower()
            {
                if (_batteries.Count == 0) return 0;
                float currentStoredPower = _batteries.Sum(battery => battery.CurrentStoredPower);
                return currentStoredPower;
            }

            private float GetBatteryMaxStoredPower()
            {
                if (_batteries.Count == 0) return 0;
                float maxStoredPower;
                maxStoredPower = _batteries.Sum(battery => battery.MaxStoredPower);
                return maxStoredPower;
            }

            private int GetBatteryLevel()
            {
                if (_batteries.Count == 0) return 0;
                int batteryInPercentage = (int)Math.Round(BatteryStoredPower / BatteryMaxStoredPower * 100, 0);
                return batteryInPercentage;
            }

            private static int GetTanksLevel<T>(List<T> tanks) where T : IMyGasTank
            {
                if (tanks.Count == 0) return 0;
                int filledRatio = (int)Math.Round((tanks.Sum(tank => tank.FilledRatio)) / tanks.Count * 100, 2);
                return filledRatio;
            }

            private static double GetTanksCurrentVolume<T>(List<T> tanks) where T : IMyGasTank
            {
                if (tanks.Count == 0) return 0;
                double currentVolume = tanks.Sum(tank => tank.FilledRatio * tank.Capacity);
                return currentVolume;
            }

            private static double GetTanksMaxVolume<T>(List<T> tanks) where T : IMyGasTank
            {
                if (tanks.Count == 0) return 0;
                double maxVolume = tanks.Sum(tank => tank.Capacity);
                return maxVolume;
            }

            private int GetPowerLoad()
            {
                if (_powerProducers.Count == 0) return 0;
                float currentOutput = _powerProducers.Sum(producer => producer.CurrentOutput);
                float maxOutput = _powerProducers.Sum(producer => producer.MaxOutput);
                return (int)Math.Round(currentOutput / maxOutput * 100, 2);
            }
        }


        internal class ControlRoom : ISector
        {
            private readonly Program _program;
            readonly List<IMyTextPanel> _displays = new List<IMyTextPanel>();
            private readonly IMyBlockGroup _controlRoomGroup;
            readonly List<IMyTextPanel> _hangarDisplays = new List<IMyTextPanel>();
            readonly List<IMyLightingBlock> _roomLights = new List<IMyLightingBlock>();
            readonly IMyTextPanel _powerDisplay;
            internal LightControl Lights { get; }
            public string SectorName { get; set; }
            public List<Alarm> AlarmList { get; set; } = new List<Alarm>();

            public ControlRoom(Program program, string controlRoomName)
            {

                _program = program;
                SectorName = controlRoomName;
                _controlRoomGroup = _program.GridTerminalSystem.GetBlockGroupWithName(controlRoomName);
                _controlRoomGroup.GetBlocksOfType(_displays);
                _controlRoomGroup.GetBlocksOfType(_roomLights);

                Lights = new LightControl(_roomLights);

                //Пред. настройка дисплеев
                foreach (IMyTextPanel display in _displays)
                {
                    display.FontSize = 1.4f;
                    display.Alignment = TextAlignment.CENTER;
                    display.TextPadding = 5;
                }

                _hangarDisplays = _displays.Where(display => display.CustomData.Contains("hangar")).ToList();
                _powerDisplay = _displays.Find(display => display.CustomData.Contains("power"));
            }

            public void ShowEnergyStatus(EnergyInfo energyInfo)
            {
                if (_powerDisplay != null)
                {
                    string text = "";
                    text += $"Батареи: {energyInfo.BatteriesLevel}%\n";
                    text += $"Водородные баки: {energyInfo.HydrogenLevel}%\n";
                    text += $"Кислородные баки: {energyInfo.OxygenLevel}%\n";
                    text += $"Нагрузка сети: {energyInfo.PowerLoad}%\n";
                    _powerDisplay.WriteText(text, false);
                }
            }

            //Метод отображения на определенном ангарном дисплее инфы об коннекторах ангара
            public void ShowHangarConnectorInfo(string hangarName, int displayIndex, Dictionary<long, ConnectedShipInfo> connectorsInfo)
            {
                if (displayIndex < _hangarDisplays.Count)
                {
                    string text = $"{hangarName}\n";
                    foreach (var connectorInfo in connectorsInfo)
                    {
                        text += "-----------------------------------------------------\n";
                        text += $"{connectorInfo.Value.ConnectorName}: {connectorInfo.Value.ConnectorStatus}\n";
                        if (connectorInfo.Value.ConnectorStatus == MyShipConnectorStatus.Connected)
                        {

                            text += $"{connectorInfo.Value.ShipInfo.ShipName}\n";
                            text += $"ТРЮМ: {connectorInfo.Value.ShipInfo.CargoHoldTotalFilledRatio}% ";
                            text += $"БАТАРЕИ: {connectorInfo.Value.ShipInfo.BatteryLevel}%\n";
                        }
                    }
                    _hangarDisplays[displayIndex].WriteText(text, false);
                }
            }

            // Заглушка для интерфейса надо потом разнести
            // TODO: Убрать когда отпадет необходимость
            public void Monitoring() { }
        }


        internal class HangarControl : ISector
        {
            /* Класс управления блоками в ангаре
             * Выполняет управление и мониторинг состояния блоков
             */
            readonly Program _program;
            private readonly List<IMyLightingBlock> _hangarLights = new List<IMyLightingBlock>();
            private readonly List<IMyMotorAdvancedStator> _hangarHinges = new List<IMyMotorAdvancedStator>();
            private readonly List<IMyTextPanel> _hangarDisplays = new List<IMyTextPanel>();
            private readonly List<IMyAirtightHangarDoor> _hangarDoors = new List<IMyAirtightHangarDoor>();
            private readonly List<IMySoundBlock> _hangarSpeakers = new List<IMySoundBlock>();
            private readonly List<IMyShipConnector> _hangarConnectors = new List<IMyShipConnector>();
            public List<Alarm> AlarmList { get; set; } = new List<Alarm>();
            public IMyTextSurface PlcScreen1 { get; }
            public string SectorName { get; set; }
            int _memAlarmNumber;
            readonly bool _hasDoor;
            readonly bool _hasRoof;
            int _alarmTimer;
            int _alarmMessageDuration;
            internal GateControl Gate1 { get; }
            internal RoofControl Roof1 { get; }
            internal LightControl Lights { get; }
            public DisplayControl Screens { get; }
            private readonly IMyBlockGroup _hangarGroup;


            public HangarControl(Program program, string hangarName, float openState = 0f, float closeState = -90f, bool hasDoor = false, bool hasRoof = false)
            {
                _program = program; //Ссылка на основную программу для возможности использовать GridTerminalSystem
                SectorName = hangarName; // Имя группы устройств в ангаре, например "Ангар 1".
                _hasDoor = hasDoor; // Идентификатор наличия ворот
                _hasRoof = hasRoof;  // Идентификатор наличия крыши
                PlcScreen1 = _program.Me.GetSurface(0); // Экран на программируемом блоке


                //Распределение блоков по типам в соответствующие списки
                _hangarGroup = _program.GridTerminalSystem.GetBlockGroupWithName(SectorName);
                _hangarGroup.GetBlocksOfType(_hangarLights);
                _hangarGroup.GetBlocksOfType(_hangarHinges);
                _hangarGroup.GetBlocksOfType(_hangarDoors);
                _hangarGroup.GetBlocksOfType(_hangarDisplays);
                _hangarGroup.GetBlocksOfType(_hangarSpeakers);
                _hangarGroup.GetBlocksOfType(_hangarConnectors);

                if (hasRoof) Roof1 = new RoofControl(_hangarHinges, openState, closeState);
                if (hasDoor) Gate1 = new GateControl(_hangarDoors);
                Lights = new LightControl(_hangarLights);
                Screens = new DisplayControl(_hangarDisplays);

                // Первая операция контроля
                Monitoring();
            }

            public Dictionary<long, ConnectedShipInfo> GetConnectorsInfo()
            // TODO: Подумать как упростить
            {
                Dictionary<long, ConnectedShipInfo> connectors_info = new Dictionary<long, ConnectedShipInfo>();
                foreach (IMyShipConnector connector in _hangarConnectors)
                {
                    if (connector.Status == MyShipConnectorStatus.Connected)
                    {
                        connectors_info.Add(connector.EntityId, new ConnectedShipInfo(connector.CustomName, connector.Status, _program, connector.OtherConnector));
                    }
                    else
                    {
                        connectors_info.Add(connector.EntityId, new ConnectedShipInfo(connector.CustomName, connector.Status, _program));
                    }
                }
                return connectors_info;
            }


            // Отображение состояний на дисплеях и лампах.
            public void ShowStatus(string blockState, string blockName)

            {
                switch (blockState)
                {
                    case "ОТКРЫВАЮТСЯ":
                        Screens.UpdateDisplays($"{SectorName}\n{blockName}\n ОТКРЫВАЮТСЯ", Color.Yellow, Color.Black);
                        Lights.UpdateLights(Color.Yellow, true);
                        break;
                    case "ЗАКРЫВАЮТСЯ":
                        Screens.UpdateDisplays($"{SectorName}\n{blockName}\n ЗАКРЫВАЮТСЯ", Color.Yellow, Color.Black);
                        Lights.UpdateLights(Color.Yellow, true);
                        break;
                    case "ОТКРЫТО":
                        Screens.UpdateDisplays($"{SectorName}\n{blockName}\n ОТКРЫТЫ", Color.Green, Color.White);
                        Lights.UpdateLights(Color.Green, false);
                        break;
                    case "ЗАКРЫТО":
                        Screens.UpdateDisplays($"{SectorName}\n{blockName}\n ЗАКРЫТЫ", Color.Black, Color.White);
                        Lights.UpdateLights(Color.White, false);
                        break;
                    default:
                        Screens.UpdateDisplays($"{SectorName}\n{blockName}\n НЕ ОПРЕДЕЛЕНО", Color.Orange, Color.White);
                        Lights.UpdateLights(Color.White, false);
                        break;
                };
            }

            // Отображение тревоги на дисплее и лампах
            private void ShowAlarm()
            // !!!ВЫПОЛНЕНО!!! TODO: Подумать как передавать объект Alarm из списка тревог Alarm System и правильно его здесь обрабатывать
            // TODO: Отображать не только 1-ю ошибку в списке, а все друг за другом.
            // TODO: Добавить возможность задавать длительнось сообщения тревоги динамически, а не фиксировано как сейчас.
            // TODO: Вынести всю обработку тревоги в отдельный класс
            {
                string alarm_text = AlarmList[0].Text;
                string sound = AlarmList[0].Sound;
                Screens.UpdateDisplays($"!!!ВНИМАНИЕ!!!\n{alarm_text}", Color.Red, Color.White);
                Lights.UpdateLights(Color.Red, true);

                // Первый запуск тревоги
                if (_memAlarmNumber == 0)
                {

                    foreach (IMySoundBlock speaker in _hangarSpeakers)
                    {
                        speaker.SelectedSound = sound;
                        speaker.LoopPeriod = 3;
                        speaker.Play();
                    }
                }
                _alarmTimer += 1; // Тик в 1.5 сек

                // Раз в 10 тиков. Описание тревоги
                if (_alarmTimer % 10 == 0)
                {
                    foreach (IMySoundBlock speaker in _hangarSpeakers)
                    {
                        speaker.Stop();
                        speaker.SelectedSound = sound;
                        speaker.LoopPeriod = 60;
                        speaker.Play();
                    }
                    _alarmMessageDuration = 0;
                }
                // Пищалка тревоги
                else if (_memAlarmNumber != 0)
                {
                    if (_alarmMessageDuration > 1)
                    {
                        foreach (IMySoundBlock speaker in _hangarSpeakers)
                        {
                            if (speaker.SelectedSound == sound || speaker.SelectedSound == "Arc" + sound)
                            {
                                speaker.SelectedSound = "SoundBlockAlert2";
                                speaker.LoopPeriod = 60;
                                speaker.Play();
                            }
                        }
                        
                    }
                    else _alarmMessageDuration += 1;
                }
            }

            // Отключение тревоги
            public void DisableAlarm()

            {
                _alarmTimer = 0;
                _hangarSpeakers.ForEach(speaker => speaker.Stop());
            }

            private static void ShowRoofState<T>(T display, List<IMyMotorAdvancedStator> hinges, string roofState) where T : IMyTextSurface
            {
                display.WriteText($"{roofState}\n", false);
                hinges.ForEach(hinge => display.WriteText($"{hinge.CustomName}: {Math.Round(RadToDeg(hinge.Angle), 0)}\n", true));
            }

            private static void ShowDoorState<T>(T display, List<IMyAirtightHangarDoor> doors, string doorState) where T : IMyTextSurface
            {
                display.WriteText($"{doorState}\n", false);
                doors.ForEach(door => display.WriteText($"{door.CustomName}: {door.Status}\n", true));
            }


            // TODO: Не тестирован, нужно имплементировать куда-то для теста
            public void ShowOnDisplay(IMyTextPanel display)
            {
                if (display != null)
                {
                    if (_hasRoof) ShowRoofState(display, _hangarHinges, Roof1.RoofState);
                    else if (_hasDoor) ShowDoorState(display, _hangarDoors, Gate1.GateState);
                }
            }

            // TODO: Не тестирован, нужно имплементировать куда-то для теста
            public void ShowOnDisplay(IMyTextSurface display)
            {
                if (display != null)
                {
                    if (_hasRoof) ShowRoofState(display, _hangarHinges, Roof1.RoofState);
                    else if (_hasDoor) ShowDoorState(display, _hangarDoors, Gate1.GateState);
                }
            }

            // Циклическая рутина контроля состояний и отображения статусов
            public void Monitoring()
            // TODO: Подумать как привести в человеческий вид
            {
                Roof1?.CheckRoof();
                Gate1?.CheckGate();
                if (AlarmList.Count > 0)
                {
                    ShowAlarm();
                }
                else
                {
                    if (Roof1 != null) ShowStatus(Roof1.RoofState, "СТВОРКИ");
                    else if (Gate1 != null) ShowStatus(Gate1.GateState, "ВОРОТА");
                }

                if (AlarmList.Count == 0 && _memAlarmNumber != 0) DisableAlarm();
                _memAlarmNumber = AlarmList.Count;
            }
        }

        readonly Dictionary<string, ISector> _sectors = new Dictionary<string, ISector>();
        private readonly HangarControl _hangar1;
        private readonly HangarControl _hangar2;
        private readonly HangarControl _hangar3;
        private readonly HangarControl _production;
        private readonly AlarmSystem _alarmSystem;
        private readonly ControlRoom _controlRoom;
        private readonly EnergySystem _energy;

        public Program()
        {
            _controlRoom = new ControlRoom(this, "ЦУП");
            _hangar1 = new HangarControl(this, "Ангар 1", hasDoor: true);
            _hangar2 = new HangarControl(this, "Ангар 2", hasDoor: true, hasRoof: true);
            _hangar3 = new HangarControl(this, "Ангар 3", hasDoor: true);
            _production = new HangarControl(this, "Производство");
            _energy = new EnergySystem(this, Me);
            _sectors.Add("hangar1", _hangar1);
            _sectors.Add("hangar2", _hangar2);
            _sectors.Add("hangar3", _hangar3);
            _sectors.Add("production", _production);
            _sectors.Add("control room", _controlRoom);

            _alarmSystem = new AlarmSystem(this);
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
        }

        public void Main(string argument, UpdateType updateSource)
        {
            switch (updateSource)
            {
                case UpdateType.Update100:
                    //Выполняется каждые 1.5 сек
                    // TODO: Разобраться почему не работает запись формата "sectors.Values.ForEach(sector => sector.Monitoring());", а работает стандартный foreach цикл
                    if (_alarmSystem.DetectAlarms())
                    {
                        foreach (ISector sector in _sectors.Values)
                        //TODO: Понять почему нужно преобразование ToList и откуда вообще берется IEnumerable
                        {
                            sector.AlarmList = _alarmSystem.CurrentAlarms.Where(alarm => alarm.Zone == "БАЗА" || alarm.Zone == sector.SectorName).ToList();
                        }
                    }
                    else
                    {
                        foreach (ISector sector in _sectors.Values)
                        {
                            sector.AlarmList.Clear();
                        }
                    }
                    foreach (ISector sector in _sectors.Values)
                    {
                        sector.Monitoring();
                    }
                    _controlRoom.ShowHangarConnectorInfo("Ангар 1", 0, _hangar1.GetConnectorsInfo());
                    _controlRoom.ShowHangarConnectorInfo("Ангар 2", 1, _hangar2.GetConnectorsInfo());
                    _controlRoom.ShowHangarConnectorInfo("Ангар 3", 2, _hangar3.GetConnectorsInfo());
                    _controlRoom.ShowEnergyStatus(_energy.GetEnergyInfo());
                    break;

                case UpdateType.Terminal:
                    // Выполняется при "Выполнить" через терминал
                    break;
                case UpdateType.Script:
                    // Выполняется по запросу от другого программируемого блока
                    switch (argument)
                    {
                        case "hangar1 toggle_door":
                            _hangar1.Gate1.ToggleGate();
                            break;
                        case "hangar1 toggle_light":
                            _hangar1.Lights.ToggleLights();
                            break;
                        case "hangar1 toggle_roof":
                            _hangar1.Roof1.ToggleRoof();
                            break;
                        case "hangar2 toggle_door":
                            _hangar2.Gate1.ToggleGate();
                            break;
                        case "hangar2 toggle_light":
                            _hangar2.Lights.ToggleLights();
                            break;
                        case "hangar2 toggle_roof":
                            _hangar2.Roof1.ToggleRoof();
                            break;
                        case "hangar3 toggle_door":
                            _hangar3.Gate1.ToggleGate();
                            break;
                        case "hangar3 toggle_light":
                            _hangar3.Lights.ToggleLights();
                            break;
                        case "hangar3 toggle_roof":
                            _hangar3.Roof1.ToggleRoof();
                            break;
                        case "control_room toggle_light":
                            _controlRoom.Lights.ToggleLights();
                            break;
                    }
                    break;
            }
        }
        //------------END--------------
    }
}
