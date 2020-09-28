using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ACSL.Interaction;
using ACSL.Network;

namespace ACSL.Menu
{
    public class WristMenu : MonoBehaviour
    {
        #region PUBLIC VARIABLES
        public GameObject m_LaserPointer;
        public GameObject m_OpenButton;
        public GameObject m_CloseButton;
        public GameObject m_ExitButton;
        public Transform m_Eye;
        public Transform m_Hand;
        public float m_TimeToClose = 2f;

        public bool m_Debug;

        public GameObject m_Invitation;
        //public GameObject m_Reconnect;
        #endregion

        #region PRIVATE VARIABLES
        [SerializeField] private ACSL.Utility.MonoReference m_MenuReference;
        private MenuManager m_MenuManager { get { return m_MenuReference.reference as MenuManager; } }
        private GameObject m_MenuCloseButton;
        private float m_TimePassed;
        private int m_LayerMask;

        #endregion

        // Use this for initialization
        void Start()
        {
            Init();
        }

        private void OnDestroy()
        {
            //Unsubscribe all buttons when the wrist menu is destroyed
            m_OpenButton.GetComponentInChildren<VRMenuButton>().OnButtonActivate -= OpenMainMenu;
            m_CloseButton.GetComponentInChildren<VRMenuButton>().OnButtonActivate -= CloseMenu;
            m_ExitButton.GetComponentInChildren<VRMenuButton>().OnButtonActivate -= OpenMainMenu;

            m_MenuCloseButton.GetComponentInChildren<VRMenuButton>().OnButtonActivate -= CloseMenu;
        }

        // Update is called once per frame
        void Update()
        {
            //Implemented to help with debugging so the menu can stay open without needing to look at it
            if (m_Debug)
                return;

            //Create the struct to store the hit information
            RaycastHit hit;

            //If the menu is not open
            if (!m_MenuManager.IsOpen)
            {
                //Calculate the ray direction from the position of the right hand to the forward
                Ray handRay = new Ray(m_LaserPointer.transform.position, m_LaserPointer.transform.forward);

                //See if the hand is pointing to a menu
                if (Physics.Raycast(handRay, out hit, 5f, m_LayerMask))
                {
                    //Turn on the laser pointer if pointing to a menu
                    m_LaserPointer.SetActive(true);
                    GrabPoint.RightHand.m_HandUtility.GetComponent<HandUtility>().ToggleBool("Pointing", true);
                }
                else
                {
                    //Otherwise turn off
                    if (m_LaserPointer != null)
                    {
                        m_LaserPointer.SetActive(false);
                        GrabPoint.RightHand.m_HandUtility.GetComponent<HandUtility>().ToggleBool("Pointing", false);
                    }
                }
            }

            //else the Menu is open, check to see if it should close
            else
            {
                //If the menu is open, turn on the laser pointer
                m_LaserPointer.SetActive(true);
                GrabPoint.RightHand.m_HandUtility.GetComponent<HandUtility>().ToggleBool("Pointing", true);

                //Calculate the ray direction from the position of the camera to the forward
                Ray eyeRay = new Ray(m_Eye.position, m_Eye.forward);
                //Calculate the ray direction from the position of the right hand to the forward

                Ray handRay = new Ray(m_LaserPointer.transform.position, m_LaserPointer.transform.forward);

                //Sphre cast from the camera because the player might not be turning their head directly to the menu
                if (Physics.SphereCast(eyeRay, 0.5f, out hit, 5f, m_LayerMask))
                {
                    //If something on the Menu Layer is hit, reset the timer to 0
                    m_TimePassed = 0f;

                    // Show the debug ray if required
                    if (m_Debug)
                        Debug.DrawRay(m_Eye.position, m_Eye.forward * hit.distance, Color.blue, 0.2f);
                }
                //If the camera doesn't see something, test to see if the hand is using the menu
                if (Physics.Raycast(handRay, out hit, 5f, m_LayerMask))
                {
                    //If something on the Menu layer is hit, reset the timer to 0
                    m_TimePassed = 0f;

                    //set the hand to pointing.
                    GrabPoint.RightHand.m_HandUtility.GetComponent<HandUtility>().ToggleBool("Pointing", true);

                    //Show the debug ray if required
                    if (m_Debug)
                        Debug.DrawRay(m_Hand.position, m_Hand.forward * hit.distance, Color.green, 0.2f);
                }

                //Increase time passed by delta time
                m_TimePassed += Time.deltaTime;

                if (m_TimePassed >= m_TimeToClose)
                {
                    //If more time has passed than the time to close allows, close the menu
                    Close();
                }
            }
        }

