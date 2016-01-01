﻿using System;
using UnityEngine;

namespace PilotAssistant.PID
{
    using Utility;
    using FlightModules;

    public class PID_Controller : MonoBehaviour
    {
        protected double target_setpoint = 0; // target setpoint
        protected double active_setpoint = 0;

        protected double k_proportional; // Kp
        protected double k_integral; // Ki
        protected double k_derivative; // Kd

        protected double sum = 0; // integral sum
        protected double previous = 0; // previous value stored for derivative action
        protected double rolling_diff = 0; // used for rolling average difference
        protected double rollingFactor = 0.5; // rolling average proportion. 0 = all new, 1 = never changes
        protected double error = 0; // error of current iteration

        protected double inMin = -1000000000; // Minimum input value
        protected double inMax = 1000000000; // Maximum input value

        protected double outMin; // Minimum output value
        protected double outMax; // Maximum output value

        protected double integralClampUpper; // AIW clamp
        protected double integralClampLower;

        protected double dt = 1; // standardised response for any physics dt

        protected double scale = 1;
        protected double easing = 1;
        protected double increment = 0;

        public double lastOutput { get; protected set; }
        public bool invertInput { get; set; }
        public bool invertOutput { get; set; }
        public bool bShow { get; set; }
        public bool skipDerivative { get; set; }
        public bool isHeadingControl { get; set; }

        public PID_Controller(double Kp, double Ki, double Kd, double OutputMin, double OutputMax, double intClampLower, double intClampUpper, double scalar = 1, double easing = 1)
        {
            k_proportional = Kp;
            k_integral = Ki;
            k_derivative = Kd;
            outMin = OutputMin;
            outMax = OutputMax;
            integralClampLower = intClampLower;
            integralClampUpper = intClampUpper;
            scale = scalar;
            this.easing = easing;
        }

        public PID_Controller(double[] gains)
        {
            k_proportional = gains[0];
            k_integral = gains[1];
            k_derivative = gains[2];
            outMin = gains[3];
            outMax = gains[4];
            integralClampLower = gains[5];
            integralClampUpper = gains[6];
            scale = gains[7];
            easing = gains[8];
        }
        
        public virtual double ResponseD(double input, bool useIntegral)
        {
            if (active_setpoint != target_setpoint)
            {
                increment += easing * TimeWarp.fixedDeltaTime * 0.01;
                if (active_setpoint < target_setpoint)
                {
                    if (active_setpoint + increment > target_setpoint)
                        active_setpoint = target_setpoint;
                    else
                        active_setpoint += increment;
                }
                else
                {
                    if (active_setpoint - increment < target_setpoint)
                        active_setpoint = target_setpoint;
                    else
                        active_setpoint -= increment;
                }
            }
            input = (invertInput ? -1 : 1) * Utils.Clamp(input, inMin, inMax);
            dt = TimeWarp.fixedDeltaTime;
            
            if (!isHeadingControl)
                error = input - active_setpoint;
            else
                error = Utils.CurrentAngleTargetRel(input, active_setpoint, 180) - active_setpoint;

            if (skipDerivative)
            {
                skipDerivative = false;
                previous = input;
            }

            lastOutput = Utils.Clamp((proportionalError(error) + integralError(error, useIntegral) + derivativeError(input)), outMin, outMax);
            return (invertOutput ? -1 : 1) * lastOutput;
        }

        public virtual float ResponseF(double input, bool useIntegral)
        {
            return (float)ResponseD(input, useIntegral);
        }

        protected virtual double proportionalError(double error)
        {
            return error * k_proportional / scale;
        }

        protected virtual double integralError(double error, bool useIntegral)
        {
            if (k_integral == 0 || !useIntegral)
            {
                sum = 0;
                return sum;
            }

            sum += error * dt * k_integral / scale;
            sum = Utils.Clamp(sum, integralClampLower, integralClampUpper); // AIW
            return sum;
        }

