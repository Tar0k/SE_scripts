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

namespace Script7
{
    public sealed class Program : MyGridProgram
    {
        //------------BEGIN--------------
        string _broadCastTag = "Домик на озере";

        public Program()
        {
        }

        public void Save()

        {
        }


        public void Main(string argument, UpdateType updateSource)
        {
            if (argument != "position")
            IGC.SendBroadcastMessage(_broadCastTag, argument);
            else
            {
                IGC.SendBroadcastMessage(_broadCastTag, Me.CubeGrid.GetPosition());
            }
        }
        //------------END--------------
    }
}