using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace ACSL
{
    namespace Menu
    {
        public class MenuScreen : MonoBehaviour
        {
            public ScreenTypes m_ScreenType;
            public List<VRMenuButton> m_MenuButtons;
            public bool m_MainMenuEnabled = true;

            private void Awake()
            {
                m_MenuButtons = new List<VRMenuButton>();
                Init();
            }
            private void Init()
            {
                foreach (VRMenuButton button in GetComponentsInChildren<VRMenuButton>())
                {
                    m_MenuButtons.Add(button);
                }
            }
        }
    }
}