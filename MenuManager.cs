using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;

using ACSL.Audio;

namespace ACSL
{
    namespace Menu
    {
        public class MenuManager : MonoBehaviour
        {
            #region PRIVATE VARIABLES

            [SerializeField] private ACSL.Utility.MonoReference m_MyReference;


            //list of all the MenuScreens, by which menu they are.
            private Dictionary<ScreenTypes, MenuScreen> m_MenuScreens;

            //Stack to keep track of the active menu screens
            private Stack<MenuScreen> m_ActiveScreens;

            //boolean to check if the menu is currently open
            private bool m_IsMenuOpen;

            //Object reference to player helmet
            private Transform m_Player;
            #endregion

            #region PUBLIC VARIABLES
            //Close Menu Button
            public GameObject m_CloseButton;

            //input type
            public ACSL.ControllerInput.InputTypes m_InputType;
            
            //Button colours for different states
            public Color m_ButtonColor;
            public Color m_HighlightColor;
            public Color m_SelectedColor;
            public Color m_DisabledColor;

            //Menu Sound Object
            public SoundObject MenuSounds { get; private set; }
            #endregion

            #region ACCESSORS

            public Dictionary<ScreenTypes, MenuScreen> MenuScreens
            {
                get { return m_MenuScreens; }
                set { m_MenuScreens = value; }
            }
            public bool IsOpen
            {
                get { return m_IsMenuOpen; }
            }

            #endregion

            #region PUBLIC METHODS

            #region SCREEN METHODS
            /*
             * Run through the list of all screens in the dictionary and disable them.
             * Disable the menu buttons and clear the stack of active screens.
             * Created by: Michael Watt
             * Last Updated: 2018.10.22
             */
            private void DisableAllScreens()
            {
                foreach (ScreenTypes screen in m_MenuScreens.Keys)
                    m_MenuScreens[screen].gameObject.SetActive(false);

                //foreach (MenuScreen screen in m_ActiveScreens)
                //{
                //    //DisableMenuButtons();
                //}

                m_ActiveScreens.Clear();
            }

            /*
             * Enable the screen with the associated ScreenType
             * Created by: Michael Watt
             * Last Updated: 2018.10.22
             */
            private void EnableScreen(ScreenTypes screenToEnable)
            {
                DisableScreen();
                //Debug.Log(screenToEnable);

                //change the indices first
                m_ActiveScreens.Push(m_MenuScreens[screenToEnable]);
                m_ActiveScreens.Peek().gameObject.SetActive(true);

                //EnableMenuButtons();
            }

            private void DisableScreen()
            {
                if (m_ActiveScreens.Count < 1)
                    return;

                //DisableMenuButtons();
                m_ActiveScreens.Peek().gameObject.SetActive(false);
            }

            private void ChangeScreen(ScreenTypes screenIndex)
            {
                //may be useful for a menu with multiple screens
                DisableAllScreens();

                //enable the main menu screen
                if (m_MenuScreens[screenIndex].m_MainMenuEnabled)
                {
                    EnableScreen(ScreenTypes.MainMenu);
                    //DisableMenuButtons();
                }

                //enable the new screen, making it active.
                EnableScreen(screenIndex);
            }

            private void PreviousScreen()
            {
                DisableScreen();
                m_ActiveScreens.Pop();

                m_ActiveScreens.Peek().gameObject.SetActive(true);

                //EnableMenuButtons();

                if (m_ActiveScreens.Count <= 0)
                {
                    EnableScreen(ScreenTypes.MainMenu);
                }
            }

            public ScreenTypes ActiveScreen()
            {
                return ((MenuScreen)m_ActiveScreens.Peek()).m_ScreenType;
            }

            #endregion

            #region BUTTON METHODS
            /*
             * Disable the Menu buttons by setting the button's enabled bool to false.
             * Created by: Michael Watt
             * Last Updated: 2018.10.26
             */
            private void DisableMenuButtons()
            {
                if (m_ActiveScreens.Count > 0)
                {
                    foreach (VRMenuButton button in m_ActiveScreens.Peek().m_MenuButtons)
                    {
                        button.SetEnabled(false);
                    }
                }
            }

            /*
             * Enable the Menu buttons by setting the button's enabled bool to false.
             * Created by: Michael Watt
             * Last Updated: 2018.10.26
             */
            private void EnableMenuButtons()
            {
                if (m_ActiveScreens.Count > 0)
                {
                    foreach (VRMenuButton button in m_ActiveScreens.Peek().m_MenuButtons)
                    {
                        button.SetEnabled(true);
                    }
                }
            }