        protected virtual double derivativeError(double input)
        {
            double difference = 0;
            if (!isHeadingControl)
                difference = (input - previous) / dt;
            else
            {
                double inputHeadingRounded = Utils.CurrentAngleTargetRel(input, previous, 180);
                difference = (inputHeadingRounded - previous) / dt;
            }
            rolling_diff = rolling_diff * rollingFactor + difference * (1 - rollingFactor); // rolling average sometimes helps smooth out a jumpy derivative response
            
            previous = input;
            return rolling_diff * k_derivative / scale;
        }

        protected virtual double derivativeErrorRate(double rate)
        {
            return rate * k_derivative / scale;
        }

        public virtual void Clear()
        {
            sum = 0;
        }

        public virtual void Preset(bool invert = false)
        {
            sum = lastOutput * (invert ? -1 : 1);
        }

        public virtual void Preset(double target, bool invert = false)
        {
            if (!invert)
                sum = target * (invertOutput ? -1 : 1);
            else
                sum = target * (invertOutput ? 1 : -1);
        }

        #region properties
        public virtual double SetPoint
        {
            get
            {
                return invertInput ? -target_setpoint : target_setpoint;
            }
            set
            {
                active_setpoint = target_setpoint = invertInput ? -value : value;
            }
        }

        /// <summary>
        /// let active setpoint move to match the target to smooth the transition
        /// </summary>
        public virtual double BumplessSetPoint
        {
            get
            {
                return invertInput ? -active_setpoint : active_setpoint;
            }
            set
            {
                target_setpoint = invertInput ? -value : value;
                increment = 0;
            }
        }

        public virtual double PGain
        {
            get
            {
                return k_proportional;
            }
            set
            {
                k_proportional = value;
            }
        }

        public virtual double IGain
        {
            get
            {
                return k_integral;
            }
            set
            {
                k_integral = value;
            }
        }

        public virtual double DGain
        {
            get
            {
                return k_derivative;
            }
            set
            {
                k_derivative = value;
            }
        }

        public virtual double InMin
        {
            set
            {
                inMin = value;
            }
        }

        public virtual double InMax
        {
            set
            {
                inMax = value;
            }
        }

        /// <summary>
        /// Set output minimum to value
        /// </summary>
        public virtual double OutMin
        {
            get
            {
                return outMin;
            }
            set
            {
                outMin = value;
            }
        }

        /// <summary>
        /// Set output maximum to value
        /// </summary>
        public virtual double OutMax
        {
            get
            {
                return outMax;
            }
            set
            {
                outMax = value;
            }
        }

        public virtual double ClampLower
        {
            get
            {
                return integralClampLower;
            }
            set
            {
                integralClampLower = value;
            }
        }

        public virtual double ClampUpper
        {
            get
            {
                return integralClampUpper;
            }
            set
            {
                integralClampUpper = value;
            }
        }

        public virtual double Scalar
        {
            get
            {
                return scale;
            }
            set
            {
                scale = Math.Max(value, 0.01);
            }
        }

        public virtual double Easing
        {
            get
            {
                return easing;
            }
            set
            {
                easing = Math.Max(value, 0.01);
            }
        }
        #endregion
    }

    public class AsstController : PID_Controller
    {
        public AsstList ctrlID { get; set; }

        public AsstController(AsstList ID, double Kp, double Ki, double Kd, double OutputMin, double OutputMax, double intClampLower, double intClampUpper, double scalar = 1, double easing = 1)
                            : base(Kp, Ki, Kd, OutputMin, OutputMax, intClampLower, intClampUpper, scalar, easing)
        {
            ctrlID = ID;
        }

        public AsstController(AsstList ID, double[] gains) : base (gains)
        {
            ctrlID = ID;
        }
    }

    public class SASController : PID_Controller
    {
        public SASList ctrlID { get; set; }

        public SASController(SASList ID, double Kp, double Ki, double Kd, double OutputMin, double OutputMax, double intClampLower, double intClampUpper, double scalar = 1, double easing = 1)
                            : base (Kp, Ki, Kd, OutputMin, OutputMax, intClampLower, intClampUpper, scalar, easing)
        {            
            ctrlID = ID;
        }

        public SASController(SASList ID, double[] gains) : base(gains)
        {
            ctrlID = ID;
        }
    }
}
