using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using VRageMath;
using VRage.Game;
using Sandbox.ModAPI.Interfaces;
using Sandbox.ModAPI.Ingame;
using Sandbox.Game.EntityComponents;
using VRage.Game.Components;
using VRage.Collections;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using VRage.Game.GUI.TextPanel;
using VRage;
using VRage.Game.ModAPI.Ingame.Utilities;
using Sandbox.Game.Lights;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using VRage.Voxels.Mesh;
using VRage.Scripting;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography.X509Certificates;

namespace Script9
{
    public sealed class Program : MyGridProgram
    {

        //------------START--------------

        #region Интерфейсы
        interface ISector
        {
            string Sector_name { get; set; }
            List<Alarm> Alarm_list { get; set; }
            void Monitoring();
        }

        interface IGate
        {
            void OpenGate();
            void CloseGate();
            void ToggleGate();
            string Gate_state { get; }
        }

        interface IRoof
        {
            void OpenRoof();
            void CloseRoof();
            void ToggleRoof();
            string Roof_state { get; }
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
            public string Gate_state { get; set; }
            private readonly List<IMyAirtightHangarDoor> _gate_doors = new List<IMyAirtightHangarDoor>();

            public GateControl(List<IMyAirtightHangarDoor> gateDoors)
            {
                _gate_doors = gateDoors;
                Gate_state = "NA";
            }

            // Откр. ворота
            public void OpenGate() => _gate_doors.ForEach(door => door.OpenDoor());

            // Закр. ворота
            public void CloseGate() => _gate_doors.ForEach(door => door.CloseDoor());


            // Откр./Закр. ворота
            public void ToggleGate()
            {
                switch (Gate_state)
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
                Gate_state = "NA";
                Gate_state = _gate_doors.FindAll(door => door.Status == DoorStatus.Closing).Count == _gate_doors.Count ? "ЗАКРЫВАЮТСЯ" : Gate_state;
                Gate_state = _gate_doors.FindAll(door => door.Status == DoorStatus.Opening).Count == _gate_doors.Count ? "ОТКРЫВАЮТСЯ" : Gate_state;
                Gate_state = _gate_doors.FindAll(door => door.Status == DoorStatus.Open).Count == _gate_doors.Count ? "ОТКРЫТО" : Gate_state;
                Gate_state = _gate_doors.FindAll(door => door.Status == DoorStatus.Closed).Count == _gate_doors.Count ? "ЗАКРЫТО" : Gate_state;
            }
        }

        internal class RoofControl : IRoof
        {
            public string Roof_state { get; set; }
            private readonly List<IMyMotorAdvancedStator> _roof_hinges = new List<IMyMotorAdvancedStator>();
            private readonly float _open_state; // Положение шарниров для состояния "ОТКРЫТО" в градусах
            private readonly float _close_state; // Положение шарниров для состояния "ЗАКРЫТО" в градусах

            public RoofControl(List<IMyMotorAdvancedStator> roofHinges, float openState = 0f, float closeState = -90f)
            {
                _roof_hinges = roofHinges;
                _open_state = openState;
                _close_state = closeState;
                Roof_state = "NA";
            }

            // Открыть крышу
            public void OpenRoof() => _roof_hinges.ForEach(hinge => hinge.TargetVelocityRPM = Math.Round(RadToDeg(hinge.Angle), 0) != _open_state ? 1 : 0);

            // Закрыть крышу
            public void CloseRoof() => _roof_hinges.ForEach(hinge => hinge.TargetVelocityRPM = Math.Round(RadToDeg(hinge.Angle), 0) != _close_state ? -1 : 0);

