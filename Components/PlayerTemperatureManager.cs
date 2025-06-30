using EOSExt.EnvTemperature.Definitions;
using EOSExt.EnvTemperature.Patches;
using ExtraObjectiveSetup.Utils;
using FloLib.Infos;
using GameData;
using GTFO.API;
using Il2CppInterop.Runtime.Injection;
using Localization;
using Player;
using System;
using TMPro;
using UnityEngine;

namespace EOSExt.EnvTemperature.Components
{
    public class PlayerTemperatureManager : MonoBehaviour
    {
        public PlayerAgent PlayerAgent { get; private set; }

        public const float DEFAULT_PLAYER_TEMPERATURE = 0.5f;

        public const float TEMPERATURE_SETTING_UPDATE_TIME = 1f;

        public const float TEMPERATURE_FLUCTUATE_TIME = 1f;

        public TemperatureDefinition? TemperatureDef { get; private set; }

        public TemperatureSetting? TemperatureSetting { get; private set; }

        public float PlayerTemperature { get; private set; } = DEFAULT_PLAYER_TEMPERATURE;
        
        private float m_tempSettingLastUpdateTime = 0f;
        private float m_lastDamageTime = 0f;
        private float m_tempFluctuateTime = 0f;
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

            EOSLogger.Warning($"GameState: {GameStateManager.CurrentStateName}");
            if(GameStateManager.CurrentStateName == eGameStateName.ExpeditionFail)
            {
                OnBuildDone();
                OnEnterLevel();
            }
        }

        private void OnDestroy()
        {
            GameObject.Destroy(m_TemperatureText);
            LevelAPI.OnBuildDone -= OnBuildDone;
            LevelAPI.OnEnterLevel -= OnEnterLevel;
        }

