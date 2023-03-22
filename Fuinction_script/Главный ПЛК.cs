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

namespace Script5
{
    public sealed class Program : MyGridProgram
    {
        //------------START--------------

        public class AlarmSystem
        {
            Program _program;
            public List<string> current_alarms = new List<string>();

            List<IMyWarhead> warheads = new List<IMyWarhead>();
            bool warhead_detected = false;
            public AlarmSystem(Program program)
            {
                _program = program;
            }

            private bool detect_warheads()
            {
                _program.GridTerminalSystem.GetBlocksOfType(warheads, warhead => warhead.IsSameConstructAs(_program.Me));
                warhead_detected = warheads.Count > 0 ? true : false;
                return warhead_detected;
            }

            public void detect_alarms()
            {
                current_alarms.Clear();
                if (detect_warheads()) current_alarms.Add("Боеголовка");
            }
        }

        public class HangarControl
        {
            /* Класс управления блоками в ангаре
             * Выполняет управление и мониторинг состояния блоков
             */
            Program _program;
            List<IMyInteriorLight> hangar_lights = new List<IMyInteriorLight>();
            List<IMyMotorAdvancedStator> hangar_hinges = new List<IMyMotorAdvancedStator>();
            List<IMyTextPanel> hangar_displays = new List<IMyTextPanel>();
            List<IMyAirtightHangarDoor> hangar_doors = new List<IMyAirtightHangarDoor>();
            List<IMySoundBlock> hangar_speakers = new List<IMySoundBlock>();
            IMyTextSurface _plc_screen1;
            IMyTextPanel debug_display;


            string _hangar_name;
            float _open_state;
            float _close_state;
            public bool alarm_mode = false;
            bool _mem_alarm_mode = false;
            string roof_state = "Неопределено";

            public HangarControl(Program program, string hangar_name, float open_state = 0f, float close_state = -90f)
            {
                _program = program; //Ссылка на основную программу для возможности использовать GridTerminalSystem
                _hangar_name = hangar_name; // Имя группы устройств в ангаре, например "Ангар 1".
                _open_state = open_state; // Положение шарниров для состояния "ОТКРЫТО" в градусах
                _close_state = close_state; // Положение шарниров для состояния "ЗАКРЫТО" в градусах
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



                //Пред. настройка дисплеев
                foreach (IMyTextPanel display in hangar_displays)
                {
                    display.FontSize = 4.0f;
                    display.Alignment = TextAlignment.CENTER;
                }
                // Первая операция контроля
                this.Monitoring();
            }

            public void ToggleLights()
            // Вкл./Выкл. свет в ангаре
            {
                foreach (IMyInteriorLight light in hangar_lights)
                    light.Enabled = light.Enabled ? false : true;
            }

            public void OpenRoof()
            // Открыть крышу
            {
                foreach (IMyMotorAdvancedStator hinge in hangar_hinges)
                    hinge.TargetVelocityRPM = Math.Round(RadToDeg(hinge.Angle), 0) != _open_state ? 1 : 0;
            }

            public void CloseRoof()
            // Закрыть крышу
            {
                foreach (IMyMotorAdvancedStator hinge in hangar_hinges)
                    hinge.TargetVelocityRPM = Math.Round(RadToDeg(hinge.Angle), 0) != _close_state ? -1 : 0;
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
                        _program.Echo("2");
                        OpenRoof();
                        break;
                }
            }

            public void ToggleDoor()
            // Откр./Закр. дверь
            {
                foreach (IMyAirtightHangarDoor door in hangar_doors)
                    switch (door.Status)
                    {
                        case DoorStatus.Closed:
                        case DoorStatus.Closing:
                            door.OpenDoor();
                            break;
                        case DoorStatus.Open:
                        case DoorStatus.Opening:
                            door.CloseDoor();
                            break;
                    }
            }
            public void CheckRoof()
            // Проверить состояние крыши на откр. или закр. и т.д.
            {

                roof_state = "NA";
                roof_state = hangar_hinges.FindAll(hinge => hinge.TargetVelocityRPM < 0f && hinge.Enabled).Count() == hangar_hinges.Count() ? "ЗАКРЫВАЮТСЯ" : roof_state;
                roof_state = hangar_hinges.FindAll(hinge => hinge.TargetVelocityRPM > 0f && hinge.Enabled).Count() == hangar_hinges.Count() ? "ОТКРЫВАЮТСЯ" : roof_state;
                roof_state = hangar_hinges.FindAll(hinge => Math.Round(RadToDeg(hinge.Angle)) == _open_state).Count() == hangar_hinges.Count() ? "ОТКРЫТО" : roof_state;
                roof_state = hangar_hinges.FindAll(hinge => Math.Round(RadToDeg(hinge.Angle)) == _close_state).Count() == hangar_hinges.Count() ? "ЗАКРЫТО" : roof_state;

            }

