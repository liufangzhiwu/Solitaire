#if UNITY_ANDROID

using System;
using UnityEngine;

namespace Middleware
{
    public class Account_android : IAccounts
    {
        public string UserId { get; set; }
        public bool IsLogin { get; set; } = false;

        private string _teamPlayerId = string.Empty;
        public void Init(float delay)
        {
            UnityTimer.Delay(delay, () =>
            {
                IsLogin = true;
                UserId = SystemInfo.deviceUniqueIdentifier;
            });
        }

        public void Login(bool isShowLoginPanel = false)
        {
            IsLogin = true;
            UserId = SystemInfo.deviceUniqueIdentifier;
        }

        public void Logout()
        {
          
        }

        public void VerifyPlayer()
        {
  
        }
    }
}
#endif