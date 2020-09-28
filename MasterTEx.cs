using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEditor;

using TMPro;

using Photon.Pun;
using Photon.Realtime;

using ACSL;
using ACSL.Game;
using ACSL.Interaction;
using ACSL.Utility;
using ACSL.Audio;

namespace ACSL.TEx
{
    public class MasterTEx : MonoBehaviour
    {
        #region PUBLIC VARIABLES
        public Transform m_CenterOfMass;
        public AnimationCurve m_TorqueCurve;
        public bool m_TerrainTest;
        public TextMeshProUGUI m_HUD;

        #region ENGINES
        //Unloaded RPM, max is around 16000
        public float m_MaxRPM = 16000f;
        //Power in units/s
        //If unit = m, 30m/s = 108 km/h
        public float m_MaxSpeed = 15f;
        //Torque in Nm
        public float m_StallTorque = 3000f;
        //Brake Force in Nm
        public float m_MaxBrakeForce = 5000f;
        #endregion

        #region CONTROLLERS
        public SteeringObject m_Throttle;
        public SteeringObject m_Joystick;

        public bool m_Debug;
        [Range(-1, 1)]
        public float m_DebugThrottle;
        [Range(-1, 1)]
        public float m_DebugSteering;
        #endregion
        #region WHEEL
        //Steering Angle
        [Range(0, 29)]
        public float m_MaxSteeringAngle;

        //Steering Speed
        public float m_SteeringSpeed;

        //Steering Type
        public bool m_FourWheelSteering;

        //wheel rotation 
        public GameObject m_LFW;
        public GameObject m_LMW;
        public GameObject m_LBW;
        public GameObject m_RFW;
        public GameObject m_RMW;
        public GameObject m_RBW;

        [Space(10)]

        //rotation on axle
        public GameObject m_LFWRotation;
        public GameObject m_LMWRotation;
        public GameObject m_LBWRotation;
        public GameObject m_RFWRotation;
        public GameObject m_RMWRotation;
        public GameObject m_RBWRotation;

        [Space(10)]

        //colliders
        public WheelCollider m_LFWCollider;
        public WheelCollider m_LMWCollider;
        public WheelCollider m_LBWCollider;
        public WheelCollider m_RFWCollider;
        public WheelCollider m_RMWCollider;
        public WheelCollider m_RBWCollider;

        //Raycast start locations for terrain checking
        public Transform m_LFRaycast;
        public Transform m_LBRaycast;
        public Transform m_RFRaycast;
        public Transform m_RBRaycast;
        #endregion
        #endregion
        #region  PRIVATE VARIABLES
        [SerializeField]private UnityEngine.UI.Image m_ForwardBars;
        [SerializeField]private UnityEngine.UI.Image m_BackwardsBars;
        private float m_MaxSpeedForUI;
        private Vignette m_Vignette;
        [SerializeField] private PostProcessProfile m_PlayerProfile;
        [Range(0.0f, 1.0f)]
        [SerializeField] private float m_VignetteMax = 1.0f;

        private PhotonView m_PV;
        private Rigidbody m_RB;

        //Sound variables
        private bool m_MotorOn = false;
        private float m_MinMotorPitch = 0.8f;
        private float m_MaxMotorPitch = 1.2f;
        private float m_PreviousSpeed = 0.0f;
        private SoundObject m_MotorSound;
        private float m_MaxPercChanceForWheelSound = 0.5f;
        private Dictionary<WheelCollider, SoundObject> m_WheelSounds = new Dictionary<WheelCollider, SoundObject>();

        //Current Top Speed, different from absolute MAX SPEED
        private float m_TopSpeed;

        //Variable for total mass, if we want to give different game objects different masses
        private float m_TotalMass;

        //Variable to track current speed
        private float m_CurrentSpeed;

        //Engage emergency breaks?
        private bool m_EmergencyStop;

