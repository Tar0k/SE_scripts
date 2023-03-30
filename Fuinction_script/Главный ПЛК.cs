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

namespace Script5
{
    public sealed class Program : MyGridProgram
    {
        //------------START--------------
        #region Интерфейсы
        interface ISector
        {
            string alarm { get; set; }
            void Monitoring();
        }

        interface IGate
        {
            void OpenGate();
            void CloseGate();
            void ToggleGate();
            string gate_state { get; }
        }

        interface IRoof
        {
            void OpenRoof();
            void CloseRoof();
            void ToggleRoof();
            string roof_state { get; }
        }
        #endregion

        #region Вспомогательные классы
        public class GateControl : IGate
        {
            public string gate_state { get; set; }
            private List<IMyAirtightHangarDoor> _gate_doors = new List<IMyAirtightHangarDoor>();

            public GateControl(List<IMyAirtightHangarDoor> gate_doors)
            {
                _gate_doors = gate_doors;
                gate_state = null;
            }


            public void OpenGate()
            // Откр. ворота
            {
                _gate_doors.ForEach(door => door.OpenDoor());
            }

            public void CloseGate()
            // Закр. ворота
            {
                _gate_doors.ForEach(door => door.CloseDoor());
            }


            public void ToggleGate()
            // Откр./Закр. ворота
            {
                switch (gate_state)
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
                gate_state = null;

                gate_state = _gate_doors.FindAll(door => door.Status == DoorStatus.Closing).Count() == _gate_doors.Count() ? "ЗАКРЫВАЮТСЯ" : gate_state;
                gate_state = _gate_doors.FindAll(door => door.Status == DoorStatus.Opening).Count() == _gate_doors.Count() ? "ОТКРЫВАЮТСЯ" : gate_state;
                gate_state = _gate_doors.FindAll(door => door.Status == DoorStatus.Open).Count() == _gate_doors.Count() ? "ОТКРЫТО" : gate_state;
                gate_state = _gate_doors.FindAll(door => door.Status == DoorStatus.Closed).Count() == _gate_doors.Count() ? "ЗАКРЫТО" : gate_state;
            }
        }

        public class RoofControl : IRoof
        {
            public string roof_state { get; set; }
            private List<IMyMotorAdvancedStator> _roof_hinges = new List<IMyMotorAdvancedStator>();
            private float _open_state; // Положение шарниров для состояния "ОТКРЫТО" в градусах
            private float _close_state; // Положение шарниров для состояния "ЗАКРЫТО" в градусах

            public RoofControl(List<IMyMotorAdvancedStator> roof_hinges, float open_state = 0f, float close_state = -90f)
            {
                _roof_hinges = roof_hinges;
                _open_state = open_state;
                _close_state = close_state;
                roof_state = null;
            }

            public void OpenRoof()
            // Открыть крышу
            {
                _roof_hinges.ForEach(hinge => hinge.TargetVelocityRPM = Math.Round(RadToDeg(hinge.Angle), 0) != _open_state ? 1 : 0);
            }

            public void CloseRoof()
            // Закрыть крышу
            {
                _roof_hinges.ForEach(hinge => hinge.TargetVelocityRPM = Math.Round(RadToDeg(hinge.Angle), 0) != _close_state ? -1 : 0);
            }

            public void ToggleRoof()
            // Откр./Закр. крышу
            {
                switch (roof_state)
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
                roof_state = null;
                roof_state = _roof_hinges.FindAll(hinge => hinge.TargetVelocityRPM < 0f && hinge.Enabled).Count() == _roof_hinges.Count() ? "ЗАКРЫВАЮТСЯ" : roof_state;
                roof_state = _roof_hinges.FindAll(hinge => hinge.TargetVelocityRPM > 0f && hinge.Enabled).Count() == _roof_hinges.Count() ? "ОТКРЫВАЮТСЯ" : roof_state;
                roof_state = _roof_hinges.FindAll(hinge => Math.Round(RadToDeg(hinge.Angle)) == _open_state).Count() == _roof_hinges.Count() ? "ОТКРЫТО" : roof_state;
                roof_state = _roof_hinges.FindAll(hinge => Math.Round(RadToDeg(hinge.Angle)) == _close_state).Count() == _roof_hinges.Count() ? "ЗАКРЫТО" : roof_state;
            }
        }
        #endregion