            // Откр./Закр. крышу
            public void ToggleRoof()
            {
                switch (Roof_state)
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
                Roof_state = "NA";
                Roof_state = _roof_hinges.FindAll(hinge => hinge.TargetVelocityRPM < 0f && hinge.Enabled).Count == _roof_hinges.Count ? "ЗАКРЫВАЮТСЯ" : Roof_state;
                Roof_state = _roof_hinges.FindAll(hinge => hinge.TargetVelocityRPM > 0f && hinge.Enabled).Count == _roof_hinges.Count ? "ОТКРЫВАЮТСЯ" : Roof_state;
                Roof_state = _roof_hinges.FindAll(hinge => Math.Round(RadToDeg(hinge.Angle)) == _open_state).Count == _roof_hinges.Count ? "ОТКРЫТО" : Roof_state;
                Roof_state = _roof_hinges.FindAll(hinge => Math.Round(RadToDeg(hinge.Angle)) == _close_state).Count == _roof_hinges.Count ? "ЗАКРЫТО" : Roof_state;
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
            readonly string alarm_text;
            readonly string alarm_zone;
            readonly string alarm_sound;

            public Alarm(string alarmText, string alarmZone, string alarmSound)
            {
                alarm_text = alarmText;
                alarm_zone = alarmZone;
                alarm_sound = alarmSound;
            }
            public string Text => alarm_text;
            public string Zone => alarm_zone;
            public string Sound => alarm_sound;
        }

        internal class InventorySystem
        {
            readonly Program _program;
            readonly List<IMyTerminalBlock> _blocks_with_inventory = new List<IMyTerminalBlock>();
            readonly List<IMyCargoContainer> _cargo_containers = new List<IMyCargoContainer>();
            readonly List<IMyInventory> _all_inventories = new List<IMyInventory>();
            readonly List<IMyInventory> _containers_inventories = new List<IMyInventory>();

            public InventorySystem(Program program, IMyTerminalBlock reference_block)
            {
                _program = program;
                _program.GridTerminalSystem.GetBlocksOfType(_blocks_with_inventory, block => block.IsSameConstructAs(reference_block) && block.HasInventory);
                _cargo_containers = _blocks_with_inventory.OfType<IMyCargoContainer>().ToList();
                _all_inventories = _blocks_with_inventory.Select(block => block.GetInventory()).ToList();
                _containers_inventories = _cargo_containers.Select(block => block.GetInventory()).ToList();
            }

            public double CurrentVolumeTotal => SumCurrentVolumes(_all_inventories);
            public double MaxVolumeTotal => SumMaxVolumes(_all_inventories);
            public int FilledRatioTotal => FilledRatio(CurrentVolumeTotal, MaxVolumeTotal);
            public double CurrentVolumeCargo => SumCurrentVolumes(_containers_inventories);
            public double MaxVolumeCargo => SumMaxVolumes(_containers_inventories);
            public int FilledRatioCargo => FilledRatio(CurrentVolumeCargo, MaxVolumeCargo);

            private static double SumCurrentVolumes(List<IMyInventory> inventories) => inventories.Sum(inventory => inventory.CurrentVolume.RawValue);
            private static double SumMaxVolumes(List<IMyInventory> inventories) => inventories.Sum(inventory => inventory.MaxVolume.RawValue);
            private static int FilledRatio(double current_value, double max_value) => (int)Math.Round(current_value / max_value * 100, 2);
        }



        //Класс инфы об корабле
        internal class ShipInfo
        //TODO: Посмотреть, есть ли упрощенная запись. Слишком длинно.
        {
            readonly EnergySystem _ship_energy_system;
            readonly InventorySystem _ship_inventory_system;
            readonly string _ship_name;

            public ShipInfo(Program program, IMyTerminalBlock reference_block)
            {
                _ship_name = reference_block.CubeGrid.CustomName;
                _ship_energy_system = new EnergySystem(program, reference_block);
                _ship_inventory_system = new InventorySystem(program, reference_block);
            }

            public string ShipName => _ship_name;
            public int BatteryLevel => _ship_energy_system.BatteryLevel;
            public int HydrogenLevel => _ship_energy_system.HydrogenTanksLevel;
            public int OxygenLevel => _ship_energy_system.OxygenTanksLevel;
            public int CargoHoldTotalFilledRatio => _ship_inventory_system.FilledRatioTotal;
            public int CargoHoldContainersFilledRatio => _ship_inventory_system.FilledRatioCargo;

        }

        //Класс инфы об подключенном корабле и коннекторе
        internal class ConnectedShipInfo
        {
            readonly string connector_name;
            readonly MyShipConnectorStatus connector_status;
            readonly ShipInfo ship_info;

            public ConnectedShipInfo(string ConnectorName, MyShipConnectorStatus ConnectorStatus, Program program, IMyTerminalBlock reference_block = null)
            {
                connector_name = ConnectorName;
                connector_status = ConnectorStatus;
                if (reference_block != null) ship_info = new ShipInfo(program, reference_block);
            }