            public void ShowStatus()
            // Отображение состояний на дисплеях и лампах.
            {
                ShowRoofState(_plc_screen1, hangar_hinges, roof_state);

                if (!alarm_mode)
                {
                    switch (roof_state)
                    {
                        case "ОТКРЫВАЮТСЯ":
                            UpdateDisplays(hangar_displays, "СТВОРКИ\n ОТКРЫВАЮТСЯ", Color.Yellow, Color.Black);
                            UpdateLights(hangar_lights, Color.Yellow, true);
                            break;
                        case "ЗАКРЫВАЮТСЯ":
                            UpdateDisplays(hangar_displays, "СТВОРКИ\n ЗАКРЫВАЮТСЯ", Color.Yellow, Color.Black);
                            UpdateLights(hangar_lights, Color.Yellow, true);
                            break;
                        case "ОТКРЫТО":
                            UpdateDisplays(hangar_displays, "СТВОРКИ\n ОТКРЫТЫ", Color.Green, Color.White);
                            UpdateLights(hangar_lights, Color.Green, false);
                            break;
                        case "ЗАКРЫТО":
                            UpdateDisplays(hangar_displays, "СТВОРКИ\n ЗАКРЫТЫ", Color.Black, Color.White);
                            UpdateLights(hangar_lights, Color.White, false);
                            break;
                        default:
                            UpdateDisplays(hangar_displays, "СТВОРКИ\n НЕОПРЕДЕЛЕНО", Color.Orange, Color.White);
                            UpdateLights(hangar_lights, Color.White, false);
                            break;
                    };

                }
                else
                {
                    UpdateDisplays(hangar_displays, "!!!ВНИМАНИЕ!!!\nБОЕГОЛОВКА", Color.Red, Color.White);
                    UpdateLights(hangar_lights, Color.Red, true);

                    if (_mem_alarm_mode == false)
                    {
                        
						foreach (IMySoundBlock speaker in hangar_speakers)
						{
                            speaker.SelectedSound = "SoundBlockAlert2";
                            speaker.LoopPeriod = 5;
                            speaker.Play();
						}
						


                    }
                }
                _mem_alarm_mode = alarm_mode;
            }

            private static float RadToDeg(float rad_value)
            // Конвертация из радиан в градусы
            {
                return rad_value * 180f / 3.14159265359f;
            }

            private void UpdateLights(List<IMyInteriorLight> lights, Color color, bool blink, float blink_interval = 1, float blink_length = 50F)
            // Обновление цвета ламп и режима моргания
            {
                foreach (IMyInteriorLight light in lights)
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

            public void Monitoring()
            // Циклическая рутина контроля состояний и отображения статусов
            {
                CheckRoof();
                ShowStatus();
            }

        }

        HangarControl Hangar1;
        HangarControl Hangar2;
        HangarControl Hangar3;
        AlarmSystem alarm_system;

        public Program()
        {
            Hangar1 = new HangarControl(this, "Ангар 1");
            Hangar2 = new HangarControl(this, "Ангар 2");
            Hangar3 = new HangarControl(this, "Ангар 3");
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
                    alarm_system.detect_alarms();
                    Hangar2.alarm_mode = alarm_system.current_alarms.Count() > 0 ? true : false;
                    Hangar2.Monitoring();
                    break;
                case UpdateType.Terminal:
                    // Выполняется при "Выполнить" через терминал
                    break;
                case UpdateType.Script:
                    // Выполняется по запросу от другого программируемого блока
                    switch (argument)
                    {
                        case "hangar2 toggle_door":
                            Hangar2.ToggleDoor();
                            break;
                        case "hangar2 toggle_light":
                            Hangar2.ToggleLights();
                            break;
                        case "hangar2 toggle_roof":
                            Hangar2.ToggleRoof();
                            break;
                    }
                    break;
            }

        }

        //------------END--------------
    }
}
