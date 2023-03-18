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
using static System.Reflection.Metadata.BlobBuilder;
using System.Runtime.CompilerServices;

namespace Script6
{
    public sealed class Program : MyGridProgram
    {
        //------------BEGIN--------------
        IMyBroadcastListener _myBroadcastListener;
        string _broadCastTag;
        IMyBlockGroup hangar2_group;
        List<IMyInteriorLight> hangar2_lights = new List<IMyInteriorLight>();
        List<IMyMotorAdvancedStator> hangar2_hinges = new List<IMyMotorAdvancedStator>();
        List<IMyAirtightHangarDoor> hangar2_doors = new List<IMyAirtightHangarDoor>();
        IMyTextSurface plc_screen0;

        IMyProgrammableBlock main_plc;

        public Program()
        {
            plc_screen0 = Me.GetSurface(0);
            main_plc = GridTerminalSystem.GetBlockWithName("Главный ПЛК") as IMyProgrammableBlock;
            if (main_plc == null)
            {
                plc_screen0.WriteText("Не удалось найти программный блок\n с именем 'Главный ПЛК'\n", false);
                return;
            }


            hangar2_group = GridTerminalSystem.GetBlockGroupWithName("Ангар 2");
            if (hangar2_group == null)
            {
                plc_screen0.WriteText("Не удалось найти группу\nc именем 'Ангар 2'\n", false);
                return;
            }
            hangar2_group.GetBlocksOfType(hangar2_lights);
            hangar2_group.GetBlocksOfType(hangar2_hinges);
            hangar2_group.GetBlocksOfType(hangar2_doors);
            
            

            _broadCastTag = "Домик на озере";
            _myBroadcastListener = IGC.RegisterBroadcastListener(_broadCastTag);
            _myBroadcastListener.SetMessageCallback(_broadCastTag);
        }


        public void Save()

        {
        }

        public void Main(string argument, UpdateType updateSource)
        {

            while (_myBroadcastListener.HasPendingMessage)
            {
                var myIGCMessage = _myBroadcastListener.AcceptMessage();
                if (myIGCMessage.Tag == _broadCastTag)
                {
                    if (myIGCMessage.Data is string)
                    {
                        Echo(myIGCMessage.Data.ToString());
                        plc_screen0.WriteText(String.Format("{}\n", myIGCMessage.Data.ToString()), false);
                        if (!main_plc.IsRunning && main_plc.Enabled)
                        {
                            main_plc.TryRun(myIGCMessage.Data.ToString());
                        }
                    }
                    else if (myIGCMessage.Data is Vector3D)
                    {
                        Vector3D target_position = myIGCMessage.As<Vector3D>();
                        Vector3D my_position = Me.CubeGrid.GetPosition();
                        plc_screen0.WriteText("Получена позиция: \n", false);
                        plc_screen0.WriteText(String.Format("X={0}\nY={1}\nZ={2}\n", target_position.X, target_position.Y, target_position.Z), true);
                        double distance = Math.Sqrt(Math.Pow(my_position.X - target_position.X, 2) + Math.Pow(my_position.Y - target_position.Y, 2) + Math.Pow(my_position.Z - target_position.Z, 2));
                        plc_screen0.WriteText(String.Format("Расстояние до цели: {0}", distance), true);
                    }
                }
            }
        }

        private static void toggle_door(IMyAirtightHangarDoor door)
        {
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
                    door.OpenDoor(); ;
                    break;
            }


        }
        //------------END--------------
    }
}


