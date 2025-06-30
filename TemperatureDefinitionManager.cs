using EOSExt.EnvTemperature.Components;
using EOSExt.EnvTemperature.Definitions;
using ExtraObjectiveSetup.BaseClasses;
using ExtraObjectiveSetup.Utils;
using GameData;
using GTFO.API;
using GTFO.API.Utilities;
using LevelGeneration;
using System;
using System.Collections.Generic;

namespace EOSExt.EnvTemperature
{
    public class TemperatureDefinitionManager : GenericDefinitionManager<TemperatureDefinition>
    {
        public static TemperatureDefinitionManager Current { get; } = new();

        protected override string DEFINITION_NAME => "EnvTemperature";

        public static readonly TemperatureZoneDefinition DEFAULT_ZONE_DEF = new TemperatureZoneDefinition() { FluctuationIntensity = 0.0025f };

        public const float MIN_TEMP = 0.005f;

        public const float MAX_TEMP = 1f;

        private Dictionary<(eDimensionIndex dimensionIndex, LG_LayerType layerType, eLocalZoneIndex localIndex), TemperatureZoneDefinition> zoneDefs { get; } = new();

        protected override void AddDefinitions(GenericDefinition<TemperatureDefinition> definition)
        {
            var tempIntervals = definition.Definition.Settings;
            if (tempIntervals.Count > 0) 
            { 
                tempIntervals.Sort((t1, t2) => t1.Temperature.CompareTo(t2.Temperature));
                
                bool valid = true;
                if (tempIntervals[0].Temperature < 0.0f)
                {
                    EOSLogger.Error($"Found negative temperature: '{tempIntervals[0].Temperature}'");
                    valid = false;
                }
                if (tempIntervals[tempIntervals.Count - 1].Temperature > 1.0f)
                {
                    EOSLogger.Error($"Found temperature greater than 1: '{tempIntervals[tempIntervals.Count - 1].Temperature}'");
                    valid = false;
                }
                if(!valid)
                {
                    EOSLogger.Error($"Found invalid temperature for MainLevelLayout '{definition.ID}'. Correct it first to make it effective");
                    return;
                }


                if (tempIntervals[0].Temperature != 0.0f)
                {
                    List<TemperatureSetting> newList = new();

                    TemperatureSetting prependSetting = new(tempIntervals[0])
                    {
                        Temperature = 0.0f
                    };

                    newList.Add(prependSetting);
                    newList.AddRange(tempIntervals);

                    definition.Definition.Settings = newList;

                    tempIntervals.Clear();
                    tempIntervals = newList;
                }
            }

            foreach(var zoneDef in definition.Definition.Zones)
            {
                zoneDef.Temperature_Downlimit = Math.Clamp(zoneDef.Temperature_Downlimit, MIN_TEMP, MAX_TEMP);
                zoneDef.Temperature_Uplimit = Math.Clamp(zoneDef.Temperature_Uplimit, MIN_TEMP, MAX_TEMP);

                if (zoneDef.Temperature_Downlimit > zoneDef.Temperature_Uplimit)
                {
                    EOSLogger.Error($"Invalid Temperature_Down/Up-limit setting! Downlimit == {zoneDef.Temperature_Downlimit}, Uplimit == {zoneDef.Temperature_Uplimit}");
                    float temp = zoneDef.Temperature_Downlimit;
                    zoneDef.Temperature_Downlimit = zoneDef.Temperature_Uplimit;
                    zoneDef.Temperature_Uplimit = temp;
                }

                if(!(zoneDef.Temperature_Downlimit <= zoneDef.Temperature_Normal && zoneDef.Temperature_Normal <= zoneDef.Temperature_Uplimit))
                {
                    EOSLogger.Error($"Invalid Temperature_Normal setting! Temperature_Normal == {zoneDef.Temperature_Normal} not in limit range [{zoneDef.Temperature_Downlimit}, {zoneDef.Temperature_Uplimit}] !");
                    zoneDef.Temperature_Normal = Math.Clamp(zoneDef.Temperature_Normal, zoneDef.Temperature_Downlimit, zoneDef.Temperature_Uplimit);
                }

                zoneDef.FluctuationIntensity = Math.Abs(zoneDef.FluctuationIntensity);
            }

            base.AddDefinitions(definition);
        }

        protected override void FileChanged(LiveEditEventArgs e)
        {
            base.FileChanged(e);

            Clear();
            OnBuildDone();

            if(PlayerTemperatureManager.TryGetCurrentManager(out var mgr))
            {
                if(definitions.TryGetValue(RundownManager.ActiveExpedition.LevelLayoutData, out var def))
                {
                    mgr.UpdateTemperatureDefinition(def.Definition);
                    mgr.UpdateGUIText();
                }
            }
        }

        private void OnBuildDone()
        {
            if (!definitions.TryGetValue(RundownManager.ActiveExpedition.LevelLayoutData, out var def)) return;

            def.Definition.Zones.ForEach((def) => { 
                if(zoneDefs.ContainsKey((def.DimensionIndex, def.LayerType, def.LocalIndex)))
                {
                    EOSLogger.Warning($"TemperatureDefinitionManager: duplicate definition found: {(def.DimensionIndex, def.LayerType, def.LocalIndex)}");
                }

                zoneDefs[(def.DimensionIndex, def.LayerType, def.LocalIndex)] = def;
            });
        }

        private void Clear() 
        {
            zoneDefs.Clear();
        }

        public bool TryGetZoneDefinition(eDimensionIndex dimensionIndex, LG_LayerType layerType, eLocalZoneIndex localIndex, out TemperatureZoneDefinition def) 
            => zoneDefs.TryGetValue((dimensionIndex, layerType, localIndex), out def);

        public bool TryGetLevelTemperatureSettings(out List<TemperatureSetting> settings)
        {
            if(definitions.TryGetValue(RundownManager.ActiveExpedition.LevelLayoutData, out var def))
            {
                settings = def.Definition.Settings;
                return settings != null && settings.Count > 0;
            }
            else
            {
                settings = null;
                return false;
            }
        }


        private TemperatureDefinitionManager() 
        {
            LevelAPI.OnBuildDone += OnBuildDone;
            LevelAPI.OnBuildStart += Clear;
            LevelAPI.OnLevelCleanup += Clear;
        }

        static TemperatureDefinitionManager() 
        {

        }
    }
}
