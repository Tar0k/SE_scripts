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

namespace Script3
{
    public sealed class Program : MyGridProgram
    {
        //------------BEGIN--------------

        // Name of the group of the station's batteries
        public string myBatteryGroupName = "StationBatteriesGroup1";

        // Name of the group of the station's hydrogen tanks
        public string myHydrogenTankGroupName = "StationHydrogenTankGroup1";

        // Name of the group of the station's hydrogen engines
        public string myHydrogenEngineGroupName = "StationHydrogenEngineGroup1";

        // Percent from 0.0 to 1.0 of the minimum average battery level amoungst all batteries.
        public float minBatteryLevel = 0.25f;

        // Percent from 0.0 to 1.0 of the max average battery level amoungst all batteries.
        public float maxBatteryLevel = 0.95f;

        // Percent from 0.0 to 1.0 of the minimum average hydrogen tank filled level.
        public double minHydrogenTankLevel = 0.25;

        // ===========================
        // DO NOT EDIT BELOW THIS LINE
        // ===========================

        MyCommandLine _commandLine = new MyCommandLine();
        List<IMyBatteryBlock> StationBatteryBlocks = new List<IMyBatteryBlock>();
        List<IMyGasTank> StationHydrogenTanks = new List<IMyGasTank>();
        List<IMyPowerProducer> StationHydrogenEngines = new List<IMyPowerProducer>();
        IMyBlockGroup BatteryGroup;
        IMyBlockGroup HydrogenTankGroup;
        IMyBlockGroup HydrogenEngineGroup;
        string State = "Battery.Empty";

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;

            GetComponents();
        }

        public void Save()
        {
            if (Me.CustomData == "Battery.Empty" || Me.CustomData == "Battery.Full")
            {
                Me.CustomData = State;
            }
        }

        public void GetComponents()
        {
            BatteryGroup = GridTerminalSystem.GetBlockGroupWithName(myBatteryGroupName);
            BatteryGroup.GetBlocksOfType(StationBatteryBlocks, x => x.BlockDefinition.SubtypeName.Contains("Battery"));
            HydrogenTankGroup = GridTerminalSystem.GetBlockGroupWithName(myHydrogenTankGroupName);
            HydrogenTankGroup.GetBlocksOfType(StationHydrogenTanks, x => x.BlockDefinition.SubtypeName.Contains("Hydro"));
            HydrogenEngineGroup = GridTerminalSystem.GetBlockGroupWithName(myHydrogenEngineGroupName);
            HydrogenEngineGroup.GetBlocksOfType(StationHydrogenEngines, x => x.BlockDefinition.SubtypeName.EndsWith("HydrogenEngine"));
        }

        public void OutputInfo()
        {
            List<float> batteryLevels = (from b in StationBatteryBlocks
                                         let a = b.CurrentStoredPower
                                         select a).ToList();
            float percent = ComputePercent(batteryLevels, (maxBatteryLevel * 3.0f));
            List<double> hydrogenTankLevels = (from g in StationHydrogenTanks
                                               let a = g.FilledRatio
                                               select a).ToList();

            double average = ComputeLevels(hydrogenTankLevels);

            List<bool> hydrogenEnginesStates = (from e in StationHydrogenEngines
                                                let a = e.Enabled
                                                select a).ToList();

            string hydrogenEngineHeader = "Hydrogen Engine ";

            Echo("Batteries: " + Math.Round(percent * 100, 2) + "%");
            Echo("Charge: " + ComputeRange(batteryLevels).ToString() + "%");
            Echo("Hydrogen Tanks: " + Math.Round(average * 100, 2) + "%");
            foreach (bool b in hydrogenEnginesStates)
            {
                Echo(hydrogenEngineHeader + (hydrogenEnginesStates.IndexOf(b) + 1).ToString() + ": " + (b.Equals(true) ? "true" : "false"));
            }
            Echo("State: " + State);
        }

        public float ComputeLevels(List<float> levels) => levels.Average();
        public double ComputeLevels(List<double> levels) => levels.Average();

        public float ComputePercent(List<float> levels, float max)
        {
            float average = levels.Average();
            return (average / max);
        }

        public float ComputeRange(List<float> levels)
        {
            float average = levels.Average();
            float range = maxBatteryLevel - minBatteryLevel;
            float correctedStartValue = average / 3.0f - minBatteryLevel;
            return correctedStartValue * 100 / range;
        }

        public bool CheckHydrogenLevels()
        {
            List<double> levels = (from g in StationHydrogenTanks
                                   let a = g.FilledRatio
                                   select a).ToList();

            double average = ComputeLevels(levels);

            if (average <= minHydrogenTankLevel)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        public bool CheckBatteryLevels()
        {
            List<float> levels = (from b in StationBatteryBlocks
                                  let a = b.CurrentStoredPower
                                  select a).ToList();

            float average = ComputeLevels(levels);
            float percent = ComputePercent(levels, maxBatteryLevel * 3.0f);

            if (average <= (minBatteryLevel * 3.0f))
            {
                State = "Battery.Empty";
                return true;
            }
            else if (average >= (maxBatteryLevel * 3.0f))
            {
                State = "Battery.Full";
                return false;
            }
            else
            {
                return false;
            }
        }

        public void LoadData()
        {
            string[] storedData = new string[] { };
            if (Me.CustomData == "Battery.Full" || Me.CustomData == "Battery.Empty")
            {
                storedData = Me.CustomData.Split(';');
            }
            if (storedData.Length >= 1 && storedData[0] != "")
            {
                State = storedData[0];
            }
        }

        public void StartStopHydrogenEngine()
        {
            CheckBatteryLevels();
            bool hydrogenState = CheckHydrogenLevels();

            if (State == "Battery.Empty" && hydrogenState)
            {
                foreach (IMyPowerProducer p in StationHydrogenEngines)
                {
                    p.Enabled = true;
                }
            }
            else if (State == "Battery.Full" || !hydrogenState)
            {
                foreach (IMyPowerProducer p in StationHydrogenEngines)
                {
                    p.Enabled = false;
                }
            }
            OutputInfo();
        }

        public void Main(string argument, UpdateType updateSource)
        {
            Echo(updateSource.Equals(UpdateFrequency.Update10) ? "Stopped" : "Running");

            if (_commandLine.TryParse(argument).Equals("stop"))
            {
                Runtime.UpdateFrequency = UpdateFrequency.None;
            }
            if (_commandLine.TryParse(argument).Equals("fix"))
            {
                Me.CustomData = "Battery.Empty";
            }
            LoadData();
            if ((updateSource & UpdateType.Update10) != 0 && !_commandLine.TryParse(argument).Equals("stop"))
            {
                StartStopHydrogenEngine();
            }
        }



        //------------END--------------
    }
}