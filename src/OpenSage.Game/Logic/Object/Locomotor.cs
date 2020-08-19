﻿using System;
using System.Numerics;
using NLog.Targets;
using OpenSage.Mathematics;
using OpenSage.Terrain;

namespace OpenSage.Logic.Object
{
    /// <summary>
    /// Locomotors control how an object moves towards a target position.
    /// (Locomotors are not the only way an object can move; some behaviors
    /// can do this too, without using a locomotor, for example DumbProjectileBehavior.)
    ///
    /// Locomotor "appearance" controls many aspects of their movement:
    ///
    /// - TREADS
    ///   This is supposed to move like a tank with treads. The object turns while stationary,
    ///   and moves only in a straight line.
    ///
    /// - WINGS
    ///
    /// - HOVER
    ///
    /// - THRUST
    ///
    /// - TWO_LEGS
    ///
    /// - FOUR_WHEELS
    ///
    /// Other Locomotor properties allow more fine-grained control:
    ///
    /// - ForwardAccelerationPitchFactor and LateralAccelerationRollFactor
    ///   These control how much acceleration and cornering, respectively, will cause
    ///   the chassis to pitch or roll.
    /// </summary>
    internal sealed class Locomotor
    {
        private readonly GameObject _gameObject;
        private readonly LocomotorSet _locomotorSet;
        private readonly LocomotorTemplate _locomotorTemplate;

        public Locomotor(GameObject gameObject, LocomotorSet locomotorSet)
        {
            _gameObject = gameObject;
            _locomotorSet = locomotorSet;
            _locomotorTemplate = locomotorSet.Locomotor.Value;
        }

        public float GetPitchDamping()
        {
            return _locomotorTemplate.PitchDamping;
        }

        //TODO: check if the damaged values exists
        private float GetAcceleration()
        {
            return _gameObject.IsDamaged
                ? GetLocomotorValue(x => x.AccelerationDamaged)
                : GetLocomotorValue(x => x.Acceleration);
        }

        private float GetFrontWheelTurnAngle()
        {
            return GetLocomotorValue(x => x.FrontWheelTurnAngle);
        }

        private float GetTurnRate()
        {
            return _gameObject.IsDamaged
                ? GetLocomotorValue(x => x.TurnRateDamaged)
                : GetLocomotorValue(x => x.TurnRate);
        }

        private float GetSpeed()
        {
            // TODO: this is probably not correct for BFME
            if (_locomotorTemplate.Speed.HasValue)
            {
                return _gameObject.IsDamaged
                    ? GetLocomotorValue(x => x.SpeedDamaged)
                    : GetLocomotorValue(x => x.Speed.Value);
            }
            else
            {
                return _locomotorSet.Speed;
            }
        }

        private float GetLift()
        {
            return _gameObject.IsDamaged
                ? GetLocomotorValue(x => x.LiftDamaged)
                : GetLocomotorValue(x => x.Lift);
        }