        //Animation Variables to Sync
        [PhotonData(typeof(float), PhotonData.PhotonDataType.Consistent)]
        private float m_LFW_RPM_Anim;
        [PhotonData(typeof(float), PhotonData.PhotonDataType.Consistent)]
        private float m_LMW_RPM_Anim;
        [PhotonData(typeof(float), PhotonData.PhotonDataType.Consistent)]
        private float m_LBW_RPM_Anim;
        [PhotonData(typeof(float), PhotonData.PhotonDataType.Consistent)]
        private float m_RFW_RPM_Anim;
        [PhotonData(typeof(float), PhotonData.PhotonDataType.Consistent)]
        private float m_RMW_RPM_Anim;
        [PhotonData(typeof(float), PhotonData.PhotonDataType.Consistent)]
        private float m_RBW_RPM_Anim;

        [PhotonData(typeof(float), PhotonData.PhotonDataType.Consistent)]
        private float m_LFW_Steer_Anim;
        [PhotonData(typeof(float), PhotonData.PhotonDataType.Consistent)]
        private float m_LBW_Steer_Anim;
        [PhotonData(typeof(float), PhotonData.PhotonDataType.Consistent)]
        private float m_RFW_Steer_Anim;
        [PhotonData(typeof(float), PhotonData.PhotonDataType.Consistent)]
        private float m_RBW_Steer_Anim;

        #endregion
        #region ACCESSORS
        public float CurrentSpeed
        {
            get { return m_CurrentSpeed; }
        }
        #endregion
        #region PUBLIC METHODS
        public void SetMotorOn(bool state)
        {
            if (state == m_MotorOn)
            {
                return;
            }

            m_MotorOn = state;

            if (m_MotorOn)
            {
                m_MotorSound.Play("MOTOR", 0);
                //m_MotorSound.PlayOneShot("REVUP", 0);
            }
            else
            {
                //m_MotorSound.PlayOneShot("REVDOWN", 0);
                m_MotorSound.StopSounds();
            }
        }
        #endregion

        #region  PRIVATE METHODS

        private void SetCenterOfMass(Vector3 center)
        {
            m_RB = GetComponent<Rigidbody>();

            if (m_CenterOfMass == null)
                Debug.Log("No CoM specified. Using regular CoM.");
            else
                m_RB.centerOfMass = center;
        }

        private void SetTorque(float acceleration, float limitSpeed, WheelCollider wheel)
        {
            //Set up default values for calculations
            float rpm = 0;

            float brakeForce = 0f;

            //Clear brake force before applying acceleration
            wheel.brakeTorque = 0f;

            //Get RPM of the wheel
            rpm = float.IsNaN(wheel.rpm) ? 0 : wheel.rpm;

            //Get absolute value of RPM to calculate ratio
            rpm = Mathf.Abs(rpm);

            //Limit the rpm to Max RPM
            rpm = Mathf.Min(rpm, m_MaxRPM);

            //Calculate ratio to look up on Animation Curve
            float RPM_Ratio = rpm / m_MaxRPM;

            //Limit RPM to throttle position
            RPM_Ratio = Mathf.Min(Mathf.Abs(acceleration), RPM_Ratio);

            //Calculate torque to apply based on throttle and torque curve
            float torque = m_TorqueCurve.Evaluate(RPM_Ratio) * m_StallTorque;

            //If the throttle is less than 0, go backwards
            if (acceleration < 0)
                torque *= -1;

            //Calculate linear wheel speed
            //RPM is already absolute value
            float radPerSecond = rpm * 9.5493f; //Magic number to convert RPM to rad/s
            float linearSpeed = radPerSecond * wheel.radius;

            //Handle traction control
            //If you're trying to accelerate
            if (Mathf.Abs(acceleration) >= 0.5f)
            {
                //If the linear speed of the wheel is much greater than the current speed
                if (linearSpeed > m_CurrentSpeed * 500f)
                {
                    //Apply a brake force
                    float difference = linearSpeed - m_CurrentSpeed;
                    brakeForce += difference; //Assume braking happens over 1s
                }
            }

            //Limit speed by not allowing acceleration beyond throttle
            float targetSpeed = Mathf.Abs(m_TopSpeed * limitSpeed);
            if (m_CurrentSpeed > targetSpeed)
            {
                torque = 0;
                if (targetSpeed >= 1)
                    brakeForce += ((m_CurrentSpeed - targetSpeed) / targetSpeed) * m_MaxBrakeForce;
                else
                    brakeForce += (2 * m_StallTorque);
            }

            wheel.motorTorque = torque;

            ApplyBrake(wheel, brakeForce);
        }

