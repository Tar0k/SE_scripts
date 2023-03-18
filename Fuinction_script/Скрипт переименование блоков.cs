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

namespace Script4
{
    public sealed class Program : MyGridProgram
    {
        //------------BEGIN--------------

        List<IMyTerminalBlock> all_blocks = new List<IMyTerminalBlock>();

        public Program()
        {
        }



        public void Save()

        {
        }



        public void Main(string argument, UpdateType updateSource)

        {
            string ship_name = Me.CubeGrid.CustomName;
            IMyTextSurface plc_screen_1 = Me.GetSurface(0);
            plc_screen_1.WriteText(string.Format("Скрипт выполняется..."), false);
            // Функция автоматического переименования всех блоков
            // Формат аргумента:
            //                      rename_all ('Имя обвязки названия корабля')
            //                                                         ↑↑↑↑↑↑↑↑↑↑↑↑↑
            //                                                опцион. аргумент  *Вводится ВМЕСТО ()
            // Пример переименования с аргументами:
            //                      Команда: rename_all __
            //                      Результат: Кузнечик Радар 1 *Кузнечик - название корабля, Радар 1 - имя блока до команды
            // Пример переименования без аргументов:
            //                      Команда: rename_all
            //                      Результат: Кузнечик Радар 1 *Кузнечик - название корабля, Радар 1 - имя блока до команды
            if (argument.StartsWith("rename_all"))
            {
                GridTerminalSystem.GetBlocksOfType(all_blocks, block => block.IsSameConstructAs(Me));
                int blocks_number = all_blocks.Count();
                plc_screen_1.WriteText(string.Format("Найдено блоков: {0}\n", blocks_number), true);
                string name_wrapper = "";
                string[] args = argument.Split();
                if (args.Count() > 1)
                {
                    name_wrapper = args[1];
                }
                for (int i = 0; i < all_blocks.Count(); i++)
                {
                    IMyTerminalBlock block = all_blocks[i];
                    if (!block.CustomName.StartsWith(string.Format("{1}{0}{1} ", block.CubeGrid.CustomName, name_wrapper)))
                    {
                        block.CustomName = string.Format("{2}{0}{2} {1}", block.CubeGrid.CustomName, block.CustomName, name_wrapper);
                    }
                }
            }
            plc_screen_1.WriteText(string.Format("Скрипт успешно выполнен."), false);

        }



        //------------END--------------
    }
}