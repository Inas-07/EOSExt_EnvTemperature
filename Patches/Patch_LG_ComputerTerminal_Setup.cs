﻿using HarmonyLib;
using LevelGeneration;
using ExtraObjectiveSetup.ObjectiveInstance;
namespace ExtraObjectiveSetup.Patches
{
    [HarmonyPatch]
    internal class Patch_LG_ComputerTerminal_Setup
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(LG_ComputerTerminal), nameof(LG_ComputerTerminal.Setup))]
        private static void Post_LG_ComputerTerminal_Setup(LG_ComputerTerminal __instance)
        {
            TerminalManager.Current.Register(__instance);
        }
    }
}