        private void ResetBrakeForces()
        {
            m_LFWCollider.brakeTorque = 0;
            m_LMWCollider.brakeTorque = 0;
            m_LBWCollider.brakeTorque = 0;
            m_RFWCollider.brakeTorque = 0;
            m_RMWCollider.brakeTorque = 0;
            m_RBWCollider.brakeTorque = 0;
        }

        private void HandleAcceleration(float throttle)
        {
            //Adjust throttle settings to give you fine control until the end
            //Using a cuibic function to model acceleration curve
            float acceleration = throttle * throttle * throttle;

            //Apply throttle to each wheel individually
            //Handle traction control inside each wheel
            SetTorque(acceleration, throttle, m_LFWCollider);
            SetTorque(acceleration, throttle, m_LMWCollider);
            SetTorque(acceleration, throttle, m_LBWCollider);
            SetTorque(acceleration, throttle, m_RFWCollider);
            SetTorque(acceleration, throttle, m_RMWCollider);
            SetTorque(acceleration, throttle, m_RBWCollider);

            //Set the differential so that the TEx can turn
            Differential(m_LFWCollider, m_RFWCollider);
            Differential(m_LMWCollider, m_RMWCollider);
            Differential(m_LBWCollider, m_RBWCollider);
        }

        private void Differential(WheelCollider wheel1, WheelCollider wheel2)
        {
            //Apply torque based on the relative speed of the wheels
            float totalTorque = wheel1.motorTorque + wheel2.motorTorque;

            float totalRPM = (float.IsNaN(wheel1.rpm) ? 0 : wheel1.rpm) + (float.IsNaN(wheel2.rpm) ? 0 : wheel2.rpm);
            float wheel1Ratio;
            float wheel2Ratio;

            if (totalRPM <= 2f)
            {
                wheel1Ratio = 0.5f;
                wheel2Ratio = 0.5f;
            }

            else
            {
                wheel1Ratio = wheel1.rpm / totalRPM;
                wheel2Ratio = wheel2.rpm / totalRPM;
            }

            //Simplified differential calculation
            wheel1.motorTorque = totalTorque * wheel1Ratio;
            wheel2.motorTorque = totalTorque * wheel2Ratio;
        }

        private void HandleSteering(float direction)
        {
            float adjustedDirection = direction * direction * direction;

            //Drive like a car, front two wheels steer
            SteerWheel(m_LFWCollider, adjustedDirection);
            SteerWheel(m_RFWCollider, adjustedDirection);

            //Tight Turn, 4 wheel turn (front two and back two wheels)
            if (m_FourWheelSteering)
            {
                SteerWheel(m_LBWCollider, -adjustedDirection);
                SteerWheel(m_RBWCollider, -adjustedDirection);
            }

            //TODO: Crawler?
        }

        private void ResetSteering()
        {
            m_LFWCollider.steerAngle = 0f;
            m_LMWCollider.steerAngle = 0f;
            m_LBWCollider.steerAngle = 0f;
            m_RFWCollider.steerAngle = 0f;
            m_RMWCollider.steerAngle = 0f;
            m_RBWCollider.steerAngle = 0f;
        }

