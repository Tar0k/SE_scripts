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

namespace SE_scripts.tests
{
    public sealed class Program : MyGridProgram
    {
        //------------BEGIN--------------

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
        }

        public void Save()
        {
        }

        public void Main(string argument, UpdateType updateSource)
        {
            //Управление ангарными воротами
            //----------------------------------------------
            IMyProgrammableBlock? prog_plc = GridTerminalSystem.GetBlockWithName("__База__ Главный ПЛК") as IMyProgrammableBlock;
            IMyTextSurface prog_plc_lcd1 = prog_plc.GetSurface(0);
            IMyTextPanel? hangar_entry_display = GridTerminalSystem.GetBlockWithName("__База__ Дисплей на входе в ангар") as IMyTextPanel;
            IMyTextPanel? hangar_exit_display = GridTerminalSystem.GetBlockWithName("__База__ Дисплей на выходе из ангара") as IMyTextPanel;
            IMySensorBlock? hangar_entry_sensor = GridTerminalSystem.GetBlockWithName("__База__ Военная дверь вх сенсор") as IMySensorBlock;
            IMySensorBlock? hangar_exit_sensor = GridTerminalSystem.GetBlockWithName("__База__ Военная дверь вых сенсор") as IMySensorBlock;
            List<IMyTerminalBlock> hangar_doors = new List<IMyTerminalBlock>();
            GridTerminalSystem.SearchBlocksOfName("_База_ Военная дверь ангара", hangar_doors);

            hangar_entry_display.FontSize = 8.0f;
            hangar_entry_display.Alignment = TextAlignment.CENTER;
            hangar_exit_display.FontSize = 8.0f;
            hangar_exit_display.Alignment = TextAlignment.CENTER;


            if (hangar_entry_sensor.IsActive || hangar_exit_sensor.IsActive)
            {
                OpenHangarDoor(hangar_doors);
            }
            else
            {
                CloseHangarDoor(hangar_doors);
            }

            string door_status = StatusHangarDoor(hangar_doors);
            prog_plc_lcd1.WriteText(door_status, false);
            ShowHangarDoorStatus(door_status, hangar_entry_display);
            ShowHangarDoorStatus(door_status, hangar_exit_display);

            //----------------------------------------------

            //Инфо-табло
            //----------------------------------------------
            string text = "";
            List<IMyPowerProducer> power_producers = new List<IMyPowerProducer>();
            GridTerminalSystem.GetBlocksOfType(power_producers);
            IMyTextPanel? info_display = GridTerminalSystem.GetBlockWithName("_База_ Инфо-табло") as IMyTextPanel;
            info_display.Alignment = TextAlignment.LEFT;
            info_display.FontSize = 0.5f;

            List<IMyBatteryBlock> batteries = new List<IMyBatteryBlock>();
            GridTerminalSystem.GetBlocksOfType(batteries);

            List<IMyGasTank> gas_tanks = new List<IMyGasTank>();
            GridTerminalSystem.GetBlocksOfType(gas_tanks);

            List<IMyPowerProducer> local_power_producers = new List<IMyPowerProducer>();
            List<IMyPowerProducer> connected_power_producers = new List<IMyPowerProducer>();


            for (int i = 0; i < power_producers.Count; i++)
            {
                if (power_producers[i].CubeGrid.CustomName.Equals("База"))
                {
                    local_power_producers.Add(power_producers[i]);
                }
                else
                {
                    connected_power_producers.Add(power_producers[i]);
                }
            }

            text += "---Отдача стационарных источники энергии---\n";
            text += PowerProducerInfo(local_power_producers);
            text += "---Отдача подключенных источников энергии---\n";
            text += PowerProducerInfo(connected_power_producers);
            text += "\n";
            text += "---Статус батареи---\n";
            text += BatteriesInfo(batteries);
            text += "\n";
            text += "---Статус баков---\n";
            text += GasTanksInfo(gas_tanks);
            info_display.WriteText(text, false);
            //----------------------------------------------
        }



