using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

using System;
using System.Collections;
using System.Collections.Generic;

using ACSL;
using ACSL.Interaction;
using ACSL.Audio;

using TMPro;

using Photon.Pun;

namespace ACSL.TEx
{
    public class NavigatorChair : MonoBehaviour
    {
        #region PUBLIC VARIABLES
        public SteeringObject m_Joystick;
        public Rigidbody m_RigidBody;

        public float m_MaxSpeed = 20f;
        public float m_MaxTurningAngle = 1f;
        public float m_MaxHeight = 30f;

        public Transform m_DockedLocation;

        public Transform m_Seat;
        public Transform m_FLThruster;
        public Transform m_FRThruster;
        public Transform m_RLThruster;
        public Transform m_RRThruster;

        public ParticleSystem m_FLParticles;
        public ParticleSystem m_FRParticles;
        public ParticleSystem m_RLParticles;
        public ParticleSystem m_RRParticles;

        public float m_MinThrusterPitch = 0.1f;
        public float m_MaxThrusterPitch = 0.3f;
        #endregion
        #region DEBUG
        public bool m_DebugMode;
        [Range(-1, 1)]
        public float m_Throttle;
        [Range(-1, 1)]
        public float m_Steering;
        [Range(0, 20)]
        public float m_TargetHeight;

        public TextMeshProUGUI m_HUD;
        #endregion
        #region  PRIVATE VARIABLES
        private Vignette m_Vignette;
        [SerializeField] private PostProcessProfile m_PlayerProfile;

#pragma warning disable 0414
        [Range(0.0f, 1.0f)]
        [SerializeField] private float m_VignetteMax = 1.0f;
#pragma warning restore 0414

        [SerializeField] private UnityEngine.UI.Image m_TargetHeight_UI;
        [SerializeField] private UnityEngine.UI.Image m_CurrentHeight_UI;

        private float m_ForceOfGravity;
        private float m_UpRatio;

        [PhotonData(typeof(float), PhotonData.PhotonDataType.Consistent)]
        private float m_CurrentTurning;
        [PhotonData(typeof(float), PhotonData.PhotonDataType.Consistent)]
        private float m_CurrentForward;

        private float m_CurrentHeight;

        private bool m_ParticlesActive;

        private bool m_ThrustersOn = false;
        private float m_PreviousUpRatio = 0.0f;
        private SoundObject[] m_ThrusterSounds = new SoundObject[4];
        #endregion
        #region  PUBLIC METHODS
        public void AdjustAltitude(float delta)
        {
            m_TargetHeight += delta;
            m_TargetHeight = Mathf.Clamp(m_TargetHeight, 0, m_MaxHeight);
        }

        public void SetThrusterOn(bool state, uint index)
        {
            if (state == m_ThrustersOn)
            {
                return;
            }

            m_ThrustersOn = state;

            if (m_ThrustersOn)
            {
                m_ThrusterSounds[index].Play("THRUSTER", 0);
            }
            else
            {
                m_ThrusterSounds[index].StopSounds();
            }
        }
        #endregion
        #region  PRIVATE METHODS
        private void Awake()
        {
            m_ThrusterSounds[0] = m_FLThruster.GetComponent<SoundObject>();
            m_ThrusterSounds[1] = m_FRThruster.GetComponent<SoundObject>();
            m_ThrusterSounds[2] = m_RLThruster.GetComponent<SoundObject>();
            m_ThrusterSounds[3] = m_RRThruster.GetComponent<SoundObject>();

            if (m_PlayerProfile != null)
                m_PlayerProfile.TryGetSettings<Vignette>(out m_Vignette);

            m_Vignette.active = true;
        }

        void Start()
        {
            //Calculate force of gravity for future calculations
            m_ForceOfGravity = m_RigidBody.mass * Physics.gravity.y;
            m_CurrentForward = 0f;
            m_CurrentTurning = 0f;

            m_ParticlesActive = false;
        }

        private void OnDisable()
        {
            m_TargetHeight = 0;
        }
        void Update()
        {
            AnimateThrusters();

            //If Thruster is On, Play relevant Sounds
            if (m_ThrustersOn)
            {
                float speedChange = m_UpRatio - m_PreviousUpRatio;
                float percChange = speedChange / m_UpRatio;

                for (int i = 0; i < 4; i++)
                {
                    if (m_ThrusterSounds[i] == null)
                        continue;


                    m_ThrusterSounds[i].AdjustPitch(Mathf.Abs(percChange), m_MinThrusterPitch, m_MaxThrusterPitch);
                }

                m_PreviousUpRatio = m_UpRatio;
            }

            if (m_HUD)
            {
                m_HUD.SetText("{0} m", m_CurrentHeight);
            }
            
            float max = Mathf.Max(m_CurrentHeight, m_TargetHeight, m_MaxHeight);

            if(m_CurrentHeight_UI)
                m_CurrentHeight_UI.fillAmount = m_CurrentHeight / max;

            if(m_TargetHeight_UI)
                m_TargetHeight_UI.fillAmount = m_TargetHeight / max;
        }