            public string ConnectorName => connector_name;
            public MyShipConnectorStatus ConnectorStatus => connector_status;
            public ShipInfo ShipInfo => ship_info;
        }

        internal class EnergyInfo
        {
            readonly int batteries_level;
            readonly int hydrogen_level;
            readonly int oxygen_level;
            readonly int power_load;

            public EnergyInfo(int BatteriesLevel, int HydrogenLevel, int OxygenLevel, int PowerLoad)
            {
                batteries_level = BatteriesLevel;
                hydrogen_level = HydrogenLevel;
                oxygen_level = OxygenLevel;
                power_load = PowerLoad;
            }

            public int BatteriesLevel => batteries_level;
            public int HydrogenLevel => hydrogen_level;
            public int OxygenLevel => oxygen_level;
            public int PowerLoad => power_load;
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
            readonly Program _program;
            readonly List<IMyWarhead> warheads = new List<IMyWarhead>();
            readonly List<IMyLargeTurretBase> turrets = new List<IMyLargeTurretBase>();
            bool warhead_detected;

            internal AlarmSystem(Program program)
            {
                _program = program;
                _program.GridTerminalSystem.GetBlocksOfType(turrets, turret => turret.IsSameConstructAs(_program.Me));
            }

            internal List<Alarm> CurrentAlarms { get; } = new List<Alarm>();

            private bool Detect_warheads()
            {
                _program.GridTerminalSystem.GetBlocksOfType(warheads, warhead => warhead.IsSameConstructAs(_program.Me));
                warhead_detected = warheads.Count > 0;
                warheads.ForEach(warhead => warhead.IsArmed = false);
                return warhead_detected;
            }
            // TODO: Метод на поднятие тревоги если у турелей цель (НЕ ПРОВЕРЕН до конца. Есть подозрение, что не работает из-за WeaponCore)
            private bool Enemy_detected() => turrets.FindAll(turret => turret.HasTarget).Count > 0;

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
            readonly Program _program;
            readonly List<IMyPowerProducer> _power_producers = new List<IMyPowerProducer>();
            readonly List<IMyBatteryBlock> _batteries = new List<IMyBatteryBlock>();
            readonly List<IMyTerminalBlock> _hydrogen_engines = new List<IMyTerminalBlock>();
            readonly List<IMyGasTank> _hydrogen_tanks = new List<IMyGasTank>();
            readonly List<IMyGasTank> _oxygen_tanks = new List<IMyGasTank>();
            private readonly string[] _hydrogen_tanks_subtypes = { "LargeHydrogenTank", "LargeHydrogenTankIndustrial", "SmallHydrogenTankSmall", "LargeHydrogenTankSmall", "SmallHydrogenTank" };
            private readonly string[] _oxygen_tanks_subtypes = { "", "OxygenTankSmall" };

            public EnergySystem(Program program, IMyTerminalBlock reference_block)
            {
                _program = program;
                _program.GridTerminalSystem.GetBlocksOfType(_power_producers, producer => producer.IsSameConstructAs(reference_block));
                _program.GridTerminalSystem.GetBlocksOfType(_batteries, battery => battery.IsSameConstructAs(reference_block));
                _program.GridTerminalSystem.GetBlocksOfType(_hydrogen_engines, engine => engine.BlockDefinition.SubtypeName == "LargeHydrogenEngine" && engine.IsSameConstructAs(reference_block));
                _program.GridTerminalSystem.GetBlocksOfType(_hydrogen_tanks, tank => _hydrogen_tanks_subtypes.Contains(tank.BlockDefinition.SubtypeName) && tank.IsSameConstructAs(reference_block));
                _program.GridTerminalSystem.GetBlocksOfType(_oxygen_tanks, tank => _oxygen_tanks_subtypes.Contains(tank.BlockDefinition.SubtypeName) && tank.IsSameConstructAs(reference_block));
            }

            public int HydrogenTanksLevel => GetTanksLevel(_hydrogen_tanks);
            public int OxygenTanksLevel => GetTanksLevel(_oxygen_tanks);
            public double HydrogenTanksCurrentVolume => GetTanksCurrentVolume(_hydrogen_tanks);
            public double HydrogenTanksMaxVolume => GetTanksMaxVolume(_hydrogen_tanks);
            public double OxygenTanksCurrentVolume => GetTanksCurrentVolume(_oxygen_tanks);
            public double OxygenTanksMaxVolume => GetTanksMaxVolume(_oxygen_tanks);
            public float BatteryStoredPower => GetBatteryStoredPower();
            public float BatteryMaxStoredPower => GetBatteryMaxStoredPower();
            public int BatteryLevel => GetBatteryLevel();
            public int PowerLoad => GetPowerLoad();