        #region Вспомогательный функции
        public static float RadToDeg(float rad_value)
        // Конвертация из радиан в градусы
        {
            return rad_value * 180f / 3.14159265359f;
        }

        #endregion
        public class AlarmSystem
        {
            Program _program;
            public List<Alarm> current_alarms = new List<Alarm>();
            List<IMyWarhead> warheads = new List<IMyWarhead>();
            List<IMyLargeTurretBase> turrets = new List<IMyLargeTurretBase>();
            bool warhead_detected = false;

            public AlarmSystem(Program program)
            {
                _program = program;
                _program.GridTerminalSystem.GetBlocksOfType(turrets, turret => turret.IsSameConstructAs(_program.Me));
            }

            public class Alarm
            // TODO: Расширить класс (текст тревоги, зона тревоги, уровень тревоги, текст звука тревоги)
            {

                public string alarm_text;
                public string alarm_zone;

                public Alarm(string alarm_text, string alarm_zone)
                {
                    this.alarm_text = alarm_text;
                    this.alarm_zone = alarm_zone;
                }

                public string AlarmText
                {
                    get { return alarm_text; }
                    set { alarm_text = value; }
                }

                public string AlarmZone
                {
                    get { return alarm_zone; }
                    set { alarm_zone = value; }
                }
            }

            private bool detect_warheads()
            {
                _program.GridTerminalSystem.GetBlocksOfType(warheads, warhead => warhead.IsSameConstructAs(_program.Me));
                warhead_detected = warheads.Count > 0 ? true : false;
                warheads.ForEach(warhead => warhead.IsArmed = false);
                return warhead_detected;
            }
            // TODO: Метод на поднятие тревоги если у турелей цель (НЕ ПРОВЕРЕН до конца. Есть подозрение, что не работает из-за WeaponCore)
            private bool enemy_detected()
            {
                return turrets.FindAll(turret => turret.HasTarget).Count() > 0 ? true : false;
            }

            // TODO: Метод на поднятие тревоги если критически низкий уровень энергии на базе.
            // Отмена. Будет отдельный объект по энергосистеме базы. Метод будет получать инфу от туда

            public bool detect_alarms()
            {
                current_alarms.Clear();
                if (detect_warheads()) current_alarms.Add(new Alarm("БОЕГОЛОВКА", "БАЗА"));
                if (enemy_detected()) current_alarms.Add(new Alarm("ВРАГИ В РАДИУСЕ\nПОРАЖЕНИЯ", "БАЗА"));

                return current_alarms.Count() > 0 ? true : false;
            }
        }

        // TODO: Написать класс, который бы описывал объекты в производственном секторе
        // TODO: Написать класс, который бы описывал объекты генерации энергии и ее потребление
        // TODO: Выделить методы и свойства двери и крыши в отдельный класс из класса HangarControl 

        public class HangarControl : ISector
        {
            /* Класс управления блоками в ангаре
             * Выполняет управление и мониторинг состояния блоков
             */
            Program _program;
            List<IMyLightingBlock> hangar_lights = new List<IMyLightingBlock>();
            List<IMyMotorAdvancedStator> hangar_hinges = new List<IMyMotorAdvancedStator>();
            List<IMyTextPanel> hangar_displays = new List<IMyTextPanel>();
            List<IMyAirtightHangarDoor> hangar_doors = new List<IMyAirtightHangarDoor>();
            List<IMySoundBlock> hangar_speakers = new List<IMySoundBlock>();
            IMyTextSurface _plc_screen1;
            IMyTextPanel debug_display;

            public string alarm { get; set; }
            string _hangar_name;
            string _mem_alarm;
            bool _has_door = false;
            bool _has_roof = false;
            int _alarm_timer = 0;
            public GateControl Gate1;
            public RoofControl Roof1;