        private void FixedUpdate()
        {
            if (m_DebugMode)
                HandleFlight(m_Throttle, m_Steering, m_TargetHeight);

            else
                HandleFlight(m_Joystick.AngleRatios, m_TargetHeight);
        }

        private void HandleFlight(float forward, float turning, float height)
        {
            HandleFlight(new Vector2(turning, forward), height);
        }

        private void HandleFlight(Vector2 input, float height)
        {
            //Set rigid body to be vertical
            if (transform.up != Vector3.up)//&& input.magnitude == 0f)
            {
                float y = transform.rotation.eulerAngles.y;
                transform.rotation = Quaternion.Euler(0f, y, 0f);
            }

            //Save the magnitude of the input vector to account for values less than 1 after normalizing the input
            float magnitude = Mathf.Clamp(input.magnitude, 0f, 1f);

            //Normalize the imput to limit turning and acceleration values to a unit circle.
            input.Normalize();

            //Re-adjust normalized values to the correct length
            float forwardInput = input.y * magnitude;
            float turningInput = input.x * magnitude;

            //Cube input values to give a zone of finer control
            forwardInput *= forwardInput * forwardInput;
            turningInput *= turningInput * turningInput;


            //Calculate the upward force required to support the chair
            float upForce = m_ForceOfGravity * -1f;

            //Raycast down from the chair's position to find height above ground
            Ray ray = new Ray(transform.position, Vector3.down);
            RaycastHit hit;
            int layerMask =~ LayerMask.NameToLayer("NoPlayerCollision");

            //If the raycast hits something
            if (Physics.Raycast(ray, out hit ,m_MaxHeight * 1.5f, layerMask) && this.transform.position.y <= m_MaxHeight*1.2f)
            {
                //Set current height to be the distance of the hit away
                m_CurrentHeight = hit.distance;

                SetParticles(true);

                //Set the default upRatio to 0 so that the chair just hovers in place if it doesn't detect ground
                float upRatio = 0f;

                //Calculate the ratio above the ground if requested height is not 0
                if (height != 0)
                {
                    upRatio = Mathf.Clamp((height - m_CurrentHeight) / height, -0.75f, 1f);
                }

                //If the requested height is 0, set the ratio to full downwards
                else
                    upRatio = -0.75f;

                m_UpRatio = upRatio;
                //Adjust the upForce by 50% up or down depending on the up ratio
                upForce *= 1 + (1f * upRatio);

                if (hit.distance <= 0.65f && height == 0)
                    SetParticles(false);
            }
            //If the raycast doesn't hit something
            else
            {
                //Set current height to the absolute y value of the position, and calculate based on that
                m_CurrentHeight = m_RigidBody.transform.position.y;

                SetParticles(true);

                //Set the default upRatio to 0 so that the chair just hovers in place if it doesn't detect ground
                float upRatio = 0f;

                //Calculate the ratio above the ground if requested height is not 0
                if (height != 0f)
                {
                    upRatio = Mathf.Clamp((height - m_CurrentHeight) / height, -0.5f, 1f);
                }

                else if (height == 0f && m_CurrentHeight <= 0f)
                {
                    upRatio = 1f;
                }

                //If the requested height is 0, set the ratio to full downwards
                else
                {
                    upRatio = -0.75f;
                }

                m_UpRatio = upRatio;
                //Adjust the upForce by 50% up or down depending on the up ratio
                upForce *= 1 + (0.5f * upRatio);
            }

            //Get current forward velocity.
            Vector3 localVelocity = transform.InverseTransformDirection(m_RigidBody.velocity);
            float forwardVelocity = localVelocity.z;

            //Calculate the target forward velocity.
            float targetForwardVelocity = m_MaxSpeed * forwardInput;

            //Initialize the forward ratio to 0.
            float forwardAdjust = 0.5f;

            //Change forward over time to simulate physics
            if (targetForwardVelocity - m_CurrentForward > 0.001f)
                m_CurrentForward += m_MaxSpeed * forwardAdjust * Time.fixedDeltaTime;
            else if (targetForwardVelocity - m_CurrentForward < -0.001f)
                m_CurrentForward -= m_MaxSpeed * forwardAdjust * Time.fixedDeltaTime;
            else
                m_CurrentForward = targetForwardVelocity;

            m_CurrentForward = Mathf.Clamp(m_CurrentForward, -1 * m_MaxSpeed, m_MaxSpeed);

            //Calculate forward force based on the forward ratio
            float forwardForce = m_CurrentForward * m_RigidBody.mass;

            //Apply calcluated forward and upward forces
            m_RigidBody.AddForce(m_RigidBody.transform.forward * forwardForce, ForceMode.Force);
            m_RigidBody.AddForce(Vector3.up * upForce, ForceMode.Force);

            //Apply turning to the rigid body around the up (y-axis)
            float targetTurningSpeed = m_MaxTurningAngle * turningInput;
            float rotationalAcceleration = 0.5f;

            //Change turning over time to simulate physics
            if (targetTurningSpeed - m_CurrentTurning > 0.001f)
                m_CurrentTurning += m_MaxTurningAngle * rotationalAcceleration * Time.fixedDeltaTime;
            else if (targetTurningSpeed - m_CurrentTurning < -0.001f)
                m_CurrentTurning -= m_MaxTurningAngle * rotationalAcceleration * Time.fixedDeltaTime;
            else
                m_CurrentTurning = targetTurningSpeed;

            m_CurrentTurning = Mathf.Clamp(m_CurrentTurning, -m_MaxTurningAngle, m_MaxTurningAngle);
            transform.Rotate(Vector3.up, m_CurrentTurning);

            //Apply cosmetic leaning to make the experience more exciting
            //Calculate lean forward ratio clamped -1 to 1
            //Use that value to calculate lean side ratio -1 to 1
            float leanForward = Mathf.Clamp(forwardVelocity / m_MaxSpeed, -1f, 1f);
            float leanSide = Mathf.Clamp(targetTurningSpeed * leanForward, -1f, 1f);

            //Calculate angle based on lean ratios
            leanForward *= 15f * Mathf.Deg2Rad;
            leanSide *= 25f * Mathf.Deg2Rad;

            //Find the local right vector
            Vector3 localRight = transform.InverseTransformDirection(m_RigidBody.transform.right);
            Vector3 localForward = transform.InverseTransformDirection(m_RigidBody.transform.forward);

            //Rotate local up towards right then forwards
            Vector3 localUp = Vector3.RotateTowards(Vector3.up, localRight, leanSide, 10f);
            localUp = Vector3.RotateTowards(localUp, localForward, leanForward, 10f);

            m_Seat.localRotation = Quaternion.FromToRotation(Vector3.up, localUp);
            m_Seat.localRotation = Quaternion.FromToRotation(Vector3.up, localUp);
        }