        //---Рутиные процедуры---

        //Метод открытия всех ангарных дверей в списке
        static void OpenHangarDoor(List<IMyTerminalBlock> doors)
        {
            for (int i = 0; i < doors.Count; i++)
            {
                IMyAirtightHangarDoor door = doors[i] as IMyAirtightHangarDoor;
                door.OpenDoor();
            }
        }

        //Метод закрытия всех ангарных дверей в списке
        static void CloseHangarDoor(List<IMyTerminalBlock> doors)
        {
            for (int i = 0; i < doors.Count; i++)
            {
                IMyAirtightHangarDoor door = doors[i] as IMyAirtightHangarDoor;
                door.CloseDoor();
            }
        }

        //Метод формирования статуса состояния дверей
        static string StatusHangarDoor(List<IMyTerminalBlock> doors)
        {
            IMyAirtightHangarDoor door = doors[0] as IMyAirtightHangarDoor;
            if (door.Status == DoorStatus.Closed)
                return "Ворота закрыты";
            else if (door.Status == DoorStatus.Open)
                return "Ворота Открыты";
            else if (door.Status == DoorStatus.Opening)
                return "Ворота открываются";
            else if (door.Status == DoorStatus.Closing)
                return "Ворота закрываются";
            else
                return "Черт их знает";
        }

        //Метод формирования индикации состояния ворот
        static void ShowHangarDoorStatus(string status, IMyTextPanel display)
        {
            if (status == "Ворота Открыты")
            {
                display.WriteText("Ворота открыты", false);
                display.BackgroundColor = Color.Green;
            }
            else if (status == "Ворота закрыты")
            {
                display.WriteText("Ворота закрыты", false);
                display.BackgroundColor = Color.Red;
            }
            else if (status == "Ворота открываются")
            {
                display.WriteText("Ворота открываются", false);
                display.BackgroundColor = Color.Yellow;
            }
            else if (status == "Ворота закрываются")
            {
                display.WriteText("Ворота закрываются", false);
                display.BackgroundColor = Color.Yellow;
            }
        }


        //Метод формирования строки данных об источниках энергии
        static string PowerProducerInfo(List<IMyPowerProducer> power_producers)
        {
            string text = "";
            for (int i = 0; i < power_producers.Count; i++)
            {
                string name = power_producers[i].CustomName.Replace("__База__", "");
                string current_output = (power_producers[i].CurrentOutput * 1000).ToString("N0");
                string max_output = (power_producers[i].MaxOutput * 1000).ToString("N0");
                string product_in_percentages = (power_producers[i].CurrentOutput / power_producers[i].MaxOutput * 100).ToString("N0");
                text += string.Format("     {0}: {1}/{2} ({3}%) kW\n", name, current_output, max_output, product_in_percentages);
            }
            return text;
        }

        static string BatteriesInfo(List<IMyBatteryBlock> batteries)
        {
            string text = "";
            for (int i = 0; i < batteries.Count; i++)
            {
                string name = batteries[i].CustomName.Replace("__База__", "");
                string current_stored_power = (batteries[i].CurrentStoredPower * 1000).ToString("N0");
                string max_stored_power = (batteries[i].MaxStoredPower * 1000).ToString("N0");
                string product_in_percentages = (batteries[i].CurrentStoredPower / batteries[i].MaxStoredPower * 100).ToString("N0");
                text += string.Format("     {0}: {1}/{2} ({3}%) kW\n", name, current_stored_power, max_stored_power, product_in_percentages);
            }
            return text;
        }

        static string GasTanksInfo(List<IMyGasTank> tanks)
        {
            string text = "";
            for (int i = 0; i < tanks.Count; i++)
            {
                string name = tanks[i].CustomName.Replace("__База__", "");
                string filled_ratio = (tanks[i].FilledRatio * 100).ToString("N2");
                text += string.Format("     {0}: {1}%\n", name, filled_ratio);
            }
            return text;
        }

        //------------END--------------
    }
}