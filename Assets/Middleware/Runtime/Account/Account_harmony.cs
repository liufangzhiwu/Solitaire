#if UNITY_OPENHARMONY

using System;
using OpenHarmonyKits.Param;
using OpenHarmonyKits.Signal;
using UnityEngine;

namespace Middleware
{
    public class Account_harmony : IAccounts
    {
        public string UserId { get; set; }
        public bool IsLogin { get; set; } = false;

        private string _teamPlayerId = string.Empty;
        public void Init(float delay)
        {
            UnityTimer.Delay(delay, () =>
            {
#if UNITY_EDITOR
                // IsLogin = true;
                // UserId = Application.identifier;
#else
                OHSDKKitManager.Instance.InitGameService();
                InitGamePerformance();
                Register();
#endif
            });
        }

        public void Login(bool isShowLoginPanel = false)
        {
            #if UNITY_EDITOR
            IsLogin = true;
            UserId = Application.identifier;
            return;
            #endif
            OHThirdAccountInfo info = new OHThirdAccountInfo();
            info.accountName = "Tuanjie";
            OHSDKKitManager.Instance.Login(null,true, LoginPanelType.ICON);
        }

        public void Logout()
        {
            OHSDKKitManager.Instance.Logout();
        }

        public void VerifyPlayer()
        {
            var thirdUserInfo = new ThirdUserInfo()
            {
                thirdOpenId = "",
                isRealName = true
            };
            OHSDKKitManager.Instance.VerifyCheck(thirdUserInfo);
        }

        private void Register()
        {
            SignalHandler.Instance.RegisterSignalDelegate<GamePlayerInitSignal>(OnGamePlayerInitTrigger);
            SignalHandler.Instance.RegisterSignalDelegate<LoginSignal>(OnLoginSignalTrigger);
            SignalHandler.Instance.RegisterSignalDelegate<LogoutSignal>(OnLogoutSignalTrigger);
            SignalHandler.Instance.RegisterSignalDelegate<Login_BindSignal>(OnLoginBindTrigger);
            SignalHandler.Instance.RegisterSignalDelegate<Login_UnBindSignal>(OnLoginUnBindTrigger);
            SignalHandler.Instance.RegisterSignalDelegate<Login_VerifySignal>(OnLoginVerifyTrigger);
            SignalHandler.Instance.RegisterSignalDelegate<SavePlayerRoleSignal>(OnSavePlayerTrigger);
            SignalHandler.Instance.RegisterSignalDelegate<PlayerChangedSingal>(OnPlayerChangedTrigger);
            SignalHandler.Instance.RegisterSignalDelegate<PlayerOnOffSignal>(OnPlayerOnOffTrigger);
            SignalHandler.Instance.RegisterSignalDelegate<GamePerformance_InitSignal>(OnGamePerformanceInit);
            SignalHandler.Instance.RegisterSignalDelegate<GamePerformance_UpdateSignal>(OnGamePerformanceUpdate);
        }
        private void SavePlayer()
        {
        
            if (!IsLogin)
            {
                Debug.LogError("请先登录再保存玩家信息");
                return;
            }
            OHSDKKitManager.Instance.SavePlayerInfo(new GSKPlayerRole());
        }
        private void PlayerOn()
        {
            if (OHSDKKitManager.ReceivePlayerChangedEvent)
            {
                Debug.LogError("Player changed event is receiving now, no need to on again.");
                return;
            }

            OHSDKKitManager.Instance.EnablePlayerChangedEvent();
        }

        public void PlayerOff()
        {
            if (!OHSDKKitManager.ReceivePlayerChangedEvent)
            {
                Debug.LogError("Player changed event is not receiving now, no need to off again.");
                return;
            }

            OHSDKKitManager.Instance.DisablePlayerChangedEvent();
        }
        
        private void InitGamePerformance()
        {
            string bundleName = Application.identifier;
            string appVersion = Application.version;
            int messageType = 0;
            OHSDKKitManager.Instance.InitGamePerformance(bundleName, appVersion, messageType);
        }

        private void OnGamePlayerInitTrigger(SignalBase signal)
        {
            if (!signal.hasError())
            {
                GamePlayerInitSignal targetSignal = (GamePlayerInitSignal)signal;
                Debug.Log("[GamePlayerInit Success] " + "\n " + targetSignal.successMessage + "\n");
                Login();
            }
            else
            {
                Debug.Log(" [GamePlayerInit Error ]  Code : " + signal.code + " \n Message : " + signal.message + "\n");
            }
        }

        private void OnLoginSignalTrigger(SignalBase signal)
        {
            if (!signal.hasError())
            {
                LoginSignal targetSignal = (LoginSignal)signal;
                _teamPlayerId = targetSignal.localPlayer.teamPlayerId;
                UserId = targetSignal.localPlayer.gamePlayerId;
                IsLogin = true;
                Debug.Log("Login Success" + "\n "
                                          + "authorizationCode :" + targetSignal.authorizationCode + "\n "
                                          + "idToken : " + targetSignal.idToken + "\n"
                                          + "teamPlayerId : " + targetSignal.localPlayer.teamPlayerId + "\n"
                                          + "gamePlayerId : " + targetSignal.localPlayer.gamePlayerId + "\n");
                VerifyPlayer();
            }
            else
            {
                Debug.Log("Login Error" + "\n "
                                        + "Code : " + signal.code + " \n Message : " + signal.message + "\n");
                Game.Instance.ShowLoginErrorPanel();
            }
        }

