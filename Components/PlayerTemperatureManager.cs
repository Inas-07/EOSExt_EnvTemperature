﻿using EOSExt.EnvTemperature.Definitions;
using EOSExt.EnvTemperature.Patches;
using ExtraObjectiveSetup.Utils;
using FloLib.Infos;
using GameData;
using GTFO.API;
using Il2CppInterop.Runtime.Injection;
using LevelGeneration;
using Localization;
using Player;
using System;
using TMPro;
using Unity.Mathematics;
using UnityEngine;

namespace EOSExt.EnvTemperature.Components
{
    public class PlayerTemperatureManager : MonoBehaviour
    {
        public PlayerAgent PlayerAgent { get; private set; }

        public const float DEFAULT_PLAYER_TEMPERATURE = 0.5f;

        public const float TEMPERATURE_SETTING_UPDATE_TIME = 1f;

        public TemperatureDefinition? TemperatureDef { get; private set; }

        public TemperatureSetting? TemperatureSetting { get; private set; }

        public float PlayerTemperature { get; private set; } = DEFAULT_PLAYER_TEMPERATURE;
        
        private float m_tempSettingLastUpdateTime = 0f;
        private float m_lastDamageTime = 0f;
        private bool m_ShowDamageWarning = true;

        private TextMeshPro m_TemperatureText;

        private const string DEFAULT_GUI_TEXT = "SUIT TEMP: <color=orange>{0}</color>";

        private string m_GUIText = DEFAULT_GUI_TEXT;

        private readonly Color m_lowTempColor = new(0, 0.5f, 0.5f);
        private readonly Color m_midTempColor = new(1f, 0.64f, 0f);
        private readonly Color m_highTempColor = new(1f, 0.07f, 0.576f);

        internal void Setup()
        {
            TemperatureDef = null;
            PlayerAgent = gameObject.GetComponent<PlayerAgent>();
            LevelAPI.OnBuildDone += OnBuildDone;
            LevelAPI.OnEnterLevel += OnEnterLevel;
        }

        private void OnDestroy()
        {
            GameObject.Destroy(m_TemperatureText);
            LevelAPI.OnBuildDone -= OnBuildDone;
            LevelAPI.OnEnterLevel -= OnEnterLevel;
        }

        private void SetupGUI()
        {
            if(m_TemperatureText != null)
            {
                return;
            }

            m_TemperatureText = UnityEngine.Object.Instantiate<TextMeshPro>(GuiManager.PlayerLayer.m_objectiveTimer.m_titleText);
            m_TemperatureText.transform.SetParent(GuiManager.PlayerLayer.m_playerStatus.gameObject.transform, false);
            m_TemperatureText.GetComponent<RectTransform>().anchoredPosition = new Vector2(-5f, 8f);
            m_TemperatureText.gameObject.transform.localPosition = new Vector3(268.2203f, 25.3799f, 0f);
            m_TemperatureText.gameObject.transform.localScale = new Vector3(0.75f, 0.75f, 0.75f);
        }

        internal void UpdateGUIText()
        {
            var b = GameDataBlockBase<TextDataBlock>.GetBlock("EnvTemperature.Text");
            m_GUIText = b != null ? Text.Get(b.persistentID) : DEFAULT_GUI_TEXT;
        }

        private void UpdateGui()
        {
            if(TemperatureDef != null)
            {
                m_TemperatureText.gameObject.SetActive(true);
                m_TemperatureText.SetText(string.Format(m_GUIText, (PlayerTemperature * 100f).ToString("N0")), true);
                if(PlayerTemperature > 0.5f)
                {
                    m_TemperatureText.color = Color.Lerp(m_midTempColor, m_highTempColor, (PlayerTemperature - 0.5f) * 2);
                }
                else
                {
                    m_TemperatureText.color = Color.Lerp(m_lowTempColor, m_midTempColor, PlayerTemperature * 2);
                }
                m_TemperatureText.ForceMeshUpdate(false, false);
            }
            else
            {
                m_TemperatureText.gameObject.SetActive(false);
            }
        }

        public void UpdateTemperatureDefinition(TemperatureDefinition def)
        {
            TemperatureDef = def;
            TemperatureSetting = null;
            PlayerTemperature = def != null ? def.StartTemperature : DEFAULT_PLAYER_TEMPERATURE;
        }

        private void OnBuildDone()
        {
            var def = TemperatureDefinitionManager.Current.GetDefinition(RundownManager.ActiveExpedition.LevelLayoutData);
            UpdateTemperatureDefinition(def.Definition);
        }

        private void OnEnterLevel()
        {
            PlayerTemperature = TemperatureDef != null ? TemperatureDef.StartTemperature : DEFAULT_PLAYER_TEMPERATURE;
            UpdateGUIText();
            SetupGUI();
        }

        private void DealDamage()
        {
            if (TemperatureSetting == null) return;
            if (TemperatureSetting.Damage < 0 || Time.time - m_lastDamageTime < TemperatureSetting.DamageTick) return;

            Patch_Dam_PlayerDamageBase.s_disableDialog = true;
            PlayerAgent.Damage.FallDamage(TemperatureSetting.Damage);
            Patch_Dam_PlayerDamageBase.s_disableDialog = false;
            //GuiManager.PlayerLayer.m_playerStatus.StopWarning(true, new PUI_LocalPlayerStatus.WarningColors
            //{
            //    healthBad = GuiManager.PlayerLayer.m_playerStatus.m_healthBad,
            //    healthBadPulse = GuiManager.PlayerLayer.m_playerStatus.m_healthBadPulse,
            //    healthWarningDark = GuiManager.PlayerLayer.m_playerStatus.m_healthWarningDark,
            //    healthWarningBright = GuiManager.PlayerLayer.m_playerStatus.m_healthWarningBright
            //});

            //m_ShowDamageWarning = true;
            m_lastDamageTime = Time.time;
        }