            public HangarControl(Program program, string hangar_name, float open_state = 0f, float close_state = -90f, bool has_door = false, bool has_roof = false)
            {
                _program = program; //Ссылка на основную программу для возможности использовать GridTerminalSystem
                _hangar_name = hangar_name; // Имя группы устройств в ангаре, например "Ангар 1".
                                            //Hangar1 = new HangarControl(this, "Ангар 1", has_door: true);
                _has_door = has_door; // Идентификатор наличия ворот
                _has_roof = has_roof;  // Идентификатор наличия крыши
                alarm = "НЕТ ТРЕВОГИ";
                _plc_screen1 = _program.Me.GetSurface(0); // Экран на программируемом блоке
                debug_display = _program.GridTerminalSystem.GetBlockWithName("debug_display") as IMyTextPanel; // Дисплей для отладки
                if (debug_display == null)
                    _plc_screen1.WriteText("Не найден дисплей\n для отладки с именем\n 'debug_display'");

                IMyBlockGroup hangar_group;
                //Распределение блоков по типам в соответствующие списки
                hangar_group = _program.GridTerminalSystem.GetBlockGroupWithName(_hangar_name);
                hangar_group.GetBlocksOfType(hangar_lights);
                hangar_group.GetBlocksOfType(hangar_hinges);
                hangar_group.GetBlocksOfType(hangar_doors);
                hangar_group.GetBlocksOfType(hangar_displays);
                hangar_group.GetBlocksOfType(hangar_speakers);

                if (has_roof) Roof1 = new RoofControl(hangar_hinges);
                if (has_door) Gate1 = new GateControl(hangar_doors);

                //Пред. настройка дисплеев
                foreach (IMyTextPanel display in hangar_displays)
                {
                    display.FontSize = 4.0f;
                    display.Alignment = TextAlignment.CENTER;
                    display.TextPadding = 15;
                }

                foreach (IMyLightingBlock light in hangar_lights)
                {
                    light.BlinkIntervalSeconds = 0;
                    light.BlinkLength = 0;
                    light.Intensity = 10;
                    light.Radius = 20;
                    light.Color = Color.White;
                }

                // Первая операция контроля
                this.Monitoring();
            }

            public void ToggleLights()
            // Вкл./Выкл. свет в ангаре
            {
                hangar_lights.ForEach(light => light.Enabled = light.Enabled ? false : true);
            }

            public void ShowStatus(string block_state, string block_name)
            // Отображение состояний на дисплеях и лампах.
            {
                switch (block_state)
                {
                    case "ОТКРЫВАЮТСЯ":
                        UpdateDisplays(hangar_displays, $"{_hangar_name}\n{block_name}\n ОТКРЫВАЮТСЯ", Color.Yellow, Color.Black);
                        UpdateLights(hangar_lights, Color.Yellow, true);
                        break;
                    case "ЗАКРЫВАЮТСЯ":
                        UpdateDisplays(hangar_displays, $"{_hangar_name}\n{block_name}\n ЗАКРЫВАЮТСЯ", Color.Yellow, Color.Black);
                        //UpdateDisplays(hangar_displays, string.Format("{0}\n{1}\n ЗАКРЫВАЮТСЯ", _hangar_name, block_name), Color.Yellow, Color.Black);
                        UpdateLights(hangar_lights, Color.Yellow, true);
                        break;
                    case "ОТКРЫТО":
                        UpdateDisplays(hangar_displays, $"{_hangar_name}\n{block_name}\n ОТКРЫТЫ", Color.Green, Color.White);
                        //UpdateDisplays(hangar_displays, string.Format("{0}\n{1}\n ОТКРЫТЫ", _hangar_name, block_name), Color.Green, Color.White);
                        UpdateLights(hangar_lights, Color.Green, false);
                        break;
                    case "ЗАКРЫТО":
                        UpdateDisplays(hangar_displays, $"{_hangar_name}\n{block_name}\n ЗАКРЫТЫ", Color.Black, Color.White);
                        //UpdateDisplays(hangar_displays, string.Format("{0}\n{1}\n ЗАКРЫТЫ", _hangar_name, block_name), Color.Black, Color.White);
                        UpdateLights(hangar_lights, Color.White, false);
                        break;
                    default:
                        UpdateDisplays(hangar_displays, $"{_hangar_name}\n{block_name}\n НЕ ОПРЕДЕЛЕНО", Color.Orange, Color.White);
                        //UpdateDisplays(hangar_displays, string.Format("{0}\n{1}\n НЕ ОПРЕДЕЛЕНО", _hangar_name, block_name), Color.Orange, Color.White);
                        UpdateLights(hangar_lights, Color.White, false);
                        break;
                };
            }

