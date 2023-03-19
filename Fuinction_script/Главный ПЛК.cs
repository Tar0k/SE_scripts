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
            List<IMyWarhead> warheads = new List<IMyWarhead>();
            bool warhead_detected = false;
            public AlarmSystem(Program program)
            {
                _program = program;
            }

            public bool detect_warheads()
             {
                _program.GridTerminalSystem.GetBlocksOfType(warheads, warhead => warhead.IsSameConstructAs(_program.Me));
                warhead_detected = warheads.Count > 0 ? true : false;
                return warhead_detected;
            }
        }

        public class HangarDoorControl
        {
            /* Класс управления блоками в ангаре
             * Выполняет управление и мониторинг состояния блоков
             */
            Program _program;
            List<IMyInteriorLight> hangar_lights = new List<IMyInteriorLight>();
            List<IMyMotorAdvancedStator> hangar_hinges = new List<IMyMotorAdvancedStator>();
            List<IMyTextPanel> hangar_displays = new List<IMyTextPanel>();
            List<IMyAirtightHangarDoor> hangar_doors = new List<IMyAirtightHangarDoor>();
            IMyTextSurface _plc_screen1;
            string _hangar_name;
            float _open_state;
            float _close_state;
            public bool alarm_mode = false;
            string roof_state = "Неопределено";

            public HangarDoorControl(Program program, string hangar_name, float open_state = 0f, float close_state = -90f)
            {
                _program = program; //Ссылка на основную программу для возможности использовать GridTerminalSystem
                _hangar_name = hangar_name; // Имя группы устройств в ангаре, например "Ангар 1".
                _open_state = open_state; // Положение шарниров для состояния "ОТКРЫТО" в градусах
                _close_state = close_state; // Положение шарниров для состояния "ЗАКРЫТО" в градусах
                _plc_screen1 = _program.Me.GetSurface(0); // Экран на программируемом блоке
                IMyBlockGroup hangar_group;

                //Распределение блоков по типам в соответствующие списки
                hangar_group = _program.GridTerminalSystem.GetBlockGroupWithName(_hangar_name);
                hangar_group.GetBlocksOfType(hangar_lights);
                hangar_group.GetBlocksOfType(hangar_hinges);
                hangar_group.GetBlocksOfType(hangar_doors);

                //Пред. настройка дисплеев
                foreach (IMyTextPanel display in hangar_displays)
                {
                    display.FontSize = 8.0f;
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
                    hinge.TargetVelocityRPM = RadToDeg(hinge.Angle) > -90f && hinge.Enabled ? 1f : 0f;
            }

            public void CloseRoof()
                // Закрыть крышу
            {
                foreach (IMyMotorAdvancedStator hinge in hangar_hinges)
                    hinge.TargetVelocityRPM = RadToDeg(hinge.Angle) > 0f && hinge.Enabled ? -1f : 0f;
            }

            public void ToggleRoof()
                // Откр./Закр. крышу
            {
                switch(roof_state)
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
                roof_state = hangar_hinges.FindAll(hinge => RadToDeg(hinge.Angle) <= _open_state + 0.2F && RadToDeg(hinge.Angle) >= _open_state - 0.2F).Count() == hangar_hinges.Count() ? "ОТКРЫТО" : roof_state;
                roof_state = hangar_hinges.FindAll(hinge => RadToDeg(hinge.Angle) <= _close_state + 0.2F && RadToDeg(hinge.Angle) >= _close_state - 0.2F).Count() == hangar_hinges.Count() ? "ЗАКРЫТО" : roof_state;

            }

            public void ShowStatus()
                // Отобразить состояние н
            {
                _plc_screen1.WriteText(String.Format("{0}\n", roof_state), false);
                foreach (IMyMotorAdvancedStator hinge in hangar_hinges)
                {
                    _plc_screen1.WriteText(String.Format("{0}: {1}\n", hinge.CustomName, RadToDeg(hinge.Angle).ToString()), true);
                }

                if (!alarm_mode)
                {
                    foreach (IMyTextPanel display in hangar_displays)
                    {
                        switch (roof_state)
                        {
                            case "ОТКРЫВАЮТСЯ":
                                display.WriteText("СТВОРКИ ОТКРЫВАЮТСЯ", false);
                                display.BackgroundColor = Color.Yellow;
                                break;
                            case "ЗАКРЫВАЮТСЯ":
                                display.WriteText("СТВОРКИ ЗАКРЫВАЮТСЯ", false);
                                display.BackgroundColor = Color.Yellow;
                                break;
                            case "ОТКРЫТО":
                                display.WriteText("СТВОРКИ ОТКРЫТЫ", false);
                                display.BackgroundColor = Color.Green;
                                break;
                            case "ЗАКРЫТО":
                                display.WriteText("СТВОРКИ ЗАКРЫТЫ", false);
                                display.BackgroundColor = Color.Red;
                                break;
                            default:
                                display.WriteText("СТВОРКИ НЕОПРЕДЕЛЕНО", false);
                                display.BackgroundColor = Color.Orange;
                                break;
                        };
                    }


                    foreach (IMyInteriorLight light in hangar_lights)
                    {
                        switch (roof_state)
                        {
                            case "ОТКРЫВАЮТСЯ":
                            case "ЗАКРЫВАЮТСЯ":
                                UpdateLed(light, Color.Yellow, true);
                                break;
                            case "ОТКРЫТО":
                                UpdateLed(light, Color.Green, false);
                                break;
                            case "ЗАКРЫТО":
                            default:
                                UpdateLed(light, Color.White, false);
                                break;
                        };
                    }

                }
                else
                {
                    foreach (IMyTextPanel display in hangar_displays)
                    {
                        display.WriteText("ВНИМАНИЕ!!!\nОБНАРУЖЕНА БОЕГОЛОВКА!", false);
                        display.BackgroundColor = Color.Red;
                    }
                    foreach (IMyInteriorLight light in hangar_lights)
                    {
                        UpdateLed(light, Color.Red, true);
                    }
                }
            }

            private static float RadToDeg(float rad_value)
            {
                return rad_value * 180f / 3.14159265359f;
            }

            private static void UpdateLed(IMyInteriorLight light, Color color, bool blink, float blink_interval = 1, float blink_length = 50F)
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

            public void Monitoring()
            {
                CheckRoof();
                ShowStatus();
            }

        }


        HangarDoorControl Hangar_door2;
        AlarmSystem alarm_system;


        public Program()
        {
            Hangar_door2 = new HangarDoorControl(this, "Ангар 2");
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
                    Hangar_door2.alarm_mode = alarm_system.detect_warheads();
                    Hangar_door2.Monitoring();
                    break;
                case UpdateType.Terminal:
                    Hangar_door2.ToggleDoor();
                    break;
                case UpdateType.Script:
                    switch (argument)
                    {
                        case "hangar2 toggle_door":
                            Hangar_door2.ToggleDoor();
                            break;
                        case "hangar2 toggle_light":
                            Hangar_door2.ToggleLights();
                            break;
                        case "hangar2 toggle_roof":
                            Hangar_door2.ToggleRoof();
                            break;
                    }
                    break;
            }
            
        }

        //------------END--------------
    }
}