        private void OnLogoutSignalTrigger(SignalBase signal)
        {
            if (!signal.hasError())
            {
                LogoutSignal targetSignal = (LogoutSignal)signal;
                Debug.Log("Logout Success" + "\n "
                                           + "message" + targetSignal.state + "\n");
                _teamPlayerId = string.Empty;
                IsLogin = false;
            }
            else
            {
                Debug.Log("Logout Error" + "\n "
                                         + "Code : " + signal.code + " \n Message : " + signal.message + "\n");
            }
        }

        private void OnLoginBindTrigger(SignalBase signal)
        {
            if (!signal.hasError())
            {
                Login_BindSignal targetSignal = (Login_BindSignal)signal;
                Debug.Log("LoginBind Success" + "\n "
                                              + "thirdOpenId :" + targetSignal.thirdOpenId + "\n "
                                              + "teamPlayerId :" + targetSignal.teamPlayerId + "\n ");
            }
            else
            {
                Debug.Log("LoginBind Error" + "\n "
                                            + "Code : " + signal.code + " \n Message : " + signal.message + "\n");
            }
        }

        private void OnLoginUnBindTrigger(SignalBase signal)
        {
            if (!signal.hasError())
            {
                Login_UnBindSignal targetSignal = (Login_UnBindSignal)signal;
                Debug.Log("LoginUnBind Success" + "\n "
                                                + "thirdOpenId :" + targetSignal.thirdOpenId + "\n "
                                                + "teamPlayerId : " + targetSignal.teamPlayerId + "\n ");
            }
            else
            {
                Debug.Log("LoginUnBind Error" + "\n "
                                              + "Code : " + signal.code + " \n Message : " + signal.message + "\n");
            }
        }

        private void OnLoginVerifyTrigger(SignalBase signal)
        {
            if (!signal.hasError())
            {
                Login_VerifySignal targetSignal = (Login_VerifySignal)signal;
                Debug.Log("LoginVerify Success" + "\n "
                                                + "thirdOpenId: " + targetSignal.thirdOpenId + "\n "
                                                + "isRealName : " + targetSignal.isRealName + "\n ");
                SavePlayer();
            }
            else
            {
                Debug.Log("LoginVerify Error" + "\n "
                                              + "Code : " + signal.code + " \n Message : " + signal.message + "\n");
            }
        }

        private void OnSavePlayerTrigger(SignalBase signal)
        {
            if (!signal.hasError())
            {
                SavePlayerRoleSignal targetSignal = (SavePlayerRoleSignal)signal;
                targetSignal.roleId = _teamPlayerId;
                Debug.Log("SavePlayer Success" + "\n "
                                               + "roleId : " + targetSignal.roleId + "\n "
                                               + "roleName : " + targetSignal.roleName + "\n ");

                PlayerOn();
            }
            else
            {
                Debug.Log("SavePlayer Error" + "\n "
                                             + "Code : " + signal.code + " \n Message : " + signal.message + "\n");
            }
        }

        /// <summary>
        /// 触发玩家状态变化
        /// </summary>
        /// <param name="signal"></param>
        private void OnPlayerChangedTrigger(SignalBase signal)
        {
            if (!signal.hasError())
            {
                PlayerChangedSingal targetSignal = (PlayerChangedSingal)signal;
                Debug.Log("Player Changed" + "\n "
                                           + "changedEvent : " +
                                           Enum.GetName(typeof(PlayerChangedEvent), targetSignal.changedEvent) + "\n ");
            }
        }

        /// <summary>
        /// 开启或关闭玩家状态变化的监听
        /// </summary>
        /// <param name="signal"></param>
        private void OnPlayerOnOffTrigger(SignalBase signal)
        {
            if (!signal.hasError())
            {
                PlayerOnOffSignal targetSignal = (PlayerOnOffSignal)signal;
                if (targetSignal.ReceivedPlayerChangeEvent == 1)
                {
                    Debug.Log("Enable Player ChangeEvent" + "\n ");
                }
                else
                {
                    Debug.Log("Disable Player ChangeEvent" + "\n ");
                }
            }
            else
            {
                Debug.Log("Change Player ChangeEvent Status Error" + "\n "
                                                                   + "Code : " + signal.code + " \n Message : " +
                                                                   signal.message + "\n");
            }
        }

        private void OnGamePerformanceInit(SignalBase signal)
        {
            if (!signal.hasError())
            {
                GamePerformance_InitSignal targetSignal = (GamePerformance_InitSignal)signal;
                Debug.Log("GamePerformanceInit Success" + "\n "
                                                        + "bundleName : " + targetSignal.bundleName + "\n "
                                                        + " appVersion :" + targetSignal.appVersion + "\n "
                                                        + $" messageType : {targetSignal.messageType}" + "\n ");
            }
            else
            {
                Debug.Log("GamePerformanceInit Error" + "\n "
                                                      + "Code : " + signal.code + " \n Message : " + signal.message +
                                                      "\n");
            }
        }

        private void OnGamePerformanceUpdate(SignalBase signal)
        {
            if (!signal.hasError())
            {
                GamePerformance_UpdateSignal targetSignal = (GamePerformance_UpdateSignal)signal;
                Debug.Log("PerformanceUpdate Success" + "\n "
                                                      + "extra message : " + targetSignal.extra + "\n "
                                                      + $"\n messageType is{targetSignal.messageType}" + "\n ");
            }
            else
            {
                Debug.Log("PerformanceUpdate Error" + "\n "
                                                    + "Code : " + signal.code + " \n Message : " + signal.message +
                                                    "\n");
            }
        }
    }
}
#endif