            public EnergyInfo GetEnergyInfo() => new EnergyInfo(BatteryLevel, HydrogenTanksLevel, OxygenTanksLevel, PowerLoad);

            private float GetBatteryStoredPower()
            {
                if (_batteries.Count == 0) return 0;
                float current_stored_power = _batteries.Sum(battery => battery.CurrentStoredPower);
                return current_stored_power;
            }

            private float GetBatteryMaxStoredPower()
            {
                if (_batteries.Count == 0) return 0;
                float max_stored_power = _batteries.Sum(battery => battery.MaxStoredPower);
                return max_stored_power;
            }

            private int GetBatteryLevel()
            {
                if (_batteries.Count == 0) return 0;
                int battery_in_percentage = (int)Math.Round(BatteryStoredPower / BatteryMaxStoredPower * 100, 0);
                return battery_in_percentage;
            }

            private static int GetTanksLevel<T>(List<T> tanks) where T : IMyGasTank
            {
                if (tanks.Count == 0) return 0;
                int filled_ratio = (int)Math.Round((tanks.Sum(tank => tank.FilledRatio)) / tanks.Count * 100, 2);
                return filled_ratio;
            }

            private static double GetTanksCurrentVolume<T>(List<T> tanks) where T : IMyGasTank
            {
                if (tanks.Count == 0) return 0;
                double current_volume = tanks.Sum(tank => tank.FilledRatio * tank.Capacity);
                return current_volume;
            }

            private static double GetTanksMaxVolume<T>(List<T> tanks) where T : IMyGasTank
            {
                if (tanks.Count == 0) return 0;
                double max_volume = tanks.Sum(tank => tank.Capacity);
                return max_volume;
            }

            private int GetPowerLoad()
            {
                if (_power_producers.Count == 0) return 0;
                float current_output = _power_producers.Sum(producer => producer.CurrentOutput);
                float max_output = _power_producers.Sum(producer => producer.MaxOutput);
                return (int)Math.Round(current_output / max_output * 100, 2);
            }
        }


        internal class ControlRoom : ISector
        {
            readonly Program _program;
            readonly List<IMyTextPanel> _displays = new List<IMyTextPanel>();
            readonly IMyBlockGroup control_room_group;
            readonly List<IMyTextPanel> _hangar_displays = new List<IMyTextPanel>();
            readonly List<IMyLightingBlock> _room_lights = new List<IMyLightingBlock>();
            readonly IMyTextPanel _power_display;
            internal LightControl Lights { get; }
            public string Sector_name { get; set; }
            public List<Alarm> Alarm_list { get; set; } = new List<Alarm>();

            public ControlRoom(Program program, string controlRoomName)
            {

                _program = program;
                Sector_name = controlRoomName;
                control_room_group = _program.GridTerminalSystem.GetBlockGroupWithName(controlRoomName);
                control_room_group.GetBlocksOfType(_displays);
                control_room_group.GetBlocksOfType(_room_lights);

                Lights = new LightControl(_room_lights);

                //Пред. настройка дисплеев
                foreach (IMyTextPanel display in _displays)
                {
                    display.FontSize = 1.4f;
                    display.Alignment = TextAlignment.CENTER;
                    display.TextPadding = 5;
                }

                _hangar_displays = _displays.Where(display => display.CustomData.Contains("hangar")).ToList();
                _power_display = _displays.Find(display => display.CustomData.Contains("power"));
            }

            public void ShowEnergyStatus(EnergyInfo energy_info)
            {
                if (_power_display != null)
                {
                    string text = "";
                    text += $"Батареи: {energy_info.BatteriesLevel}%\n";
                    text += $"Водородные баки: {energy_info.HydrogenLevel}%\n";
                    text += $"Кислородные баки: {energy_info.OxygenLevel}%\n";
                    text += $"Нагрузка сети: {energy_info.PowerLoad}%\n";
                    _power_display.WriteText(text, false);
                }
            }