        private void SteerWheel(WheelCollider wheel, float targetRatio)
        {
            bool isNegative = false;
            float steerAngle = 0f;

            if (targetRatio < 0)
            {
                isNegative = true;
                targetRatio *= -1f;
            }

            //Lerp from current angle to target angle to smooth out steering
            float targetAngle = Mathf.Lerp(0, m_MaxSteeringAngle, targetRatio);

            if (isNegative)
            {
                targetAngle *= -1f;
            }

            float currentAngle = wheel.steerAngle;
            steerAngle = targetAngle;

            if (targetAngle - currentAngle > 0.001f)
                currentAngle += m_SteeringSpeed * targetRatio * Time.fixedDeltaTime;
            else if (targetAngle - currentAngle < -0.001f)
                currentAngle -= m_SteeringSpeed * targetRatio * Time.fixedDeltaTime;
            else
                currentAngle = targetAngle;

            //Compare current steering angle
            wheel.steerAngle = steerAngle;
        }

        private void FullStop()
        {
            m_LFWCollider.motorTorque = 0f;
            m_LMWCollider.motorTorque = 0f;
            m_LBWCollider.motorTorque = 0f;
            m_RFWCollider.motorTorque = 0f;
            m_RMWCollider.motorTorque = 0f;
            m_RBWCollider.motorTorque = 0f;

            StopWheel(m_LFWCollider);
            StopWheel(m_LMWCollider);
            StopWheel(m_LBWCollider);
            StopWheel(m_RFWCollider);
            StopWheel(m_RMWCollider);
            StopWheel(m_RBWCollider);
        }

        private void StopWheel(WheelCollider wheel)
        {
            ApplyBrake(wheel, 1.5f * m_MaxBrakeForce);
        }

        //Anti-lock Braking System
        private void ApplyBrake(WheelCollider wheel, float brakeTorque)
        {
            //If wheels are not spinning, but you're still moving forward, don't apply brakes
            if (wheel.rpm == 0 && m_CurrentSpeed >= 5f)
            {
                wheel.brakeTorque = 0;
            }
            //Otherwise, apply brakes
            else
            {
                wheel.brakeTorque = brakeTorque;
            }
        }

        private void SyncAnimations()
        {
            m_LFW_RPM_Anim = m_LFWCollider.rpm;
            m_LMW_RPM_Anim = m_LMWCollider.rpm;
            m_LBW_RPM_Anim = m_LBWCollider.rpm;
            m_RFW_RPM_Anim = m_RFWCollider.rpm;
            m_RMW_RPM_Anim = m_RMWCollider.rpm;
            m_RBW_RPM_Anim = m_RBWCollider.rpm;

            m_LFW_Steer_Anim = m_LFWCollider.steerAngle;
            m_LBW_Steer_Anim = m_LBWCollider.steerAngle;
            m_RFW_Steer_Anim = m_RFWCollider.steerAngle;
            m_RBW_Steer_Anim = m_RBWCollider.steerAngle;
        }

        private void WheelAnimations()
        {
            //Steering Animations
            //Rotations around Z axis because of wonky rotations from exported model
            m_RFWRotation.transform.localRotation = Quaternion.Euler(0f, 0f, m_RFW_Steer_Anim);//, 0f);
            //m_RMWRotation.transform.localRotation = Quaternion.Euler(0f, 0f, m_RMWCollider.steerAngle);//, 0f);
            m_RBWRotation.transform.localRotation = Quaternion.Euler(0f, 0f, m_RBW_Steer_Anim);//, 0f);
            m_LFWRotation.transform.localRotation = Quaternion.Euler(0f, 0f, -m_LFW_Steer_Anim);//, 0f);
            //m_LMWRotation.transform.localRotation = Quaternion.Euler(0f, 0f, -m_LMWCollider.steerAngle);//, 0f);
            m_LBWRotation.transform.localRotation = Quaternion.Euler(0f, 0f, -m_LBW_Steer_Anim);//, 0f);

            //Wheel rotation animations
            //Magic number to convert RPM to deg/s is 6
            m_RFW.transform.localRotation *= Quaternion.Euler(m_RFW_RPM_Anim * -6f * Time.deltaTime, 0f, 0f);
            m_RMW.transform.localRotation *= Quaternion.Euler(m_RMW_RPM_Anim * -6f * Time.deltaTime, 0f, 0f);
            m_RBW.transform.localRotation *= Quaternion.Euler(m_RBW_RPM_Anim * -6f * Time.deltaTime, 0f, 0f);
            m_LFW.transform.localRotation *= Quaternion.Euler(m_LFW_RPM_Anim * -6f * Time.deltaTime, 0f, 0f);
            m_LMW.transform.localRotation *= Quaternion.Euler(m_LMW_RPM_Anim * -6f * Time.deltaTime, 0f, 0f);
            m_LBW.transform.localRotation *= Quaternion.Euler(m_LBW_RPM_Anim * -6f * Time.deltaTime, 0f, 0f);
        }

