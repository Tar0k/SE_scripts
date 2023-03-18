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

namespace SE_scripts.tests
{
    public sealed class Program : MyGridProgram
    {
        //------------BEGIN--------------

        List<IMyTerminalBlock> all_blocks = new List<IMyTerminalBlock>();
        List<MyInventoryItem> container1 = new List<MyInventoryItem>();

        public Program()
        {
            // Конструктор, вызванный единожды в каждой сессии и
            //  всегда перед вызовом других методов. Используйте его,
            // чтобы инициализировать ваш скрипт.
            // 
            // Конструктор опционален и может быть удалён,
            // если в нём нет необходимости.
            //
            // Рекомендуется использовать его, чтобы установить RuntimeInfo.UpdateFrequency
            // , что позволит перезапускать ваш скрипт
            // автоматически, без нужды в таймере.
        }



        public void Save()

        {
            // Вызывается, когда программе требуется сохранить своё состояние.
            // Используйте этот метод, чтобы сохранить состояние программы в поле Storage,
            // или в другое место.
            //
            // Этот метод опционален и может быть удалён,
            // если не требуется.
        }



        public void Main(string argument, UpdateType updateSource)

        {
            Dictionary<string, MyFixedPoint> items1 = new Dictionary<string, MyFixedPoint>();
            Dictionary<string, MyFixedPoint> items2 = new Dictionary<string, MyFixedPoint>();
            Dictionary<string, MyFixedPoint> items3 = new Dictionary<string, MyFixedPoint>();
            Dictionary<string, MyFixedPoint> items4 = new Dictionary<string, MyFixedPoint>();
            Dictionary<string, MyFixedPoint> items5 = new Dictionary<string, MyFixedPoint>();
            Dictionary<string, MyFixedPoint> items6 = new Dictionary<string, MyFixedPoint>();
            Dictionary<string, MyFixedPoint> items7 = new Dictionary<string, MyFixedPoint>();
            Dictionary<string, MyFixedPoint> items8 = new Dictionary<string, MyFixedPoint>();
            Dictionary<string, MyFixedPoint> items9 = new Dictionary<string, MyFixedPoint>();

            Dictionary<string, Dictionary<string, MyFixedPoint>> current_items = new Dictionary<string, Dictionary<string, MyFixedPoint>>()
    {
        { "MyObjectBuilder_Ingot", items1},
        { "MyObjectBuilder_AmmoMagazine", items2},
        { "MyObjectBuilder_Component", items3},
        { "MyObjectBuilder_Ore", items4},
        { "MyObjectBuilder_ConsumableItem", items5},
        { "MyObjectBuilder_PhysicalGunObject", items6},
        { "MyObjectBuilder_OxygenContainerObject", items7},
        { "MyObjectBuilder_PhysicalObject", items8},
        { "MyObjectBuilder_GasContainerObject", items9}
    };


            IMyTextSurface plc_screen_1 = Me.GetSurface(0);
            plc_screen_1.WriteText(string.Format("Скрипт выполняется...\n"), false);
            IMyTextPanel? ore_screen = GridTerminalSystem.GetBlockWithName("Рудный дисплей") as IMyTextPanel;
            IMyTextPanel? component_screen = GridTerminalSystem.GetBlockWithName("Компонентный дисплей") as IMyTextPanel;
            IMyTextPanel? ingot_screen = GridTerminalSystem.GetBlockWithName("Слитковый дисплей") as IMyTextPanel;
            IMyTextPanel? ammo_screen = GridTerminalSystem.GetBlockWithName("Патронный дисплей") as IMyTextPanel;
            ore_screen.FontSize = 0.8f;
            component_screen.FontSize = 0.8f;
            ingot_screen.FontSize = 0.8f;
            ammo_screen.FontSize = 0.8f;

            GridTerminalSystem.GetBlocksOfType(all_blocks, block => block.IsSameConstructAs(Me));
            plc_screen_1.WriteText(string.Format("{0}\n", all_blocks.Count().ToString()), true);

            List<IMyInventory> containers = (from block in all_blocks
                                             where block.HasInventory
                                             select block.GetInventory()).ToList();
            plc_screen_1.WriteText(string.Format("{0}\n", containers.Count().ToString()), true);


            ore_screen.WriteText(containers.Count().ToString(), true);
            ore_screen.WriteText("\n", true);
            for (int i = 0; i < containers.Count(); i++)
            {
                List<MyInventoryItem> container = new List<MyInventoryItem>();
                containers[i].GetItems(container);
                for (int k = 0; k < container.Count(); k++)
                {
                    if (!current_items.ContainsKey(container[k].Type.TypeId))
                    {
                        ore_screen.WriteText(string.Format("!!!  ВНИМАНИЕ !!! \n Найдена новая сущность:  {0}\n", container[k].Type.TypeId), true);
                        break;
                    }

                    if (!current_items[container[k].Type.TypeId].ContainsKey(container[k].Type.SubtypeId))
                    {
                        current_items[container[k].Type.TypeId].Add(container[k].Type.SubtypeId, container[k].Amount);
                    }
                }
            }

            ore_screen.WriteText("-----------------------РУДЫ---------------------------\n", false);
            foreach (var item in current_items["MyObjectBuilder_Ore"])
            {
                ore_screen.WriteText(item.Key, true);
                ore_screen.WriteText(" ", true);
                ore_screen.WriteText(ValueConverter(item.Value, "weight"), true);
                ore_screen.WriteText("\n", true);
            }

            ingot_screen.WriteText("-----------------------СЛИТКИ---------------------------\n", false);
            foreach (var item in current_items["MyObjectBuilder_Ingot"])
            {
                ingot_screen.WriteText(item.Key, true);
                ingot_screen.WriteText(" ", true);
                ingot_screen.WriteText(ValueConverter(item.Value, "weight"), true);
                ingot_screen.WriteText("\n", true);
            }

            component_screen.WriteText("-----------------------КОМПОНЕНТЫ---------------------------\n", false);
            foreach (var item in current_items["MyObjectBuilder_Component"])
            {
                component_screen.WriteText(item.Key, true);
                component_screen.WriteText(" ", true);
                component_screen.WriteText(ValueConverter(item.Value, "item"), true);
                component_screen.WriteText("\n", true);
            }

            ammo_screen.WriteText("-----------------------БОЕПРИПАСЫ---------------------------\n", false);
            foreach (var item in current_items["MyObjectBuilder_AmmoMagazine"])
            {
                ammo_screen.WriteText(item.Key, true);
                ammo_screen.WriteText(" ", true);
                ammo_screen.WriteText(ValueConverter(item.Value, "item"), true);
                ammo_screen.WriteText("\n", true);
            }




        }


        static string ValueConverter(MyFixedPoint value, string type)
        {
            float raw_value = value.RawValue;
            string result;
            switch (type)
            {
                case "weight":
                    float value_kg = raw_value / 1000000;
                    if (value_kg < 999)
                    {
                        result = string.Format("{0:N2} кг.", value_kg);
                    }
                    else if (value_kg >= 999 && value_kg < 1000000)
                    {
                        float value_t = value_kg / 1000;
                        result = string.Format("{0:N2} тонн.", value_t);
                    }
                    else
                    {
                        float value_kt = value_kg / 1000000;
                        result = string.Format("{0:N2} килотонн.", value_kt);
                    }
                    break;
                case "item":
                    float value_parts = raw_value / 1000000;
                    result = string.Format("{0:N2} штук.", value_parts.ToString());
                    break;
                default:
                    result = "Неверный тип ввода";
                    break;
            }
            return result;
        }



        //------------END--------------
    }
}