            private void ShowAlarm()
            // Отображение тревоги на дисплее и лампах
            // TODO: Подумать как передавать объект Alarm из списка тревог Alarm System и правильно его здесь обрабатывать
            {
                string alarm_text = alarm;
                string sound;
                UpdateDisplays(hangar_displays, $"!!!ВНИМАНИЕ!!!\n{alarm_text}", Color.Red, Color.White);
                UpdateLights(hangar_lights, Color.Red, true);

                switch (alarm)
                {
                    case "БОЕГОЛОВКА":
                        sound = "Weapon31";
                        break;
                    case "ВРАГИ В РАДИУСЕ\nПОРАЖЕНИЯ":
                        sound = "SoundBlockEnemyDetected";
                        break;
                    default:
                        sound = "SoundBlockAlert2";
                        break;
                }

                // Первый запуск тревоги
                if (_mem_alarm == "НЕТ ТРЕВОГИ")
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
                else if (_alarm_timer % 1 == 0 && _mem_alarm != "НЕТ ТРЕВОГИ")
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

            private void UpdateLights(List<IMyLightingBlock> lights, Color color, bool blink, float blink_interval = 1, float blink_length = 50F)
            // Обновление цвета ламп и режима моргания
            {
                foreach (IMyLightingBlock light in lights)
                {
                    if (light.Color != color) light.Color = color;
                    if (blink == true)
                    {
                        if (light.BlinkIntervalSeconds != blink_interval) light.BlinkIntervalSeconds = blink_interval;
                        if (light.BlinkLength != blink_length) light.BlinkLength = blink_length;
                    }
                    else
                    {
                        if (light.BlinkIntervalSeconds != 0) light.BlinkIntervalSeconds = 0;
                        if (light.BlinkLength != 0) light.BlinkLength = 0;
                    }
                }
            }

            private static void UpdateDisplays(List<IMyTextPanel> displays, string text, Color background_color, Color font_color)
            // Обновление содержимого дисплеев
            {
                foreach (IMyTextPanel display in displays)
                {
                    display.WriteText(text, false);
                    if (display.BackgroundColor != background_color) display.BackgroundColor = background_color;
                    if (display.FontColor != font_color) display.FontColor = font_color;
                }
            }

            // TODO: Придумать, как использовать один метод вместо 2-х ShowRoofState почти одинаковых прегрузок
            private static void ShowRoofState(IMyTextSurface display, List<IMyMotorAdvancedStator> hinges, string roof_state)
            {
                display.WriteText(String.Format("{0}\n", roof_state), false);
                foreach (IMyMotorAdvancedStator hinge in hinges)
                {
                    display.WriteText(String.Format("{0}: {1}\n", hinge.CustomName, Math.Round(RadToDeg(hinge.Angle), 0).ToString()), true);
                }
            }

            private static void ShowRoofState(IMyTextPanel display, List<IMyMotorAdvancedStator> hinges, string roof_state)
            {
                display.WriteText(String.Format("{0}\n", roof_state), false);
                foreach (IMyMotorAdvancedStator hinge in hinges)
                {
                    display.WriteText(String.Format("{0}: {1}\n", hinge.CustomName, Math.Round(RadToDeg(hinge.Angle), 0).ToString()), true);
                }
            }

            // TODO: Придумать, как использовать один метод вместо 2-х ShowDoorState почти одинаковых прегрузок
            private static void ShowDoorState(IMyTextPanel display, List<IMyAirtightHangarDoor> doors, string door_state)
            {
                display.WriteText(String.Format("{0}\n", door_state), false);
                foreach (IMyAirtightHangarDoor door in doors)
                {
                    display.WriteText($"{door.CustomName}: {door.Status}\n", true);
                }
            }

            private static void ShowDoorState(IMyTextSurface display, List<IMyAirtightHangarDoor> doors, string door_state)
            {
                display.WriteText(String.Format("{0}\n", door_state), false);
                foreach (IMyAirtightHangarDoor door in doors)
                {
                    display.WriteText($"{door.CustomName}: {door.Status}\n", true);
                }
            }

            // TODO: Не тестирован, нужно имплементировать куда-то для теста
            public void ShowOnDisplay(IMyTextPanel display)
            {
                if (_has_roof) ShowRoofState(display, hangar_hinges, Roof1.roof_state);
                else if (_has_door) ShowDoorState(display, hangar_doors, Gate1.gate_state);
            }

            // TODO: Не тестирован, нужно имплементировать куда-то для теста
            public void ShowOnDisplay(IMyTextSurface display)
            {
                if (_has_roof) ShowRoofState(display, hangar_hinges, Roof1.roof_state);
                else if (_has_door) ShowDoorState(display, hangar_doors, Gate1.gate_state);
            }


            public void Monitoring()
            // Циклическая рутина контроля состояний и отображения статусов
            // TODO: Подумать как привести в человеческий вид
            {
                if (Roof1 != null) Roof1.CheckRoof();
                if (Gate1 != null) Gate1.CheckGate();
                //ShowOnDisplay(_plc_screen1);
                if (alarm != "НЕТ ТРЕВОГИ")
                {
                    ShowAlarm();
                }
                else
                {
                    if (Roof1 != null) ShowStatus(Roof1.roof_state, "СТВОРКИ");
                    else if (Gate1 != null) ShowStatus(Gate1.gate_state, "ВОРОТА");
                }

                if (alarm == "НЕТ ТРЕВОГИ" && _mem_alarm != "НЕТ ТРЕВОГИ") DisableAlarm();
                _mem_alarm = alarm;
            }
        }