        //Play Sounds for Individual Wheels On Chance
        private void EnumerateWheelSounds()
        {
            float wheelSpeed = 0.0f;

            foreach (WheelCollider wc in m_WheelSounds.Keys)
            {
                wheelSpeed = wc.rpm / m_MaxRPM;
                float chanceForSound = Mathf.Clamp(wheelSpeed, 0.0f, m_MaxPercChanceForWheelSound);
                int randNum = UnityEngine.Random.Range(0, 100 - (100 * (int)chanceForSound));

                if (randNum == 6)
                {
                    m_WheelSounds[wc].Play("GRAVEL_COLLISION");
                }
            }
        }

        private void CheckTerrain()
        {
            //Reset the emergency stop flag to false
            m_EmergencyStop = false;

            //Calculate the radians for the check angles
            float warningAngle = 15f * Mathf.Deg2Rad;
            float stopAngle = 65f * Mathf.Deg2Rad;

            //Set the length of the check raycast.  Approximated, can use commented out code for exactly accurate values for the ground at 3 units below the chasis 
            float warningDistance = 3f / Mathf.Sin(warningAngle);
            float stopDistance = 3f / Mathf.Sin(stopAngle);

            //Calculate the front steering angle's radians based off the wheel collider's steering angle
            float frontSteerAngle = m_LFWCollider.steerAngle * Mathf.Deg2Rad;

            //Save TEx's local forward, down and right
            Vector3 localForward = transform.forward;   //transform.TransformDirection(Vector3.forward);
            Vector3 localDown = -transform.up;          //transform.TransformDirection(Vector3.down);
            Vector3 localRight = transform.right;       //transform.TransformDirection(Vector3.right);

            //Calculate the angle between the front of the TEx and the horizon line
            float angleBelowHorizon = Vector3.SignedAngle(Vector3.up, transform.forward, transform.right) - 90f; //- values are above horizon

            //If the driver is trying to drive forwards and the TEx is pointing at less than 25 degrees above the horizon
            if (m_Throttle.AngleRatios.y > 0f && angleBelowHorizon >= -25f)
            {
                //Rotate the warning vector from forward, down by warninAngle radians
                //Then rotate vector left or right by frontSteerAngle radians
                //Make sure direction vector is normalized
                Vector3 warningRotation = Vector3.RotateTowards(localForward, localDown, warningAngle, 1);
                warningRotation = Vector3.RotateTowards(warningRotation, localRight, frontSteerAngle, 1);
                warningRotation.Normalize();

                //Rotate the stop vector from forward, down by stopAngle radians
                //Then rotate vector left or right by frontSteerAngle radians
                //Make sure direction vector is normalized
                Vector3 stopRotation = Vector3.RotateTowards(localForward, localDown, stopAngle, 1);
                stopRotation = Vector3.RotateTowards(stopRotation, localRight, frontSteerAngle, 1);
                stopRotation.Normalize();

                //Raycast along the warning Rotation to see if there is Ground at the location being driven to
                Ray lf_warning = new Ray(m_LFRaycast.position, warningRotation);
                Ray rf_warning = new Ray(m_RFRaycast.position, warningRotation);

                RaycastHit hit;

                //Reset TopSpeed to MaxSpeed
                m_TopSpeed = m_MaxSpeed;

                //If the raycast hits something then there is ground in front of the left wheel
                if (Physics.Raycast(lf_warning, out hit, warningDistance))
                {
                    //Show that it hits in the editor by drawing a debug ray
                    Debug.DrawRay(m_LFRaycast.position, warningRotation * hit.distance, Color.blue, 1f);

                    //Potential to add code here for UI Topographical view around hit location
                }
                else
                {
                    //No ground has been detected at the warning distance, reduce top speed to 1/10th of max
                    m_TopSpeed = m_MaxSpeed / 10f;

                    Ray lf_stop = new Ray(m_LFRaycast.position, stopRotation);

                    //If the emergency stop hits something, only limit speed by half
                    if (Physics.Raycast(lf_stop, out hit, stopDistance))
                    {
                        //Show that it hits in the editor by drawing a debug ray
                        Debug.DrawRay(m_LFRaycast.position, stopRotation * hit.distance, Color.red, 1f);
                        //Potential to add further functionality for on hit here
                    }

                    //Otherwise set top speed to 0, apply the emergency brakes and stop checking
                    else
                    {
                        m_TopSpeed = 0f;
                        m_EmergencyStop = true;
                        return;
                    }
                }

                //If the raycast hits something then there is ground in front of the right wheel
                if (Physics.Raycast(rf_warning, out hit, warningDistance))
                {
                    //Show that it hits in the editor by drawing a debug ray
                    Debug.DrawRay(m_RFRaycast.position, warningRotation * hit.distance, Color.green, 1f);

                    //Potential to add code here for UI Topographical view around hit location
                }
                else
                {
                    //No ground has been detected at the warning distance, reduce top speed to 1/10th of max
                    m_TopSpeed = m_MaxSpeed / 10f;

                    Ray rf_stop = new Ray(m_RFRaycast.position, stopRotation);

                    if (Physics.Raycast(rf_stop, out hit, stopDistance))
                    {
                        //Show that it hits in the editor by drawing a debug ray
                        Debug.DrawRay(m_RFRaycast.position, stopRotation * hit.distance, Color.yellow, 1f);
                        //Potential to add code here for UI Topographical view around hit location
                    }
                    else
                    {
                        m_TopSpeed = 0f;
                        m_EmergencyStop = true;
                        return;
                    }
                }
            }
            //If the driver is trying to drive backwards and the TEx is pointing at less than 25 degrees below the horizon
            else if (m_Throttle.AngleRatios.y < 0f && angleBelowHorizon <= 25f)
            {
                //Rotate the warning vector from backward, down by warningAngle radians
                //Then rotate vector left or right by frontSteerAngle radians
                //Make sure direction vector is normalized
                Vector3 warningRotation = Vector3.RotateTowards(-localForward, localDown, warningAngle, 1);
                warningRotation = Vector3.RotateTowards(warningRotation, localRight, frontSteerAngle, 1);
                warningRotation.Normalize();

                //Rotate the stop vector from backward, down by stopAngle radians
                //Then rotate vector left or right by frontSteerAngle radians
                //Make sure direction vector is normalized
                Vector3 stopRotation = Vector3.RotateTowards(-localForward, localDown, stopAngle, 1);
                stopRotation = Vector3.RotateTowards(stopRotation, localRight, frontSteerAngle, 1);
                stopRotation.Normalize();

                //Raycast from the left back and right back positions to check for ground
                Ray lb_warning = new Ray(m_LBRaycast.position, warningRotation);
                Ray rb_warning = new Ray(m_RBRaycast.position, warningRotation);

                RaycastHit hit;

                //Reset the top speed to be max speed
                m_TopSpeed = m_MaxSpeed;

                //If the raycast hits something then there is ground behind the left wheels
                if (Physics.Raycast(lb_warning, out hit, warningDistance))
                {
                    //Debug ray if hit
                    Debug.DrawRay(m_LBRaycast.position, warningRotation * hit.distance, Color.blue, 1f);
                    //Potential to add further functionality for on hit here
                }
                else
                {
                    //No ground has been detected at the warning distance, reduce top speed to 1/10th of max
                    m_TopSpeed = m_MaxSpeed / 10f;

                    Ray lb_stop = new Ray(m_LBRaycast.position, stopRotation);

                    //If the emergency stop hits something, only limit speed by half
                    if (Physics.Raycast(lb_stop, out hit, stopDistance))
                    {
                        Debug.DrawRay(m_LBRaycast.position, stopRotation * hit.distance, Color.red, 1f);
                        //Potential to add further functionality for on hit here
                    }

                    //Otherwise set speed to 0, set emergency stop, and stop checking
                    else
                    {
                        m_TopSpeed = 0f;
                        m_EmergencyStop = true;
                        return;
                    }
                }

                //If the raycast hits something then there is ground behind the right wheels
                if (Physics.Raycast(rb_warning, out hit, warningDistance))
                {
                    Debug.DrawRay(m_RBRaycast.position, warningRotation * hit.distance, Color.green, 1f);
                    //Potential to add further functionality for on hit here
                }
                else
                {
                    //No ground has been detected at the warning distance, reduce top speed to 1/10th of max
                    m_TopSpeed = m_MaxSpeed / 10f;

                    Ray rb_stop = new Ray(m_RBRaycast.position, stopRotation);

                    //If the emergency stop hits something, only limit speed by half
                    if (Physics.Raycast(rb_stop, out hit, stopDistance))
                    {
                        Debug.DrawRay(m_RBRaycast.position, stopRotation * hit.distance, Color.yellow, 1f);
                        //Potential to add further functionality for on hit here
                    }

                    //Otherwise set speed to 0, set emergency stop, and stop checking
                    else
                    {
                        m_TopSpeed = 0f;
                        m_EmergencyStop = true;
                        return;
                    }
                }
            }
        }

