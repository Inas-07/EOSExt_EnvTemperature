using EOSExt.EnvTemperature.Components;
using ExtraObjectiveSetup.Utils;
using Gear;
using HarmonyLib;
using EOSExt.EnvTemperature.Definitions;
using GTFO.API;
using System.Collections.Generic;

namespace EOSExt.EnvTemperature.Patches
{
    [HarmonyPatch]
    internal static class Patch_BulletWeapon
    {
        private static TemperatureSetting? m_curSetting = null;

        private static Dictionary<uint, float> DefaultReloadTimes = new();

        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(BulletWeaponArchetype), nameof(BulletWeaponArchetype.OnWield))]
        public static void Post_OnWield(BulletWeaponArchetype __instance)
        {
            uint pid = __instance.m_archetypeData.persistentID;
            if (!DefaultReloadTimes.ContainsKey(pid))
            {
                DefaultReloadTimes[pid] = __instance.m_archetypeData.DefaultReloadTime;
            }

            var s = m_curSetting;
            if (s == null)
            {
                return;
            }

            if (s.SlowDownMultiplier_Reload > 0.0f)
            {
                __instance.m_archetypeData.DefaultReloadTime = DefaultReloadTimes[pid] * s.SlowDownMultiplier_Reload;
                EOSLogger.Debug("Temperature: Slowing down reload!");
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(BulletWeaponArchetype), nameof(BulletWeaponArchetype.Update))]
        private static void Post_Update(BulletWeaponArchetype __instance)
        {
            var s = PlayerTemperatureManager.GetCurrentTemperatureSetting();
            if (!ReferenceEquals(m_curSetting, s))
            {
                uint pid = __instance.m_archetypeData.persistentID;
                if(DefaultReloadTimes.ContainsKey(pid))
                {
                    float defaultReloadTime = DefaultReloadTimes[pid];
                    if (s == null || s.SlowDownMultiplier_Reload <= 0f)
                    {
                        __instance.m_archetypeData.DefaultReloadTime = defaultReloadTime;
                    }
                    else
                    {
                        __instance.m_archetypeData.DefaultReloadTime = defaultReloadTime * s.SlowDownMultiplier_Reload;
                    }
                }

                //EOSLogger.Debug($"Setting change: updated reload time to {__instance.m_archetypeData.DefaultReloadTime}");
                m_curSetting = s;
            }
        }

        [HarmonyPrefix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(BulletWeaponArchetype), nameof(BulletWeaponArchetype.OnUnWield))]
        private static void Pre_OnUnWield(BulletWeaponArchetype __instance)
        {
            uint pid = __instance.m_archetypeData.persistentID;
            if (!DefaultReloadTimes.ContainsKey(pid)) return;

            float defaultReloadTime = DefaultReloadTimes[pid];
            __instance.m_archetypeData.DefaultReloadTime = defaultReloadTime;
        }

        private static void Clear()
        {
            DefaultReloadTimes.Clear();
        }

        static Patch_BulletWeapon()
        {
            LevelAPI.OnBuildStart += Clear;
            LevelAPI.OnLevelCleanup += Clear;
        }
    }
}