        Dictionary<string, ISector> sectors = new Dictionary<string, ISector>();
        HangarControl Hangar1;
        HangarControl Hangar2;
        HangarControl Hangar3;
        HangarControl Production;
        AlarmSystem alarm_system;

        public Program()
        {
            Hangar1 = new HangarControl(this, "Ангар 1", has_door: true);
            Hangar2 = new HangarControl(this, "Ангар 2", has_door: true, has_roof: true);
            Hangar3 = new HangarControl(this, "Ангар 3", has_door: true);
            Production = new HangarControl(this, "Производство");
            sectors.Add("hangar1", Hangar1);
            sectors.Add("hangar2", Hangar2);
            sectors.Add("hangar3", Hangar3);
            sectors.Add("production", Production);

            alarm_system = new AlarmSystem(this);
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
        }

        public void Save()
        {
        }

        public void Main(string argument, UpdateType updateSource)
        {
            switch (updateSource)
            {
                case UpdateType.Update100:
                    //Выполняется каждые 1.5 сек
                    // TODO: Разобраться почему не работает запись формата "sectors.Values.ForEach(sector => sector.Monitoring());", а работает стандартный foreach цикл
                    // TODO: Передавать в класс сектора не только текст ошибки, а весь объект
                    if (alarm_system.detect_alarms())
                    {
                        foreach (ISector sector in sectors.Values)
                        {
                            sector.alarm = alarm_system.current_alarms[0].alarm_text;
                        }
                    }
                    else
                    {
                        foreach (ISector sector in sectors.Values)
                        {
                            sector.alarm = "НЕТ ТРЕВОГИ";
                        }
                    }
                    foreach (ISector sector in sectors.Values)
                    {
                        sector.Monitoring();
                    }
                    break;
                case UpdateType.Terminal:
                    // Выполняется при "Выполнить" через терминал
                    break;
                case UpdateType.Script:
                    // Выполняется по запросу от другого программируемого блока
                    // TODO: Отменить! Нужен парсер для разбора приходящей команды на объект и метод.  Invoke запрещен к использованию.

                    switch (argument)
                    {
                        case "hangar1 toggle_door":
                            Hangar1.Gate1.ToggleGate();
                            break;
                        case "hangar1 toggle_light":
                            Hangar1.ToggleLights();
                            break;
                        case "hangar1 toggle_roof":
                            Hangar1.Roof1.ToggleRoof();
                            break;
                        case "hangar2 toggle_door":
                            Hangar2.Gate1.ToggleGate();
                            break;
                        case "hangar2 toggle_light":
                            Hangar2.ToggleLights();
                            break;
                        case "hangar2 toggle_roof":
                            Hangar2.Roof1.ToggleRoof();
                            break;
                        case "hangar3 toggle_door":
                            Hangar3.Gate1.ToggleGate();
                            break;
                        case "hangar3 toggle_light":
                            Hangar3.ToggleLights();
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