        private void SetVignette(float intensity)
        {
            if (m_Vignette != null )
            {
                intensity = Mathf.Clamp(intensity, 0.0f, m_VignetteMax);
                m_Vignette.intensity.value = Mathf.Lerp(m_Vignette.intensity.value, intensity, Time.deltaTime);
            }
        }
        #endregion

        private void Awake()
        {
            SetCenterOfMass(m_CenterOfMass.localPosition);

            //Motor Sound
            m_MotorSound = m_CenterOfMass.gameObject.GetComponent<SoundObject>();

            //Wheel Sounds
            m_WheelSounds[m_LFWCollider] = m_LFW.GetComponent<SoundObject>();
            m_WheelSounds[m_LMWCollider] = m_LMW.GetComponent<SoundObject>();
            m_WheelSounds[m_LBWCollider] = m_LBW.GetComponent<SoundObject>();
            m_WheelSounds[m_RFWCollider] = m_RFW.GetComponent<SoundObject>();
            m_WheelSounds[m_RMWCollider] = m_RMW.GetComponent<SoundObject>();
            m_WheelSounds[m_RBWCollider] = m_RBW.GetComponent<SoundObject>();

            //If you want to make sure there are different characteristics for 
            Rigidbody[] rb = GetComponentsInChildren<Rigidbody>();
            foreach (Rigidbody r in rb)
            {
                m_TotalMass += r.mass;
            }

            FullStop();

            GameManager.Instance.OnSceneLoaded += Init;

            m_TopSpeed = m_MaxSpeed;
            m_PV = this.GetComponent<PhotonView>();

            if (m_PlayerProfile != null)
                m_PlayerProfile.TryGetSettings<Vignette>(out m_Vignette);

            m_Vignette.active = true;

            m_MaxSpeedForUI = m_MaxSpeed;
        }

