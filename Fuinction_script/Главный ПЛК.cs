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
using static Script5.Program;
using VRage.Scripting;
using static Script5.Program.AlarmSystem;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography.X509Certificates;

namespace Script5
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

            // Вкл./Выкл. свет в ангаре
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

        internal class GateControl : IGate
        {
            public string Gate_state { get; set; }
            private readonly List<IMyAirtightHangarDoor> _gate_doors = new List<IMyAirtightHangarDoor>();

            public GateControl(List<IMyAirtightHangarDoor> gateDoors)
            {
                _gate_doors = gateDoors;
                Gate_state = "NA";
            }


            public void OpenGate() => _gate_doors.ForEach(door => door.OpenDoor());
            // Откр. ворота

            public void CloseGate() => _gate_doors.ForEach(door => door.CloseDoor());
            // Закр. ворота


            public void ToggleGate()
            // Откр./Закр. ворота
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

            public void CheckGate()
            // Проверить состояние ворот на откр. или закр. и т.д.
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

            public void OpenRoof() => _roof_hinges.ForEach(hinge => hinge.TargetVelocityRPM = Math.Round(RadToDeg(hinge.Angle), 0) != _open_state ? 1 : 0);
            // Открыть крышу

            public void CloseRoof() => _roof_hinges.ForEach(hinge => hinge.TargetVelocityRPM = Math.Round(RadToDeg(hinge.Angle), 0) != _close_state ? -1 : 0);
            // Закрыть крышу

            public void ToggleRoof()
            // Откр./Закр. крышу
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

            public void CheckRoof()
            // Проверить состояние крыши на откр. или закр. и т.д.
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

            public void UpdateDisplays(string text, Color backgroundColor, Color fontColor)
            // Обновление содержимого дисплеев
            {
                foreach (IMyTextPanel display in _displays)
                {
                    display.WriteText(text, false);
                    if (display.BackgroundColor != backgroundColor) display.BackgroundColor = backgroundColor;
                    if (display.FontColor != fontColor) display.FontColor = fontColor;
                }
            }
        }

        internal class Alarm
        // TODO: Расширить класс (текст тревоги, зона тревоги, уровень тревоги, текст звука тревоги)
        {

            string alarm_text;
            string alarm_zone;
            string alarm_sound;

            public Alarm(string alarmText, string alarmZone, string alarmSound)
            {
                alarm_text = alarmText;
                alarm_zone = alarmZone;
                alarm_sound = alarmSound;
            }

            public string Text
            {
                get { return alarm_text; }
                set { alarm_text = value; }
            }

            public string Zone
            {
                get { return alarm_zone; }
                set { alarm_zone = value; }
            }

            public string Sound
            {
                get { return alarm_sound; }
                set { alarm_sound = value; }
            }
        }

        //Класс инфы об корабле
        internal class ShipInfo
            //TODO: Посмотреть, есть ли упрощенная запись. Слишком длинно.
        {
            string ship_name;
            float battery_level;
            float max_battery_level;
            float current_capacity;
            float max_capacity;

            public ShipInfo(string ShipName, float BatteryLevel, float MaxBatteryLevel, float CurrentCapacity, float MaxCapacity)
            {
                ship_name = ShipName;
                battery_level = BatteryLevel;
                max_battery_level = MaxBatteryLevel;
                max_capacity = MaxCapacity;
                current_capacity = CurrentCapacity;
            }

            public string ShipName
            {
                get { return ship_name; }
                set { ship_name = value; }
            }

            public float BatteryLevel
            {
                get { return battery_level; }
                set { battery_level = value; }
            }

            public float MaxBatteryLevel
            {
                get { return max_battery_level; }
                set { max_battery_level = value; }
            }

            public float CurrentCapacity
            {
                get { return current_capacity; }
                set { current_capacity = value; }
            }

            public float MaxCapacity
            {
                get { return max_capacity; }
                set { max_capacity = value; }
            }

        }
        #endregion


        #region Вспомогательный функции
        // Конвертация из радиан в градусы
        public static float RadToDeg(float radValue)
        {
            return radValue * 180f / (float)Math.PI;
        }

        #endregion
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
            private bool Enemy_detected()
            {
                return turrets.FindAll(turret => turret.HasTarget).Count > 0;
            }

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
        // TODO: Выделить методы и свойства двери и крыши в отдельный класс из класса HangarControl 


        internal class ControlRoom: ISector
        {
            readonly Program _program;
            readonly List<IMyTextPanel> _displays = new List<IMyTextPanel>();
            readonly IMyBlockGroup control_room_group;
            readonly List<IMyTextPanel> _hangar_displays = new List<IMyTextPanel>();
            public string Sector_name { get; set; }
            public List<Alarm> Alarm_list { get; set; } = new List<Alarm>();

            public ControlRoom(Program program, string controlRoomName)
            {

                _program = program;
                Sector_name = controlRoomName;
                control_room_group = _program.GridTerminalSystem.GetBlockGroupWithName(controlRoomName);
                control_room_group.GetBlocksOfType(_displays);

                //Пред. настройка дисплеев
                foreach (IMyTextPanel display in _displays)
                {
                    display.FontSize = 2.0f;
                    display.Alignment = TextAlignment.CENTER;
                    display.TextPadding = 5;
                }

                _hangar_displays = _displays.Where(display => display.CustomData.Contains("hangar")).ToList();
            }

            //Метод отображения на определенном ангарном дисплее инфы об коннекторах ангара ( не доделано)
            public void ShowHangarConnectorInfo(string hangarName, int display_index, Dictionary<string, ShipInfo> connectors_info)
            {
                if (display_index < _hangar_displays.Count)
                {
                    string text = $"{hangarName}\n";
                    foreach (KeyValuePair<string, ShipInfo> connector_info in connectors_info)
                    {
                        string ship_info = connector_info.Value.ShipName != "НЕ ПОДКЛЮЧЕН" ? connector_info.Value.ShipName : "НЕ ПОДКЛЮЧЕН";
                        text += $"{connector_info.Key}: {ship_info}\n";
                    }

                    _hangar_displays[display_index].WriteText(text, false);
                }
            }

            // Заглушка для интерфейса надо потом разнести
            // TODO: Убрать когда отпадет необходимость
            public void Monitoring()
            { 
            }
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

            public Dictionary<string, ShipInfo> GetConnectorsInfo()
            // TODO: Есть баг, что если в одном ангаре будет 2 коннектора с одним именем то получится ошибка по ключу
            {
                Dictionary<string, ShipInfo> connectors_info = new Dictionary<string, ShipInfo>();
                foreach (IMyShipConnector connector in hangar_connectors)
                {
                    if (connector.Status == MyShipConnectorStatus.Connected)
                    {
                        connectors_info.Add(connector.CustomName, new ShipInfo(connector.OtherConnector.CubeGrid.CustomName, 0, 0, 0, 0));
                    }
                    else
                    {
                        connectors_info.Add(connector.CustomName, new ShipInfo("НЕ ПОДКЛЮЧЕН", 0, 0, 0, 0));
                    }
                }
                return connectors_info;
            }

            public void ShowStatus(string blockState, string blockName)
            // Отображение состояний на дисплеях и лампах.
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

            private void ShowAlarm()
            // Отображение тревоги на дисплее и лампах
            // !!!ВЫПОЛНЕНО!!! TODO: Подумать как передавать объект Alarm из списка тревог Alarm System и правильно его здесь обрабатывать
            // TODO: Отображать не только 1-ю ошибку в списке, а все друг за другом.
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
                        speaker.LoopPeriod = 3;
                        speaker.Play();
                    }
                }
                // Пищалка тревоги
                else if (_mem_alarm_number != 0)
                {
                    foreach (IMySoundBlock speaker in hangar_speakers)
                    {
                        if (speaker.SelectedSound == sound)
                        {
                            speaker.SelectedSound = "SoundBlockAlert2";
                            speaker.LoopPeriod = 60;
                            speaker.Play();
                        }
                    }
                }
            }

            public void DisableAlarm()
            // Отключение тревоги
            {
                _alarm_timer = 0;
                hangar_speakers.ForEach(speaker => speaker.Stop());
            }

            // TODO: Придумать, как использовать один метод вместо 2-х ShowRoofState почти одинаковых прегрузок
            private static void ShowRoofState(IMyTextSurface display, List<IMyMotorAdvancedStator> hinges, string roof_state)
            {
                display.WriteText($"{roof_state}\n", false);
                foreach (IMyMotorAdvancedStator hinge in hinges)
                {
                    display.WriteText($"{hinge.CustomName}: {Math.Round(RadToDeg(hinge.Angle), 0)}\n", true);
                }
            }

            private static void ShowRoofState(IMyTextPanel display, List<IMyMotorAdvancedStator> hinges, string roof_state)
            {
                display.WriteText($"{roof_state}\n", false);
                foreach (IMyMotorAdvancedStator hinge in hinges)
                {
                    display.WriteText($"{hinge.CustomName}: {Math.Round(RadToDeg(hinge.Angle), 0)}\n", true);
                }
            }

            // TODO: Придумать, как использовать один метод вместо 2-х ShowDoorState почти одинаковых прегрузок
            private static void ShowDoorState(IMyTextPanel display, List<IMyAirtightHangarDoor> doors, string door_state)
            {
                display.WriteText($"{door_state}\n", false);
                foreach (IMyAirtightHangarDoor door in doors)
                {
                    display.WriteText($"{door.CustomName}: {door.Status}\n", true);
                }
            }

            private static void ShowDoorState(IMyTextSurface display, List<IMyAirtightHangarDoor> doors, string door_state)
            {
                display.WriteText($"{door_state}\n", false);
                foreach (IMyAirtightHangarDoor door in doors)
                {
                    display.WriteText($"{door.CustomName}: {door.Status}\n", true);
                }
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


            public void Monitoring()
            // Циклическая рутина контроля состояний и отображения статусов
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

        public Program()
        {
            control_room = new ControlRoom(this, "ЦУП");
            Hangar1 = new HangarControl(this, "Ангар 1", hasDoor: true);
            Hangar2 = new HangarControl(this, "Ангар 2", hasDoor: true, hasRoof: true);
            Hangar3 = new HangarControl(this, "Ангар 3", hasDoor: true);
            Production = new HangarControl(this, "Производство");
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
                    // !!!ВЫПОЛНЕНО!!! TODO: Передавать в класс сектора не только текст ошибки, а список ошибок касающийся только сектора
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
                    }
                    break;
            }
        }
        //------------END--------------
    }
}