        private void UpdateTemperatureSettings()
        {
            if (Time.time - m_tempSettingLastUpdateTime < TEMPERATURE_SETTING_UPDATE_TIME) return;

            if (!TemperatureDefinitionManager.Current.TryGetLevelTemperatureSettings(out var settings))
            {
                TemperatureSetting = null; // no available setting
                return;
            }

            // binary search for settings that matches current temperature
            int low = 0, high = settings.Count;
            float cur = PlayerTemperature;
            while(low < high)
            {
                int mid = (low + high) / 2;

                var s = settings[mid];
                if(cur > s.Temperature)
                {
                    low = mid + 1;
                }
                else
                {
                    high = mid;
                }
            }

            TemperatureSetting = settings[Math.Clamp((low + high) / 2, 0, settings.Count - 1)];

            m_tempSettingLastUpdateTime = Time.time;
        }

        void Update()
        {
            if (GameStateManager.CurrentStateName != eGameStateName.InLevel 
                || TemperatureDef == null 
                || PlayerAgent == null) return;

            var z = PlayerAgent.CourseNode.m_zone;

            if(!TemperatureDefinitionManager.Current.TryGetZoneDefinition(z.DimensionIndex, z.Layer.m_type, z.LocalIndex, out var zoneTempDef))
            {
                zoneTempDef = TemperatureDefinitionManager.DEFAULT_ZONE_DEF;
            }

            PlayerTemperature -= zoneTempDef.DecreaseRate * Time.deltaTime;
            PlayerTemperature = Math.Clamp(PlayerTemperature, 0.005f, 1f);

            float movement_speed = PlayerAgent.Locomotion.LastMoveDelta.magnitude / Clock.FixedDelta;
            float player_sprint_speed = (PlayerAgent.PlayerData.walkMoveSpeed + PlayerAgent.PlayerData.runMoveSpeed) * 0.5f;

            switch (PlayerAgent.Locomotion.m_currentStateEnum)
            {
                case PlayerLocomotion.PLOC_State.Stand:
                    PlayerTemperature += TemperatureDef.StandingActionHeatGained * Time.deltaTime;
                    break;

                case PlayerLocomotion.PLOC_State.Crouch:
                    if (movement_speed <= player_sprint_speed)
                    {
                        PlayerTemperature += TemperatureDef.CrouchActionHeatGained * Time.deltaTime;
                    }
                    else
                    {
                        PlayerTemperature += (TemperatureDef.CrouchActionHeatGained + TemperatureDef.SprintActionHeatGained) * Time.deltaTime;
                    }

                    break;
                    
                case PlayerLocomotion.PLOC_State.Run:
                    PlayerTemperature += TemperatureDef.SprintActionHeatGained * Time.deltaTime;
                    break;

                case PlayerLocomotion.PLOC_State.Jump:
                    if (movement_speed <= player_sprint_speed)
                    {
                        PlayerTemperature += TemperatureDef.JumpActionHeatGained * Time.deltaTime;
                    }
                    else
                    {
                        PlayerTemperature += (TemperatureDef.JumpActionHeatGained + TemperatureDef.SprintActionHeatGained) * Time.deltaTime;
                    }
                    break;

                case PlayerLocomotion.PLOC_State.Downed:
                    PlayerTemperature = 0.75f;
                    break;
                case PlayerLocomotion.PLOC_State.ClimbLadder:
                    PlayerTemperature += TemperatureDef.LadderClimbingActionHeatGained * Time.deltaTime;
                    break;
            }

            UpdateTemperatureSettings();

            if (TemperatureSetting?.Damage > 0f)
            {
                //if(m_ShowDamageWarning)
                //{
                //    GuiManager.PlayerLayer.m_playerStatus.StartHealthWarning(0.8f, 0.3f, 0f, false);
                //    m_ShowDamageWarning = false;
                //}
                DealDamage();
            }

            UpdateGui();
        }

        public static bool TryGetCurrentManager(out PlayerTemperatureManager mgr)
        {
            mgr = null;
            if (!LocalPlayer.TryGetLocalAgent(out var p))
            {
                EOSLogger.Debug("Temperature: cannot get localplayeragent");
                return false;
            }

            mgr = p.gameObject.GetComponent<PlayerTemperatureManager>();
            if (p == null)
            {
                EOSLogger.Error("LocalPlayerAgent does not have `PlayerTemperatureManager`!");
                return false;
            }

            return true;
        }

        public static TemperatureSetting? GetCurrentTemperatureSetting()
        {
            if(!TryGetCurrentManager(out var mgr))
            {
                return null;
            }

            return mgr.TemperatureSetting; // nullable
        }

        static PlayerTemperatureManager()
        {
            ClassInjector.RegisterTypeInIl2Cpp<PlayerTemperatureManager>();
        }
    }
}