        public bool MoveTowardsPosition(in TimeInterval gameTime, in Vector3 targetPoint, HeightMap heightMap, in Vector3? nextPoint)
        {
            var deltaTime = (float) gameTime.DeltaTime.TotalSeconds;

            var transform = _gameObject.Transform;

            var x = transform.Translation.X;
            var y = transform.Translation.Y;
            var z = transform.Translation.Z;
            var trans = transform.Translation;

            var oldSpeed = _gameObject.Speed;

            var delta = targetPoint - transform.Translation;

            // Distance is 2D
            var distanceRemaining = delta.Vector2XY().Length();
            var braking = GetLocomotorValue(x => x.Braking);

            switch (_locomotorTemplate.Appearance)
            {
                case LocomotorAppearance.Treads:
                    break;
                default:
                    if (nextPoint != null) braking = 0;

                    // Calculate turn radius from speed & turnrate
                    var circumference = 360.0f / GetTurnRate() * oldSpeed;
                    var radius = circumference / MathUtility.TwoPi;

                    if (distanceRemaining < (radius + 0.25f) && nextPoint != null)
                        // turn towards next point
                        return true;
                    break;
            }

            if (distanceRemaining < 0.25f) return true;

            var damaged = _gameObject.IsDamaged;

            var minimumBrakingDistance = braking > 0
                ? (oldSpeed * oldSpeed) / braking
                : 0;

            // Are we braking or accelerating?
            // TODO: calculate this distance depending on the remaining path or insert a 'breaking'- point
            var accelerating = distanceRemaining > minimumBrakingDistance;
            var currentAcceleration = accelerating
                ? GetAcceleration()
                : -GetLocomotorValue(x => x.Braking);

            var deltaSpeed = currentAcceleration * deltaTime;

            var newSpeed = oldSpeed + deltaSpeed;
            _gameObject.Speed = GetSpeed();//Math.Clamp(newSpeed, 0.0f, GetSpeed());

            // This locomotor speed is distance/second
            var distance = MathF.Min(_gameObject.Speed * deltaTime, distanceRemaining);

            // The distance we're moving
            var direction = Vector2.Normalize(delta.Vector2XY());
            var moveDirection = direction;

            // Calculate rotation
            var currentYaw = -transform.EulerAngles.Z;

            var targetYaw = MathUtility.GetYawFromDirection(direction);
            var angleDelta = MathUtility.CalculateAngleDelta(targetYaw, currentYaw);

            var d = MathUtility.ToRadians(GetTurnRate()) * deltaTime;
            var newDelta = -MathF.Sign(angleDelta) * MathF.Min(MathF.Abs(angleDelta), MathF.Abs(d));
            var yaw = currentYaw + newDelta;

            _gameObject.SteeringWheelsYaw = Math.Clamp(-angleDelta, MathUtility.ToRadians(-GetFrontWheelTurnAngle()), MathUtility.ToRadians(GetFrontWheelTurnAngle()));

            var pitch = 0.0f;
            var roll = 0.0f;
            var height = heightMap.GetHeight(x, y);
            var lookingDirection = transform.LookDirection;

            switch (_locomotorTemplate.Appearance)
            {
                case LocomotorAppearance.Thrust:
                    var targetZ = targetPoint.Z;
                    if (nextPoint != null) targetZ = height + _locomotorTemplate.PreferredHeight;
                    var deltaZ = (distance / distanceRemaining) * (targetZ - trans.Z);
                    trans.Z += deltaZ;
                    pitch = deltaZ;
                    break;
                case LocomotorAppearance.Wings:
                case LocomotorAppearance.Hover:
                    var heightRemaining = (height + _locomotorTemplate.PreferredHeight) - z;
                    var oldLift = _gameObject.Lift;
                    var lift = GetLift();
                    var newLift = oldLift + lift;
                    newLift = Math.Clamp(newLift, 0.0f, lift);
                    _gameObject.Lift = newLift;
                    var velocityZ = MathF.Min(newLift * deltaTime, heightRemaining);
                    trans.Z += velocityZ;

                    moveDirection = Vector2.Normalize(new Vector2(lookingDirection.X, lookingDirection.Y));
                    // pitch = TODO
                    break;
                case LocomotorAppearance.Treads:
                    trans.Z = height;
                    if (MathF.Abs(targetYaw - yaw) > MathUtility.ToRadians(2.0f)) //first fully rotate towards target point
                    {
                        distance = 0.0f;
                    }
                    break;
                case LocomotorAppearance.FourWheels:
                default:
                    trans.Z = height;
                    moveDirection = Vector2.Normalize(new Vector2(lookingDirection.X, lookingDirection.Y));
                    break;
            }

            switch (_locomotorTemplate.Appearance)
            {
                case LocomotorAppearance.Treads:
                case LocomotorAppearance.FourWheels:
                    var normal = heightMap.GetNormal(x, y);

                    var deltaX = normal.X;
                    var deltaY = normal.Y;

                    pitch = (float) Math.Asin(deltaX);
                    roll = (float) Math.Asin(deltaY);
                    break;
                default:
                    break;
            }

            trans += new Vector3(moveDirection * distance, roll);
            transform.Translation = trans;

            transform.Rotation = Quaternion.CreateFromYawPitchRoll(pitch, 0.0f, yaw);
            return false;
        }

        public void MaintainPosition(in TimeInterval gameTime, HeightMap heightMap)
        {
            switch (_locomotorTemplate.Appearance)
            {
                case LocomotorAppearance.Wings:
                    var deltaTime = (float) gameTime.DeltaTime.TotalSeconds;

                    var transform = _gameObject.Transform;
                    var trans = transform.Translation;

                    var circumference = 2.0f * Math.PI * GetLocomotorValue(x => x.CirclingRadius);
                    var timePerRoundtrip = circumference / GetSpeed();

                    var lookingDirection = transform.LookDirection;
                    var moveDirection = Vector2.Normalize(new Vector2(lookingDirection.X, lookingDirection.Y));

                    var deltaTransform = new Vector3(moveDirection * GetSpeed() * deltaTime, 0.0f);

                    trans += deltaTransform;
                    transform.Translation = trans;

                    var yaw = -transform.EulerAngles.Z + (deltaTime / timePerRoundtrip) * Math.PI * 2.0f;
                    transform.Rotation = Quaternion.CreateFromYawPitchRoll(0.0f, 0.0f, (float)yaw);
                    break;

            }
        }

        private float GetLocomotorValue(Func<LocomotorTemplate, float> getValue)
        {
            return (_locomotorSet.Speed / 100.0f) * getValue(_locomotorTemplate);
        }
    }
}
