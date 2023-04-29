using System;
using System.Text;
using System.Linq;
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



        readonly ShipInfo ShipData;
        readonly string _broadCastTag = "Домик на озере";
        readonly List<IMyCockpit> _main_cockpits = new List<IMyCockpit>();
        readonly IMyCockpit _main_cockpit;
        readonly List<IMyTextSurface> _main_cockpit_displays = new List<IMyTextSurface>();
        public Program()
        {
            GridTerminalSystem.GetBlocksOfType(_main_cockpits, cockpit => cockpit.IsSameConstructAs(Me) && cockpit.CustomData.StartsWith("main"));
            if (_main_cockpits != null && _main_cockpits.Count > 0)
            {
                _main_cockpit = _main_cockpits[0];
                for (int i = 0; i < _main_cockpit.SurfaceCount - 1; i++)
                {
                    _main_cockpit_displays.Add(_main_cockpit.GetSurface(i));
                }
            }
            ShipData = new ShipInfo(this, Me);
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
        }
        public void Main(string argument, UpdateType updateSource)
        {
            switch (updateSource)
            {
                case UpdateType.Update100:
                    foreach (var display in _main_cockpit_displays)
                    {
                        string text = "";
                        text += $"Заряд батарей {ShipData.BatteryLevel}%\n";
                        text += $"Уровень водорода {ShipData.HydrogenLevel}%\n";
                        text += $"Уровень кислорода {ShipData.OxygenLevel}%\n";
                        text += $"Трюм (общий) {ShipData.CargoHoldTotalFilledRatio}%\n";
                        text += $"Трюм (только контейнеры) {ShipData.CargoHoldContainersFilledRatio}%\n";
                        display.WriteText(text, false);
                    }
                    break;
                case UpdateType.Trigger:
                    if (argument != "position")
                        IGC.SendBroadcastMessage(_broadCastTag, argument);
                    else
                    {
                        IGC.SendBroadcastMessage(_broadCastTag, Me.CubeGrid.GetPosition());
                    }
                    break;
            }
        }
        //------------END--------------
    }
}