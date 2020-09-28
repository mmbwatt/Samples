using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using ACSL;
using ACSL.ControllerInput;
using ACSL.Interaction;
using ACSL.Utility;

namespace ACSL
{
    namespace Menu
    {
        /*
         * Class to handle UI buttons in VR. Includes a selector that will fill up a progress bar
         * before sending off a message to activate the script listening to the button.
         * 
         * Created by Michael Watt
         * 
         */
        public class VRMenuButton : MonoBehaviour
        {
            #region DEBUG

            public void OnDrawGizmos()
            {
                OnDrawGizmosSelected();
            }
            public void OnDrawGizmosSelected()
            {
                if (GetComponent<Collider>() != null)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawWireCube(GetComponent<Collider>().bounds.center, GetComponent<Collider>().bounds.extents);
                }
            }

            public void OnMouseEnter()
            {
                Over();
            }
            public void OnMouseOver()
            {
                Ray mouseRay = Camera.main.ScreenPointToRay(Input.mousePosition);
                LayerMask mask = 1 << LayerMask.NameToLayer("Menu");
                RaycastHit hit;

                if (Physics.Raycast(mouseRay, out hit, 5f, mask))
                {
                    SetSelectorLocation(hit);
                }
            }
            public void OnMouseExit()
            {
                Out();
                StopSelecting(false);
            }
            public void OnMouseDown()
            {
                StartSelecting(false);
            }
            #endregion

            #region PUBLIC VARIABLES
            public bool m_IsRadial = true;
            public bool m_StartEnabled = true;
            public bool m_Hold = false;
            public GameObject m_SelectionFillImage;
            public ScreenTypes m_ScreenSelection;

            [Range(0, 2)]
            public float m_IncreaseSelectionDuration = 0f;

            //Different colours
            public Color m_ButtonColor;
            public Color m_DisabledColor;
            public Color m_HighlightColor;
            public Color m_SelectedColor;

            //Events that fire when buttons are pressed
            public event Action<VRMenuButton> OnButtonActivate;
            public event Action<VRMenuButton> OnButtonDeactivate;
            #endregion

            #region PRIVATE VARIABLES
            private Coroutine m_FillBarRoutine;
            private float m_SelectionDuration = 0.45f;//0.35f;
            private bool m_IsBarFilled;
            private bool m_IsFocused;
            private bool m_IsCurrentlyActive;
            private float m_Timer;

            //Button states
            private bool m_IsSelected;
            private bool m_IsEnabled;

            //Selector image
            private UnityEngine.UI.Image m_FillImage;
            private UnityEngine.UI.Image[] m_SpinningImages;

            private Vector3 m_HitPosition = Vector3.one;
            private GameObject m_MainCamera;

            //Menu Manager
            private MenuManager m_MenuManager;
            #endregion

            #region ACCESSORS
            public Vector3 HitPosition
            {
                get { return m_HitPosition; }
            }

            public bool IsSelected
            {
                get { return m_IsSelected; }
            }

            public bool IsFocused
            {
                get { return m_IsFocused; }
            }
            #endregion
            #region PUBLIC METHODS

            public void SetSelected(bool selected)
            {
                if (m_IsSelected == selected)
                    return;

                m_IsSelected = selected;
                if (m_IsSelected)
                {
                    GetComponent<CanvasRenderer>().SetColor(m_SelectedColor);
                }
                else
                {
                    GetComponent<CanvasRenderer>().SetColor(m_ButtonColor);
                }
            }

            public void SetEnabled(bool enabled)
            {
                if (m_IsEnabled == enabled)
                    return;

                m_IsEnabled = enabled;
                if (m_IsEnabled)
                {
                    GetComponent<CanvasRenderer>().SetColor(m_ButtonColor);
                }
                else
                {
                    if(m_IsSelected)
                        GetComponent<CanvasRenderer>().SetColor(m_SelectedColor);
                    else
                        GetComponent<CanvasRenderer>().SetColor(m_DisabledColor);
                }
            }

            public void SetOwner(MenuManager mm)
            {
                if (m_MenuManager != null)
                    return;

                m_MenuManager = mm;
            }

            /*
             * Called by a raycast from the right controller in the VR when the raycast intersects
             * with a collision box
             */
            public void Over()
            {
                if (!m_IsEnabled)
                    return;

                if (m_MenuManager != null)
                {
                    m_MenuManager.MenuSounds.PlayOneShot("HOVER", 1);
                }

                m_IsFocused = true;

                if (!m_IsSelected)
                    GetComponent<CanvasRenderer>().SetColor(m_HighlightColor);

                if (GameUtils.VRActive)
                {
                    GrabPoint.RightHand.m_HandUtility.GetComponent<HandUtility>().ToggleBool("Pointing", true);
                }

                //if (m_Image == null && m_IsRadial)
                //{
                //    Init();
                //}
                //Debug.Log("Hover Over Button");
            }

            public void Out()
            {
                if (!m_IsEnabled)
                    return;

                m_IsFocused = false;

                if (!m_IsSelected)
                    GetComponent<CanvasRenderer>().SetColor(m_ButtonColor);

                if (GameUtils.VRActive)
                {
                    GrabPoint.RightHand.m_HandUtility.GetComponent<HandUtility>().ToggleBool("Pointing", false);
                }

                // If the radial is active stop filling it and reset it's amount.
                if (m_IsCurrentlyActive)
                {
                    StopFill();
                    Hide();
                }
            }