            //Метод отображения на определенном ангарном дисплее инфы об коннекторах ангара
            public void ShowHangarConnectorInfo(string hangarName, int display_index, Dictionary<long, ConnectedShipInfo> connectors_info)
            {
                if (display_index < _hangar_displays.Count)
                {
                    string text = $"{hangarName}\n";
                    foreach (var connector_info in connectors_info)
                    {
                        text += "-----------------------------------------------------\n";
                        text += $"{connector_info.Value.ConnectorName}: {connector_info.Value.ConnectorStatus}\n";
                        if (connector_info.Value.ConnectorStatus == MyShipConnectorStatus.Connected)
                        {

                            text += $"{connector_info.Value.ShipInfo.ShipName}\n";
                            text += $"ТРЮМ: {connector_info.Value.ShipInfo.CargoHoldTotalFilledRatio}% ";
                            text += $"БАТАРЕИ: {connector_info.Value.ShipInfo.BatteryLevel}%\n";
                        }
                    }
                    _hangar_displays[display_index].WriteText(text, false);
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
            readonly List<IMyLightingBlock> hangar_lights = new List<IMyLightingBlock>();
            readonly List<IMyMotorAdvancedStator> hangar_hinges = new List<IMyMotorAdvancedStator>();
            readonly List<IMyTextPanel> hangar_displays = new List<IMyTextPanel>();
            readonly List<IMyAirtightHangarDoor> hangar_doors = new List<IMyAirtightHangarDoor>();
            readonly List<IMySoundBlock> hangar_speakers = new List<IMySoundBlock>();
            readonly List<IMyShipConnector> hangar_connectors = new List<IMyShipConnector>();
            public List<Alarm> Alarm_list { get; set; } = new List<Alarm>();
            public IMyTextSurface PlcScreen1 { get; }
            public string Sector_name { get; set; }
            int _mem_alarm_number;
            readonly bool _has_door;
            readonly bool _has_roof;
            int _alarm_timer;
            int _alarm_message_duration;
            internal GateControl Gate1 { get; }
            internal RoofControl Roof1 { get; }
            internal LightControl Lights { get; }
            public DisplayControl Screens { get; }
            readonly IMyBlockGroup hangar_group;


            public HangarControl(Program program, string hangarName, float openState = 0f, float closeState = -90f, bool hasDoor = false, bool hasRoof = false)
            {
                _program = program; //Ссылка на основную программу для возможности использовать GridTerminalSystem
                Sector_name = hangarName; // Имя группы устройств в ангаре, например "Ангар 1".
                _has_door = hasDoor; // Идентификатор наличия ворот
                _has_roof = hasRoof;  // Идентификатор наличия крыши
                PlcScreen1 = _program.Me.GetSurface(0); // Экран на программируемом блоке


                //Распределение блоков по типам в соответствующие списки
                hangar_group = _program.GridTerminalSystem.GetBlockGroupWithName(Sector_name);
                hangar_group.GetBlocksOfType(hangar_lights);
                hangar_group.GetBlocksOfType(hangar_hinges);
                hangar_group.GetBlocksOfType(hangar_doors);
                hangar_group.GetBlocksOfType(hangar_displays);
                hangar_group.GetBlocksOfType(hangar_speakers);
                hangar_group.GetBlocksOfType(hangar_connectors);

                if (hasRoof) Roof1 = new RoofControl(hangar_hinges, openState, closeState);
                if (hasDoor) Gate1 = new GateControl(hangar_doors);
                Lights = new LightControl(hangar_lights);
                Screens = new DisplayControl(hangar_displays);

                // Первая операция контроля
                Monitoring();
            }

            public Dictionary<long, ConnectedShipInfo> GetConnectorsInfo()
            // TODO: Подумать как упростить
            {
                Dictionary<long, ConnectedShipInfo> connectors_info = new Dictionary<long, ConnectedShipInfo>();
                foreach (IMyShipConnector connector in hangar_connectors)
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
                        Screens.UpdateDisplays($"{Sector_name}\n{blockName}\n ОТКРЫВАЮТСЯ", Color.Yellow, Color.Black);
                        Lights.UpdateLights(Color.Yellow, true);
                        break;
                    case "ЗАКРЫВАЮТСЯ":
                        Screens.UpdateDisplays($"{Sector_name}\n{blockName}\n ЗАКРЫВАЮТСЯ", Color.Yellow, Color.Black);
                        Lights.UpdateLights(Color.Yellow, true);
                        break;
                    case "ОТКРЫТО":
                        Screens.UpdateDisplays($"{Sector_name}\n{blockName}\n ОТКРЫТЫ", Color.Green, Color.White);
                        Lights.UpdateLights(Color.Green, false);
                        break;
                    case "ЗАКРЫТО":
                        Screens.UpdateDisplays($"{Sector_name}\n{blockName}\n ЗАКРЫТЫ", Color.Black, Color.White);
                        Lights.UpdateLights(Color.White, false);
                        break;
                    default:
                        Screens.UpdateDisplays($"{Sector_name}\n{blockName}\n НЕ ОПРЕДЕЛЕНО", Color.Orange, Color.White);
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
                string alarm_text = Alarm_list[0].Text;
                string sound = Alarm_list[0].Sound;
                Screens.UpdateDisplays($"!!!ВНИМАНИЕ!!!\n{alarm_text}", Color.Red, Color.White);
                Lights.UpdateLights(Color.Red, true);

                // Первый запуск тревоги
                if (_mem_alarm_number == 0)
                {

                    foreach (IMySoundBlock speaker in hangar_speakers)
                    {
                        speaker.SelectedSound = sound;
                        speaker.LoopPeriod = 3;
                        speaker.Play();
                    }
                }
                _alarm_timer += 1; // Тик в 1.5 сек

                // Раз в 10 тиков. Описание тревоги
                if (_alarm_timer % 10 == 0)
                {
                    foreach (IMySoundBlock speaker in hangar_speakers)
                    {
                        speaker.Stop();
                        speaker.SelectedSound = sound;
                        speaker.LoopPeriod = 60;
                        speaker.Play();
                    }
                    _alarm_message_duration = 0;
                }
                // Пищалка тревоги
                else if (_mem_alarm_number != 0)
                {
                    if (_alarm_message_duration > 1)
                    {
                        foreach (IMySoundBlock speaker in hangar_speakers)
                        {
                            if (speaker.SelectedSound == sound || speaker.SelectedSound == "Arc" + sound)
                            {
                                speaker.SelectedSound = "SoundBlockAlert2";
                                speaker.LoopPeriod = 60;
                                speaker.Play();
                            }
                        }
                        
                    }
                    else _alarm_message_duration += 1;
                }
            }

            // Отключение тревоги
            public void DisableAlarm()

            {
                _alarm_timer = 0;
                hangar_speakers.ForEach(speaker => speaker.Stop());
            }

            private static void ShowRoofState<T>(T display, List<IMyMotorAdvancedStator> hinges, string roof_state) where T : IMyTextSurface
            {
                display.WriteText($"{roof_state}\n", false);
                hinges.ForEach(hinge => display.WriteText($"{hinge.CustomName}: {Math.Round(RadToDeg(hinge.Angle), 0)}\n", true));
            }

            private static void ShowDoorState<T>(T display, List<IMyAirtightHangarDoor> doors, string door_state) where T : IMyTextSurface
            {
                display.WriteText($"{door_state}\n", false);
                doors.ForEach(door => display.WriteText($"{door.CustomName}: {door.Status}\n", true));
            }


            // TODO: Не тестирован, нужно имплементировать куда-то для теста
            public void ShowOnDisplay(IMyTextPanel display)
            {
                if (display != null)
                {
                    if (_has_roof) ShowRoofState(display, hangar_hinges, Roof1.Roof_state);
                    else if (_has_door) ShowDoorState(display, hangar_doors, Gate1.Gate_state);
                }
            }

            // TODO: Не тестирован, нужно имплементировать куда-то для теста
            public void ShowOnDisplay(IMyTextSurface display)
            {
                if (display != null)
                {
                    if (_has_roof) ShowRoofState(display, hangar_hinges, Roof1.Roof_state);
                    else if (_has_door) ShowDoorState(display, hangar_doors, Gate1.Gate_state);
                }
            }

            // Циклическая рутина контроля состояний и отображения статусов
            public void Monitoring()
            // TODO: Подумать как привести в человеческий вид
            {
                Roof1?.CheckRoof();
                Gate1?.CheckGate();
                if (Alarm_list.Count > 0)
                {
                    ShowAlarm();
                }
                else
                {
                    if (Roof1 != null) ShowStatus(Roof1.Roof_state, "СТВОРКИ");
                    else if (Gate1 != null) ShowStatus(Gate1.Gate_state, "ВОРОТА");
                }

                if (Alarm_list.Count == 0 && _mem_alarm_number != 0) DisableAlarm();
                _mem_alarm_number = Alarm_list.Count;
            }
        }

        readonly Dictionary<string, ISector> sectors = new Dictionary<string, ISector>();
        private readonly HangarControl Hangar1;
        private readonly HangarControl Hangar2;
        private readonly HangarControl Hangar3;
        private readonly HangarControl Production;
        private readonly AlarmSystem alarm_system;
        private readonly ControlRoom control_room;
        private readonly EnergySystem Energy;

        public Program()
        {
            control_room = new ControlRoom(this, "ЦУП");
            Hangar1 = new HangarControl(this, "Ангар 1", hasDoor: true);
            Hangar2 = new HangarControl(this, "Ангар 2", hasDoor: true, hasRoof: true);
            Hangar3 = new HangarControl(this, "Ангар 3", hasDoor: true);
            Production = new HangarControl(this, "Производство");
            Energy = new EnergySystem(this, Me);
            sectors.Add("hangar1", Hangar1);
            sectors.Add("hangar2", Hangar2);
            sectors.Add("hangar3", Hangar3);
            sectors.Add("production", Production);
            sectors.Add("control room", control_room);

            alarm_system = new AlarmSystem(this);
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
        }

        public void Main(string argument, UpdateType updateSource)
        {
            switch (updateSource)
            {
                case UpdateType.Update100:
                    //Выполняется каждые 1.5 сек
                    // TODO: Разобраться почему не работает запись формата "sectors.Values.ForEach(sector => sector.Monitoring());", а работает стандартный foreach цикл
                    if (alarm_system.DetectAlarms())
                    {
                        foreach (ISector sector in sectors.Values)
                        //TODO: Понять почему нужно преобразование ToList и откуда вообще берется IEnumerable
                        {
                            sector.Alarm_list = alarm_system.CurrentAlarms.Where(alarm => alarm.Zone == "БАЗА" || alarm.Zone == sector.Sector_name).ToList();
                        }
                    }
                    else
                    {
                        foreach (ISector sector in sectors.Values)
                        {
                            sector.Alarm_list.Clear();
                        }
                    }
                    foreach (ISector sector in sectors.Values)
                    {
                        sector.Monitoring();
                    }
                    control_room.ShowHangarConnectorInfo("Ангар 1", 0, Hangar1.GetConnectorsInfo());
                    control_room.ShowHangarConnectorInfo("Ангар 2", 1, Hangar2.GetConnectorsInfo());
                    control_room.ShowHangarConnectorInfo("Ангар 3", 2, Hangar3.GetConnectorsInfo());
                    control_room.ShowEnergyStatus(Energy.GetEnergyInfo());
                    break;

                case UpdateType.Terminal:
                    // Выполняется при "Выполнить" через терминал
                    break;
                case UpdateType.Script:
                    // Выполняется по запросу от другого программируемого блока
                    switch (argument)
                    {
                        case "hangar1 toggle_door":
                            Hangar1.Gate1.ToggleGate();
                            break;
                        case "hangar1 toggle_light":
                            Hangar1.Lights.ToggleLights();
                            break;
                        case "hangar1 toggle_roof":
                            Hangar1.Roof1.ToggleRoof();
                            break;
                        case "hangar2 toggle_door":
                            Hangar2.Gate1.ToggleGate();
                            break;
                        case "hangar2 toggle_light":
                            Hangar2.Lights.ToggleLights();
                            break;
                        case "hangar2 toggle_roof":
                            Hangar2.Roof1.ToggleRoof();
                            break;
                        case "hangar3 toggle_door":
                            Hangar3.Gate1.ToggleGate();
                            break;
                        case "hangar3 toggle_light":
                            Hangar3.Lights.ToggleLights();
                            break;
                        case "hangar3 toggle_roof":
                            Hangar3.Roof1.ToggleRoof();
                            break;
                        case "control_room toggle_light":
                            control_room.Lights.ToggleLights();
                            break;
                    }
                    break;
            }
        }
        //------------END--------------
    }
}