        private void AnimateThrusters()
        {
            //Max angle from velocity
            float angle = 45f;
            //Max angle from turning
            float turningAngle = 35f;

            //Calculate forward and turning ratios
            float forwardRatio = m_CurrentForward / m_MaxSpeed;
            float turningRatio = m_CurrentTurning / m_MaxTurningAngle;

            //Calculate the angles for the right and left sides based on the above calculated ratios
            float rightAngles = angle * forwardRatio - turningAngle * turningRatio;
            float leftAngles = angle * forwardRatio + turningAngle * turningRatio;

            //Rotate the thrusters
            m_FLThruster.localRotation = Quaternion.Euler(leftAngles, 0f, 0f);
            m_FRThruster.localRotation = Quaternion.Euler(rightAngles, 0f, 0f);

            m_RLThruster.localRotation = Quaternion.Euler(leftAngles, 0f, 0f);
            m_RRThruster.localRotation = Quaternion.Euler(rightAngles, 0f, 0f);

            //Activate the particles
            if (m_ParticlesActive)
            {
                //Calculate the variable for the starting speed based on upwards force and arbitrary values determined by experimenting in the editor with what looks good
                float startSpeed = 0.3f + 0.05f * m_UpRatio + 0.2f * Mathf.Abs(forwardRatio) + 0.2f * Mathf.Abs(turningRatio);
                startSpeed *= Mathf.Clamp(m_CurrentHeight / 4f, 0f, 1f);

                var ParticleSystem = m_FLParticles.main;
                ParticleSystem.startSpeed = startSpeed;

                ParticleSystem = m_FRParticles.main;
                ParticleSystem.startSpeed = startSpeed;

                ParticleSystem = m_RLParticles.main;
                ParticleSystem.startSpeed = startSpeed;

                ParticleSystem = m_RRParticles.main;
                ParticleSystem.startSpeed = startSpeed;
            }
        }

        private void SetParticles(bool isActive)
        {
            //If particles are already set, don't do anything
            if (m_ParticlesActive == isActive)
                return;

            m_ParticlesActive = isActive;

            //If particles are supposed to be active
            if (m_ParticlesActive)
            {
                //Start the particle emitters
                m_FLParticles.Play();
                m_FRParticles.Play();
                m_RLParticles.Play();
                m_RRParticles.Play();

                //Turn Sounds On for Engine On
                for (uint i = 0; i < 4; i++)
                {
                    //SOUNDS OFF
                    //SetThrusterOn(true, i);
                }
            }
            else
            {
                //When stoped, let the particles time out naturally instead of clearing them immediately
                m_FLParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                m_FRParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                m_RLParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                m_RRParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);

                //Turn Sounds Off for Engine Off
                for (uint i = 0; i < 4; i++)
                {
                    SetThrusterOn(false, i);
                }
            }
        }
        private void SetVignette(float intensity)
        {
            if (m_Vignette)
            {
                float percentage = m_CurrentTurning / m_MaxTurningAngle;
                m_Vignette.intensity.value = Mathf.Lerp(m_Vignette.intensity.value, intensity, percentage);
            }
        }
        #endregion
    }
}