        private void Start()
        {

        }

        private void Update()
        {
            //If I am driving the TEx
            if (m_PV.IsMine)
            {
                //I set the animation values
                SyncAnimations();
            }

            //Animate the wheels
            WheelAnimations();

            //If Motor is On, Play relevant Sounds
            if (m_MotorOn)
            {
                float speedChange = m_CurrentSpeed - m_PreviousSpeed;
                float percChange = speedChange / m_CurrentSpeed;

                m_MotorSound.AdjustPitch(percChange, m_MinMotorPitch, m_MaxMotorPitch);

                m_PreviousSpeed = m_CurrentSpeed;

                //Try play sound for each Wheel
                EnumerateWheelSounds();
            }

            //Display current speed in km/h
            m_HUD.text = Mathf.Round(m_CurrentSpeed * 3.6f) + " KM/H"; // 1 m/s is 3.6 km/h
            if(Vector3.Dot(m_RB.velocity, this.transform.forward) >= 0f)
            {
                float p = m_CurrentSpeed / m_MaxSpeedForUI;
                m_BackwardsBars.fillAmount = 0f;
                m_ForwardBars.fillAmount = p;
            }
            else
            {
                float p = m_CurrentSpeed / m_MaxSpeedForUI;
                m_BackwardsBars.fillAmount = p;
                m_ForwardBars.fillAmount = 0f;
            }
        }

