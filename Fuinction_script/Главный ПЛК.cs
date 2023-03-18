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

namespace Script5
{
    public sealed class Program : MyGridProgram
    {
        //------------START--------------
        public class HangarDoorControl
        {
            Program _program;
            List<IMyInteriorLight> hangar_lights = new List<IMyInteriorLight>();
            List<IMyMotorAdvancedStator> hangar_hinges = new List<IMyMotorAdvancedStator>();
            List<IMyTextPanel> hangar_displays = new List<IMyTextPanel>();
            List<IMyAirtightHangarDoor> hangar_doors = new List<IMyAirtightHangarDoor>();
            IMyTextSurface _plc_screen1;
            string _hangar_name;
            float _open_state;
            float _close_state;
            string roof_state = "Неопределено";

            public HangarDoorControl(Program program, string hangar_name, float open_state = 0f, float close_state = -90f)
            {
                _program = program;
                _hangar_name = hangar_name;
                _open_state = open_state;
                _close_state = close_state;
                _plc_screen1 = _program.Me.GetSurface(0);
                IMyBlockGroup hangar_group;

                hangar_group = _program.GridTerminalSystem.GetBlockGroupWithName(_hangar_name);
                hangar_group.GetBlocksOfType(hangar_lights);
                hangar_group.GetBlocksOfType(hangar_hinges);
                hangar_group.GetBlocksOfType(hangar_doors);

                foreach (IMyTextPanel display in hangar_displays)
                {
                    display.FontSize = 8.0f;
                    display.Alignment = TextAlignment.CENTER;
                }
                this.Monitoring();
            }

            public void ToggleLights()
            {
                foreach (IMyInteriorLight light in hangar_lights)
                    light.Enabled = light.Enabled ? false : true;
            }

            public void OpenRoof()
            {
                foreach (IMyMotorAdvancedStator hinge in hangar_hinges)
                    hinge.TargetVelocityRPM = RadToDeg(hinge.Angle) > -90f && hinge.Enabled ? 1f : 0f;
            }

            public void CloseRoof()
            {
                foreach (IMyMotorAdvancedStator hinge in hangar_hinges)
                    hinge.TargetVelocityRPM = RadToDeg(hinge.Angle) > 0f && hinge.Enabled ? -1f : 0f;
            }

            public void ToggleDoor()
            {
                foreach (IMyAirtightHangarDoor door in hangar_doors)
                    switch (door.Status)
                    {
                        case DoorStatus.Closed:
                            door.OpenDoor();
                            break;
                        case DoorStatus.Open:
                            door.CloseDoor();
                            break;
                        case DoorStatus.Opening:
                            door.CloseDoor();
                            break;
                        case DoorStatus.Closing:
                            door.OpenDoor();
                            break;
                    }
            }
            public void CheckRoof()
            {

                roof_state = "NA";
                roof_state = hangar_hinges.FindAll(hinge => hinge.TargetVelocityRPM < 0f && hinge.Enabled).Count() == hangar_hinges.Count() ? "ЗАКРЫВАЮТСЯ" : roof_state;
                roof_state = hangar_hinges.FindAll(hinge => hinge.TargetVelocityRPM > 0f && hinge.Enabled).Count() == hangar_hinges.Count() ? "ОТКРЫВАЮТСЯ" : roof_state;
                roof_state = hangar_hinges.FindAll(hinge => RadToDeg(hinge.Angle) <= _open_state + 0.2F && RadToDeg(hinge.Angle) >= _open_state - 0.2F).Count() == hangar_hinges.Count() ? "ОТКРЫТО" : roof_state;
                roof_state = hangar_hinges.FindAll(hinge => RadToDeg(hinge.Angle) <= _close_state + 0.2F && RadToDeg(hinge.Angle) >= _close_state - 0.2F).Count() == hangar_hinges.Count() ? "ЗАКРЫТО" : roof_state;

            }

            public void ShowStatus()
            {
                _plc_screen1.WriteText(String.Format("{0}\n", roof_state), false);
                foreach (IMyMotorAdvancedStator hinge in hangar_hinges)
                {
                    _plc_screen1.WriteText(String.Format("{0}: {1}\n", hinge.CustomName, RadToDeg(hinge.Angle).ToString()), true);
                }
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
                            UpdateLed(light, Color.Yellow, true);
                            break;
                        case "ЗАКРЫВАЮТСЯ":
                            UpdateLed(light, Color.Yellow, true);
                            break;
                        case "ОТКРЫТО":
                            UpdateLed(light, Color.Green, false);
                            break;
                        case "ЗАКРЫТО":
                            UpdateLed(light, Color.White, false);
                            break;
                        default:
                            UpdateLed(light, Color.White, false);
                            break;
                    };
                }
            }

            private static float RadToDeg(float rad_value)
            {
                float deg_value = rad_value * 180f / 3.14159265359f;
                return deg_value;
            }

            private static void UpdateLed(IMyInteriorLight light, Color color, bool blink, float blink_interval = 1, float blink_length = 50F)
            {
                if (light.Color != color)
                    light.Color = color;
                if (blink == true)
                {
                    if (light.BlinkIntervalSeconds != blink_interval)
                        light.BlinkIntervalSeconds = blink_interval;
                    if (light.BlinkLength != blink_length)
                        light.BlinkLength = blink_length;
                }
                else
                { 
                    if (light.BlinkIntervalSeconds != 0)
                        light.BlinkIntervalSeconds = 0;
                    if (light.BlinkLength != 0)
                        light.BlinkLength = 0;
                }
            }

            public void Monitoring()
            {
                CheckRoof();
                ShowStatus();
            }

        }


        HangarDoorControl Hangar_door2;


        public Program()
        {
            Hangar_door2 = new HangarDoorControl(this, "Ангар 2");
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
                    }
                    break;
            }
            
        }

        //------------END--------------
    }
}