        private void Init()
        {
            if (m_MenuManager != null && m_Eye != null)
            {
                m_MenuManager.SetPlayer(m_Eye);
                m_MenuCloseButton = m_MenuManager.m_CloseButton;
            }

            //Subscribe the buttons to their events
            m_OpenButton.GetComponentInChildren<VRMenuButton>().OnButtonActivate += OpenMainMenu;
            m_CloseButton.GetComponentInChildren<VRMenuButton>().OnButtonActivate += CloseMenu;
            m_ExitButton.GetComponentInChildren<VRMenuButton>().OnButtonActivate += OpenMainMenu;

            if(m_MenuCloseButton)
                m_MenuCloseButton.GetComponentInChildren<VRMenuButton>().OnButtonActivate += CloseMenu;

            //Initialize time passed to 0
            m_TimePassed = 0f;

            //Bitshift the layer mask to only hit the Menu layer
            m_LayerMask = 1 << LayerMask.NameToLayer("Menu");

            //Turn on the Open button, turn off the Close button to start
            m_OpenButton.SetActive(true);
            m_CloseButton.SetActive(false);

            //if(Game.GameManager.Instance.NetworkManager.m_PromptReconnect)
            //{
            //    ReconnectRequest();
            //}
        }

        private void OpenMainMenu(VRMenuButton button)
        {
            //Disable the Open Menu button, then enable the close menu button
            m_OpenButton.SetActive(false);
            m_CloseButton.SetActive(true);

            //Calculate the position at which to open the menu in front of the player's vision
            Vector3 pos = m_Eye.position;
            Vector3 forward = m_Eye.forward;

            //Change the y value to be slightly below the player's head
            forward.y = -0.15f;

            //Normalize to get a direction vector
            forward.Normalize();

            //Move point along the calculated direction vector
            forward *= 0.9f;
            pos += forward;

            //Calculate the rotation so that the menu is facing the player
            Quaternion viewPlayer = Quaternion.LookRotation(forward);//, m_Eye.transform.up);

            //Set the menu's position to the calculated position and rotation
            m_MenuManager.gameObject.transform.SetPositionAndRotation(pos, viewPlayer);

            //Open the menu to the selected screen
            m_MenuManager.OpenMenu(button.m_ScreenSelection);

            //Reset the time passed to 0
            m_TimePassed = 0f;
        }

        private void CloseMenu(VRMenuButton button)
        {
            Close();
        }

        private void Close()
        {
            //Close the menu
            m_MenuManager.CloseMenu();

            //Reset the time passed to 0
            m_TimePassed = 0f;

            //Disable the Close menu button, and enable the Open Menu button
            m_CloseButton.gameObject.SetActive(false);
            m_OpenButton.gameObject.SetActive(true);
        }

        public void PartyInvitation(string player)
        {
            UnityEngine.UI.Text[] temp = m_Invitation.GetComponentsInChildren<UnityEngine.UI.Text>(true);
            foreach (UnityEngine.UI.Text t in temp)
            {
                if (t.text.Contains("Party"))
                {
                    t.text = "Party Invitation from: " + player;
                    break;
                }
            }

            m_Invitation.SetActive(true);

            m_MenuManager.MenuSounds.PlayOneShot("NOTIFY", 0);
        }

        //public void ReconnectRequest()
        //{
        //    m_Reconnect.SetActive(true);
        //    m_MenuManager.MenuSounds.PlayOneShot("NOTIFY", 0);
        //}
    }
}