        private void SetupGUI()
        {
            if(m_TemperatureText == null)
            {
                m_TemperatureText = UnityEngine.Object.Instantiate<TextMeshPro>(GuiManager.PlayerLayer.m_objectiveTimer.m_titleText);
                m_TemperatureText.transform.SetParent(GuiManager.PlayerLayer.m_playerStatus.gameObject.transform, false);
                m_TemperatureText.GetComponent<RectTransform>().anchoredPosition = new Vector2(-5f, 8f);
                m_TemperatureText.gameObject.transform.localPosition = new Vector3(268.2203f, 25.3799f, 0f);
                m_TemperatureText.gameObject.transform.localScale = new Vector3(0.75f, 0.75f, 0.75f);
            }

            m_TemperatureText.gameObject.SetActive(TemperatureDef != null);
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

        private void UpdateMoveSpeed() 
        { 
            if(TemperatureSetting == null || TemperatureSetting.SlowDownMultiplier_Move <= 0f)
            {
                Patches_PLOC.ResetMoveSpeedModifier();
            }
            else 
            {
                Patches_PLOC.SetMoveSpeedModifier(TemperatureSetting.SlowDownMultiplier_Move);
            }
        } 
        
        private void ResetMoveSpeed() => Patches_PLOC.ResetMoveSpeedModifier();
        
        public void UpdateTemperatureDefinition(TemperatureDefinition def)
        {
            TemperatureDef = def;
            TemperatureSetting = null;
            PlayerTemperature = def != null ? def.StartTemperature : DEFAULT_PLAYER_TEMPERATURE;
        }

        private void OnBuildDone()
        {
            var def = TemperatureDefinitionManager.Current.GetDefinition(RundownManager.ActiveExpedition.LevelLayoutData);
            UpdateTemperatureDefinition(def?.Definition ?? null);
        }

        private void OnEnterLevel()
        {
            PlayerTemperature = TemperatureDef != null ? TemperatureDef.StartTemperature : DEFAULT_PLAYER_TEMPERATURE;
            UpdateGUIText();
            SetupGUI();
            ResetMoveSpeed();
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
            int low = 0, high = settings.Count - 1;
            float cur = PlayerTemperature;
            while(low < high)
            {
                int mid = (low + high) / 2;

                float l = settings[mid].Temperature;
                float r = mid + 1 < settings.Count ? settings[mid + 1].Temperature : 1.0f;

                if (l <= cur && cur < r) break;
                else if(cur < l)
                {
                    high = mid - 1;
                }
                else // r <= cur
                {
                    low = mid + 1;
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

            float movement_speed = PlayerAgent.Locomotion.LastMoveDelta.magnitude / Clock.FixedDelta;
            float player_sprint_speed = (PlayerAgent.PlayerData.walkMoveSpeed + PlayerAgent.PlayerData.runMoveSpeed) * 0.5f;

            float actionTempDelta = 0f;
            switch (PlayerAgent.Locomotion.m_currentStateEnum)
            {
                case PlayerLocomotion.PLOC_State.Stand:
                    actionTempDelta = TemperatureDef.StandingActionHeatGained * Time.deltaTime;
                    //PlayerTemperature += TemperatureDef.StandingActionHeatGained * Time.deltaTime;
                    break;

                case PlayerLocomotion.PLOC_State.Crouch:
                    if (movement_speed <= player_sprint_speed)
                    {
                        actionTempDelta = TemperatureDef.CrouchActionHeatGained * Time.deltaTime;
                        //PlayerTemperature += TemperatureDef.CrouchActionHeatGained * Time.deltaTime;
                    }
                    else
                    {
                        actionTempDelta = (TemperatureDef.CrouchActionHeatGained + TemperatureDef.SprintActionHeatGained) * Time.deltaTime;
                        //PlayerTemperature += (TemperatureDef.CrouchActionHeatGained + TemperatureDef.SprintActionHeatGained) * Time.deltaTime;
                    }

                    break;
                    
                case PlayerLocomotion.PLOC_State.Run:
                    actionTempDelta = TemperatureDef.SprintActionHeatGained * Time.deltaTime;
                    //PlayerTemperature += TemperatureDef.SprintActionHeatGained * Time.deltaTime;
                    break;

                case PlayerLocomotion.PLOC_State.Jump:
                    if (movement_speed <= player_sprint_speed)
                    {
                        actionTempDelta = TemperatureDef.JumpActionHeatGained * Time.deltaTime;
                        //PlayerTemperature += TemperatureDef.JumpActionHeatGained * Time.deltaTime;
                    }
                    else
                    {
                        actionTempDelta = (TemperatureDef.JumpActionHeatGained + TemperatureDef.SprintActionHeatGained) * Time.deltaTime;
                        //PlayerTemperature += (TemperatureDef.JumpActionHeatGained + TemperatureDef.SprintActionHeatGained) * Time.deltaTime;
                    }
                    break;

                case PlayerLocomotion.PLOC_State.Downed:
                    //PlayerTemperature = 0.75f;
                    actionTempDelta = 0f;
                    break;

                case PlayerLocomotion.PLOC_State.ClimbLadder:
                    actionTempDelta = TemperatureDef.LadderClimbingActionHeatGained * Time.deltaTime;
                    //PlayerTemperature += TemperatureDef.LadderClimbingActionHeatGained * Time.deltaTime;
                    break;
            }

            float zoneTemp = TemperatureDef.StartTemperature;
            var z = PlayerAgent.CourseNode.m_zone;

            float temp_downlimit = TemperatureDefinitionManager.MIN_TEMP;
            float temp_uplimit = TemperatureDefinitionManager.MAX_TEMP;

            if (TemperatureDefinitionManager.Current.TryGetZoneDefinition(z.DimensionIndex, z.Layer.m_type, z.LocalIndex, out var zoneTempDef))
            {
                zoneTemp = zoneTempDef.Temperature_Normal;
                temp_downlimit = zoneTempDef.Temperature_Downlimit;
                temp_uplimit = zoneTempDef.Temperature_Uplimit;
            }
            else
            {
                zoneTempDef = TemperatureDefinitionManager.DEFAULT_ZONE_DEF;
            }

            if (Math.Abs(actionTempDelta) < 1e-5)
            {
                if (Time.time - m_tempFluctuateTime > TEMPERATURE_FLUCTUATE_TIME)
                {
                    float tempFluctuation = zoneTempDef.FluctuationIntensity * UnityEngine.Random.RandomRange(0f, 1f);
                    if (PlayerTemperature > zoneTemp)
                    {
                        PlayerTemperature -= tempFluctuation;
                    }
                    else
                    {
                        PlayerTemperature += tempFluctuation;
                    }

                    m_tempFluctuateTime = Time.time;
                }
            }
            else
            {
                m_tempFluctuateTime = Time.time;
            }

            PlayerTemperature += actionTempDelta;
            PlayerTemperature = Math.Clamp(PlayerTemperature, temp_downlimit, temp_uplimit);

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
            UpdateMoveSpeed();
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
            if (mgr == null)
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