        private void FixedUpdate()
        {
            m_CurrentSpeed = m_RB.velocity.magnitude;
            m_TopSpeed = m_MaxSpeed;

            if (m_TerrainTest)
            {
                CheckTerrain();
            }

            if (m_Debug)
            {
                //SetMotorOn(true);
                HandleAcceleration(m_DebugThrottle);
                HandleSteering(m_DebugSteering);
            }
            else
            {
                float forwardInput = 0f;
                float turningInput = 0f;
                if (m_Joystick && m_Throttle)
                {
                    Vector2 input = new Vector2(m_Joystick.AngleRatios.x, m_Throttle.AngleRatios.y);
                    //Save the magnitude of the input vector to account for values less than 1 after normalizing the input
                    float magnitude = Mathf.Clamp(input.magnitude, 0f, 1f);

                    //Normalize the imput to limit turning and acceleration values to a unit circle.
                    input.Normalize();

                    //Re-adjust normalized values to the correct length
                    forwardInput = input.x * magnitude;
                    turningInput = input.y * magnitude;
                
                    if (m_Joystick.Grabbed)
                        HandleSteering(forwardInput);
                    else
                        ResetSteering();

                    if (m_Throttle.Grabbed)
                        HandleAcceleration(turningInput);
                    else
                        FullStop();
                }
            }

            if (m_EmergencyStop)
                FullStop();
        }

        private void Init()
        {
            //Sub to OnOwnershipChange
            GameManager.Instance.NetworkManager.PhotonLink.ViewOwnershipChanged += OnOwnershipChanged;

            GameManager.Instance.NetworkManager.PhotonLink.JoinedRoom += AdjustForClients;
            GameManager.Instance.OnSceneCleanUp += CleanUp;
        }
        private void CleanUp()
        {
            GameManager.Instance.OnSceneCleanUp -= CleanUp;

            GameManager.Instance.OnSceneLoaded -= Init;
            GameManager.Instance.NetworkManager.PhotonLink.ViewOwnershipChanged -= OnOwnershipChanged;
            GameManager.Instance.NetworkManager.PhotonLink.JoinedRoom -= AdjustForClients;
        }

        private void AdjustForClients()
        {
            if (!PhotonNetwork.LocalPlayer.IsMasterClient)
            {
                this.GetComponent<Rigidbody>().isKinematic = true;
            }
        }

        private void OnOwnershipChanged(PhotonView targetView, Player previousOwner)
        {
            //Check that its this Photon View
            if (targetView.ViewID == m_PV.ViewID)
            {
                //if current owner is now local
                if (m_PV.OwnerActorNr == PhotonNetwork.LocalPlayer.ActorNumber)
                {
                    m_RB.isKinematic = false;
                }
                else
                {
                    m_RB.isKinematic = true;
                }
            }
        }
    }
}