            public void StartSelecting(bool isVR = true)
            {
                if (!m_IsEnabled && !m_IsSelected)
                    return;

                if (m_IsFocused)
                {
                    //Added for the potential to create a slider loading button instead of a radial one
                    if (m_IsRadial && isVR)
                    {
                        m_FillBarRoutine = StartCoroutine(FillSelectionRadial());
                    }
                    else
                    {
                        //!IsRadial is for List Buttons or Anything that can be DeSelected
                        if (m_IsSelected && OnButtonDeactivate != null)
                        {
                            SetSelected(false);
                            OnButtonDeactivate(this);
                            return;
                        }

                        if (OnButtonActivate != null)
                        {
                            SetSelected(true);
                            OnButtonActivate(this);
                        }

                    }
                }
            }
            public void Show()
            {
                //Debug.Log("Show Selection Radial");
                if (m_FillImage != null)
                {
                    m_FillImage.gameObject.SetActive(true);
                }

                m_IsCurrentlyActive = true;
            }

            public void Hide()
            {
                //Debug.Log("Hide Selection Radial");

                if (m_FillImage != null)
                {
                    m_FillImage.gameObject.SetActive(false);

                    // This effectively resets the radial for when it's shown again.
                    m_FillImage.fillAmount = 0f;
                }

                m_IsCurrentlyActive = false;



            }
            public IEnumerator WaitForSelectionRadialToFill()
            {
                //Debug.Log("Waiting For Radial");
                // Set the radial to not filled in order to wait for it.
                m_IsBarFilled = false;

                // Make sure the radial is visible and usable.
                Show();

                // Check every frame if the radial is filled.
                while (!m_IsBarFilled)
                {
                    yield return null;
                }

                // Once it's been used make the radial invisible.
                Hide();
            }

            public void StopSelecting(bool isVR = true)
            {
                if (m_IsFocused)
                {
                    StopFill();
                }

                if (m_Hold)
                {
                    if (m_IsSelected && OnButtonDeactivate != null)
                    {
                        SetSelected(false);
                        OnButtonDeactivate(this);
                    }
                }

                if (!isVR && m_IsRadial)
                {
                    SetSelected(false);
                }
            }

            public void SetSelectorLocation(RaycastHit aHit)
            {
                m_HitPosition = aHit.point;

                //If there's an image
                if (m_SelectionFillImage != null)
                {
                    //Set the centre of the image to the position of the raycast hit, moved up by a little to avoid z-fighting.
                    m_SelectionFillImage.transform.position = Vector3.MoveTowards(
                        aHit.point, aHit.transform.forward, -0.01f);

                    //Set the direction of the image to be the "up" normal of the surface hit.
                    m_SelectionFillImage.transform.LookAt(
                        m_SelectionFillImage.transform.position + aHit.normal,
                        this.transform.rotation * Vector3.up);
                }
            }

            #endregion
            #region PRIVATE METHODS
            #region MONOBEHAVIOURS
            private void OnTriggerEnter(Collider other)
            {
                //ignore capsule collider for the grab point.
                if (other.GetComponent<GrabPoint>())
                    Physics.IgnoreCollision(GetComponent<Collider>(), other);
            }
            private void Start()
            {
                Init();
            }
            private void OnEnable()
            {
                if (m_IsSelected)
                    GetComponent<CanvasRenderer>().SetColor(m_SelectedColor);
                if (!m_IsEnabled)
                    GetComponent<CanvasRenderer>().SetColor(m_DisabledColor);
            }
            #endregion
            private void StopFill()
            {
                if (m_FillBarRoutine != null)
                {
                    StopCoroutine(m_FillBarRoutine);
                    Hide();
                }
            }

            private void Init()
            {
                //Make sure the selection image is initialized at 0
                if (m_SelectionFillImage != null)
                {
                    m_FillImage = m_SelectionFillImage.GetComponent<UnityEngine.UI.Image>();
                    m_FillImage.fillAmount = 0f;

                    m_SpinningImages = m_SelectionFillImage.GetComponentsInChildren<UnityEngine.UI.Image>();
                }

                SetSelected(false);
                SetEnabled(m_StartEnabled);
            }

            private IEnumerator FillSelectionRadial()
            {
                // At the start of the coroutine, the bar is not filled.
                m_IsBarFilled = false;

                // Create a timer and reset the fill amount.
                float timer = 0f;
                m_FillImage.fillAmount = 0f;

                // This loop is executed once per frame until the timer exceeds the duration.
                while (timer < m_SelectionDuration)
                {
                    // The image's fill amount requires a value from 0 to 1 so we normalise the time.
                    m_FillImage.fillAmount = timer / m_SelectionDuration;

                    int i = 1;
                    foreach(UnityEngine.UI.Image image in m_SpinningImages)
                    {
                        Vector3 rotation = new Vector3(0, 0, 30f * i * Time.deltaTime);
                        if (i%2 == 0)
                        {
                            rotation *= -1f;
                        }

                        image.transform.Rotate(rotation);
                        i++;
                    }

                    // Increase the timer by the time between frames and wait for the next frame.
                    timer += Time.deltaTime;

                    //Return control to other processes until next frame
                    yield return null;
                }

                // When the loop is finished set the fill amount to be full.
                m_FillImage.fillAmount = 1f;

                // Turn off the radial so it can only be used once.
                m_IsCurrentlyActive = false;

                // The radial is now filled so the coroutine waiting for it can continue.
                m_IsBarFilled = true;

                // If there is anything subscribed to OnButtonActivate call it.

                if (OnButtonActivate != null)
                    OnButtonActivate(this);
            }
            #endregion
        }
    }
}