            /*
             * Subscribe to all menu button's OnActivate events
             * Created by: Michael Watt
             * Last Updated: 2018.10.26
             */
            private void SubscribeMenuButtons()
            {
                foreach (MenuScreen menu in m_MenuScreens.Values)
                {
                    foreach (VRMenuButton button in menu.m_MenuButtons)
                    {
                        button.OnButtonActivate += HandleButtonActivate;
                        button.m_ButtonColor = m_ButtonColor;
                        button.m_HighlightColor = m_HighlightColor;
                        button.m_SelectedColor = m_SelectedColor;
                        button.m_DisabledColor = m_DisabledColor;
                        button.SetOwner(this);
                    }
                }
            }
            /*
             * Unsubscribe to all menu button's OnActivate events
             * Created by: Michael Watt
             * Last Updated: 2018.10.26
             */
            private void UnsubscribeMenuButtons()
            {
                foreach (MenuScreen menu in m_MenuScreens.Values)
                {
                    foreach (VRMenuButton button in menu.m_MenuButtons)
                    {
                        button.OnButtonActivate -= HandleButtonActivate;
                    }
                }
            }

            //Function to handle event OnButtonActivate
            public void HandleButtonActivate(VRMenuButton button)
            {
                MenuSounds.PlayOneShot("CLICK", 0);

                if (button.m_ScreenSelection == ScreenTypes.None)
                    return;

                if (button.m_ScreenSelection == ScreenTypes.Back)
                {
                    PreviousScreen();
                }
                else
                {
                    EnableScreen(button.m_ScreenSelection);
                }
            }
            #endregion

            /*
             * Open the menu starting at the main menu
             * Created by Adam Brown
             * Last Updated 2018.10.22 by Michael Watt
             */
            public void OpenMenu(ScreenTypes screen = ScreenTypes.MainMenu)
            {
                if (m_IsMenuOpen)
                {
                    EnableScreen(screen);
                    return;
                }

                DisableAllScreens();

                if (screen != ScreenTypes.MainMenu)
                {
                    EnableScreen(ScreenTypes.MainMenu);
                }

                EnableScreen(screen);

                m_CloseButton.SetActive(true);

                m_IsMenuOpen = true;
            }

            //TO DO: Add functionality for BeginCloseMenu
            //Adam Brown - 09/02/2018
            public void CloseMenu()
            {
                DisableAllScreens();

                m_CloseButton.SetActive(false);

                m_IsMenuOpen = false;
            }
            public void SetPlayer(Transform player)
            {
                m_Player = player;

            }
            
            /*public void PartyInvitation(string player)
            {
                if (m_ActiveScreens.Peek().m_ScreenType == ScreenTypes.Invite)
                    return;

                DisableMenuButtons();
                UnityEngine.UI.Text[] temp = GetComponentsInChildren<UnityEngine.UI.Text>(true);
                foreach (UnityEngine.UI.Text t in temp)
                {
                    if (t.text.Contains("Party"))
                    {
                        t.text = "Party Invitation from: " + player;
                        break;
                    }
                }
                EnableScreen(ScreenTypes.Invite);

                MenuSounds.PlayOneShot("NOTIFY", 0);
            }*/
            #endregion
            #region PRIVATE METHODS
            private void Follow()
            {
                if (m_Player != null)
                {
                    Vector3 position = transform.position;
                    position.y = m_Player.transform.position.y - 0.15f;
                    transform.position = position;
                }
            }
            #endregion
            #region MONOBEHAVIOURS

            private void Awake()
            {
                //gameObject.name = "MenuManager";
                if(m_MyReference)
                    m_MyReference.reference = this;
            }

            void Start()
            {
                MenuSounds = this.GetComponent<SoundObject>();

                m_MenuScreens = new Dictionary<ScreenTypes, MenuScreen>();

                MenuScreen[] screens = GetComponentsInChildren<MenuScreen>();
                foreach (MenuScreen menu in screens)
                {
                    m_MenuScreens.Add(menu.m_ScreenType, menu);
                }

                m_ActiveScreens = new Stack<MenuScreen>();

                SubscribeMenuButtons();
            }

            private void Update()
            {
                /*if (m_IsMenuOpen == false)
                {
                    OpenMenu();
                }*/

                if (m_IsMenuOpen)
                {
                    Follow();
                }
            }

            private void OnDestroy()
            {
                UnsubscribeMenuButtons();
            }
            #endregion
        }
    }
}
