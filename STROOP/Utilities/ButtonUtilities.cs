﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using STROOP.Structs;
using STROOP.Extensions;
using STROOP.Structs.Configurations;
using STROOP.Managers;

namespace STROOP.Utilities
{
    public static class ButtonUtilities
    {
        private struct TripleAddressAngle
        {
            public readonly uint XAddress;
            public readonly uint YAddress;
            public readonly uint ZAddress;
            public readonly ushort? Angle;

            public TripleAddressAngle(uint xAddress, uint yAddress, uint zAddress, ushort? angle = null)
            {
                XAddress = xAddress;
                YAddress = yAddress;
                ZAddress = zAddress;
                Angle = angle;
            }

            public (uint XAddress, uint YAddress, uint ZAddress) GetTripleAddress()
            {
                return (XAddress, YAddress, ZAddress);
            }
        }
        
        private enum Change { SET, ADD, MULTIPLY };

        private static bool ChangeValues(List<TripleAddressAngle> posAddressAngles,
            float xValue, float yValue, float zValue, Change change, bool useRelative = false,
            (bool affectX, bool affectY, bool affectZ)? affects = null)
        {
            if (posAddressAngles.Count == 0)
                return false;

            bool success = true;
            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            foreach (var posAddressAngle in posAddressAngles)
            {
                float currentXValue = xValue;
                float currentYValue = yValue;
                float currentZValue = zValue;

                if (change == Change.ADD)
                {
                    HandleScaling(ref currentXValue, ref currentZValue);
                    HandleRelativeAngle(ref currentXValue, ref currentZValue, useRelative, posAddressAngle.Angle);
                    currentXValue += Config.Stream.GetSingle(posAddressAngle.XAddress);
                    currentYValue += Config.Stream.GetSingle(posAddressAngle.YAddress);
                    currentZValue += Config.Stream.GetSingle(posAddressAngle.ZAddress);
                }

                if (change == Change.MULTIPLY)
                {
                    currentXValue *= Config.Stream.GetSingle(posAddressAngle.XAddress);
                    currentYValue *= Config.Stream.GetSingle(posAddressAngle.YAddress);
                    currentZValue *= Config.Stream.GetSingle(posAddressAngle.ZAddress);
                }

                if (!affects.HasValue || affects.Value.affectX)
                {
                    success &= Config.Stream.SetValue(currentXValue, posAddressAngle.XAddress);
                }

                if (!affects.HasValue || affects.Value.affectY)
                {
                    success &= Config.Stream.SetValue(currentYValue, posAddressAngle.YAddress);
                }

                if (!affects.HasValue || affects.Value.affectZ)
                {
                    success &= Config.Stream.SetValue(currentZValue, posAddressAngle.ZAddress);
                }
            }

            if (!streamAlreadySuspended) Config.Stream.Resume();
            return success;
        }

        public static void HandleScaling(ref float xOffset, ref float zOffset)
        {
            if (OptionsConfig.ScaleDiagonalPositionControllerButtons)
            {
                (xOffset, zOffset) = ((float, float))MoreMath.ScaleValues(xOffset, zOffset);
            }
        }

        public static void HandleRelativeAngle(ref float xOffset, ref float zOffset, bool useRelative, double? relativeAngle)
        {
            if (useRelative)
            {
                if (!relativeAngle.HasValue)
                    throw new ArgumentNullException();

                switch (PositionControllerRelativityConfig.Relativity)
                {
                    case PositionControllerRelativity.Recommended:
                        // relativeAngle is already correct
                        break;
                    case PositionControllerRelativity.Mario:
                        relativeAngle = Config.Stream.GetUInt16(MarioConfig.StructAddress + MarioConfig.YawFacingOffset);
                        break;
                    case PositionControllerRelativity.Custom:
                        relativeAngle = MoreMath.NormalizeAngleUshort(PositionControllerRelativityConfig.CustomAngle);
                        break;
                }
                double thetaChange = MoreMath.NormalizeAngleDouble(relativeAngle.Value - 32768);
                (xOffset, _, zOffset) = ((float, float, float))MoreMath.OffsetSpherically(xOffset, 0, zOffset, 0, thetaChange, 0);
            }
        }

        public static bool GotoObjects(List<uint> objAddresses, (bool affectX, bool affectY, bool affectZ)? affects = null)
        {
            if (objAddresses.Count == 0)
                return false;

            List<TripleAddressAngle> posAddressAngles =
                new List<TripleAddressAngle> {
                    new TripleAddressAngle(
                        MarioConfig.StructAddress + MarioConfig.XOffset,
                        MarioConfig.StructAddress + MarioConfig.YOffset,
                        MarioConfig.StructAddress + MarioConfig.ZOffset)
                };

            float xDestination = objAddresses.Average(obj => Config.Stream.GetSingle(obj + ObjectConfig.XOffset));
            float yDestination = objAddresses.Average(obj => Config.Stream.GetSingle(obj + ObjectConfig.YOffset));
            float zDestination = objAddresses.Average(obj => Config.Stream.GetSingle(obj + ObjectConfig.ZOffset));

            HandleGotoOffset(ref xDestination, ref yDestination, ref zDestination);

            return ChangeValues(posAddressAngles, xDestination, yDestination, zDestination, Change.SET, false, affects);
        }

        public static bool RetrieveObjects(List<uint> objAddresses, (bool affectX, bool affectY, bool affectZ)? affects = null)
        {
            List<TripleAddressAngle> posAddressAngles =
                objAddresses.ConvertAll<TripleAddressAngle>(
                    objAddress => new TripleAddressAngle(
                        objAddress + ObjectConfig.XOffset,
                        objAddress + ObjectConfig.YOffset,
                        objAddress + ObjectConfig.ZOffset));

            float xDestination = Config.Stream.GetSingle(MarioConfig.StructAddress + MarioConfig.XOffset);
            float yDestination = Config.Stream.GetSingle(MarioConfig.StructAddress + MarioConfig.YOffset);
            float zDestination = Config.Stream.GetSingle(MarioConfig.StructAddress + MarioConfig.ZOffset);

            HandleRetrieveOffset(ref xDestination, ref yDestination, ref zDestination);

            return ChangeValues(posAddressAngles, xDestination, yDestination, zDestination, Change.SET, false, affects);
        }

        private static void HandleGotoOffset(ref float xPos, ref float yPos, ref float zPos)
        {
            float gotoAbove = GotoRetrieveConfig.GotoAboveOffset;
            float gotoInfront = GotoRetrieveConfig.GotoInfrontOffset;
            ushort marioYaw = Config.Stream.GetUInt16(MarioConfig.StructAddress + MarioConfig.YawFacingOffset);

            double xOffset, zOffset;
            (xOffset, zOffset) = MoreMath.GetComponentsFromVector(-1 * gotoInfront, marioYaw);

            xPos += (float)xOffset;
            yPos += gotoAbove;
            zPos += (float)zOffset;
        }

        private static void HandleRetrieveOffset(ref float xPos, ref float yPos, ref float zPos)
        {
            float retrieveAbove = GotoRetrieveConfig.RetrieveAboveOffset;
            float retrieveInfront = GotoRetrieveConfig.RetrieveInfrontOffset;
            ushort marioYaw = Config.Stream.GetUInt16(MarioConfig.StructAddress + MarioConfig.YawFacingOffset);

            double xOffset, zOffset;
            (xOffset, zOffset) = MoreMath.GetComponentsFromVector(retrieveInfront, marioYaw);

            xPos += (float)xOffset;
            yPos += retrieveAbove;
            zPos += (float)zOffset;
        }

        public static bool TranslateObjects(List<uint> objAddresses,
            float xOffset, float yOffset, float zOffset, bool useRelative)
        {
            List<TripleAddressAngle> posAddressAngles =
                objAddresses.ConvertAll<TripleAddressAngle>(
                    objAddress => new TripleAddressAngle(
                        objAddress + ObjectConfig.XOffset,
                        objAddress + ObjectConfig.YOffset,
                        objAddress + ObjectConfig.ZOffset,
                        Config.Stream.GetUInt16(objAddress + ObjectConfig.YawFacingOffset)));

            return ChangeValues(posAddressAngles, xOffset, yOffset, zOffset, Change.ADD, useRelative);
        }

        public static bool TranslateObjectHomes(List<uint> objAddresses,
            float xOffset, float yOffset, float zOffset, bool useRelative)
        {
            List<TripleAddressAngle> posAddressAngles =
                objAddresses.ConvertAll<TripleAddressAngle>(
                    objAddress => new TripleAddressAngle(
                        objAddress + ObjectConfig.HomeXOffset,
                        objAddress + ObjectConfig.HomeYOffset,
                        objAddress + ObjectConfig.HomeZOffset,
                        Config.Stream.GetUInt16(objAddress + ObjectConfig.YawFacingOffset)));

            return ChangeValues(posAddressAngles, xOffset, yOffset, zOffset, Change.ADD, useRelative);
        }

        public static bool RotateObjects(List<uint> objAddresses,
            int yawOffset, int pitchOffset, int rollOffset)
        {
            if (objAddresses.Count == 0)
                return false;

            bool success = true;
            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            foreach (var objAddress in objAddresses)
            {
                ushort yawFacing, pitchFacing, rollFacing, yawMoving, pitchMoving, rollMoving;
                yawFacing = Config.Stream.GetUInt16(objAddress + ObjectConfig.YawFacingOffset);
                pitchFacing = Config.Stream.GetUInt16(objAddress + ObjectConfig.PitchFacingOffset);
                rollFacing = Config.Stream.GetUInt16(objAddress + ObjectConfig.RollFacingOffset);
                yawMoving = Config.Stream.GetUInt16(objAddress + ObjectConfig.YawMovingOffset);
                pitchMoving = Config.Stream.GetUInt16(objAddress + ObjectConfig.PitchMovingOffset);
                rollMoving = Config.Stream.GetUInt16(objAddress + ObjectConfig.RollMovingOffset);

                yawFacing += (ushort)yawOffset;
                pitchFacing += (ushort)pitchOffset;
                rollFacing += (ushort)rollOffset;
                yawMoving += (ushort)yawOffset;
                pitchMoving += (ushort)pitchOffset;
                rollMoving += (ushort)rollOffset;

                success &= Config.Stream.SetValue(yawFacing, objAddress + ObjectConfig.YawFacingOffset);
                success &= Config.Stream.SetValue(pitchFacing, objAddress + ObjectConfig.PitchFacingOffset);
                success &= Config.Stream.SetValue(rollFacing, objAddress + ObjectConfig.RollFacingOffset);
                success &= Config.Stream.SetValue(yawMoving, objAddress + ObjectConfig.YawMovingOffset);
                success &= Config.Stream.SetValue(pitchMoving, objAddress + ObjectConfig.PitchMovingOffset);
                success &= Config.Stream.SetValue(rollMoving, objAddress + ObjectConfig.RollMovingOffset);
            }

            if (!streamAlreadySuspended) Config.Stream.Resume();
            return success;
        }

        public static bool ScaleObjects(List<uint> objAddresses,
            float widthChange, float heightChange, float depthChange, bool multiply)
        {
            List<TripleAddressAngle> posAddressAngles =
                objAddresses.ConvertAll<TripleAddressAngle>(
                    objAddress => new TripleAddressAngle(
                        objAddress + ObjectConfig.ScaleWidthOffset,
                        objAddress + ObjectConfig.ScaleHeightOffset,
                        objAddress + ObjectConfig.ScaleDepthOffset));

            return ChangeValues(posAddressAngles, widthChange, heightChange, depthChange, multiply ? Change.MULTIPLY : Change.ADD);
        }

        public static bool GotoObjectsHome(List<uint> objAddresses, (bool affectX, bool affectY, bool affectZ)? affects = null)
        {
            if (objAddresses.Count == 0)
                return false;

            List<TripleAddressAngle> posAddressAngles =
                new List<TripleAddressAngle> {
                    new TripleAddressAngle(
                        MarioConfig.StructAddress + MarioConfig.XOffset,
                        MarioConfig.StructAddress + MarioConfig.YOffset,
                        MarioConfig.StructAddress + MarioConfig.ZOffset)
                };

            float xDestination = objAddresses.Average(obj => Config.Stream.GetSingle(obj + ObjectConfig.HomeXOffset));
            float yDestination = objAddresses.Average(obj => Config.Stream.GetSingle(obj + ObjectConfig.HomeYOffset));
            float zDestination = objAddresses.Average(obj => Config.Stream.GetSingle(obj + ObjectConfig.HomeZOffset));

            HandleGotoOffset(ref xDestination, ref yDestination, ref zDestination);

            return ChangeValues(posAddressAngles, xDestination, yDestination, zDestination, Change.SET, false, affects);
        }

        public static bool RetrieveObjectsHome(List<uint> objAddresses, (bool affectX, bool affectY, bool affectZ)? affects = null)
        {
            List<TripleAddressAngle> posAddressAngles =
                objAddresses.ConvertAll<TripleAddressAngle>(
                    objAddress => new TripleAddressAngle(
                        objAddress + ObjectConfig.HomeXOffset,
                        objAddress + ObjectConfig.HomeYOffset,
                        objAddress + ObjectConfig.HomeZOffset));

            float xDestination = Config.Stream.GetSingle(MarioConfig.StructAddress + MarioConfig.XOffset);
            float yDestination = Config.Stream.GetSingle(MarioConfig.StructAddress + MarioConfig.YOffset);
            float zDestination = Config.Stream.GetSingle(MarioConfig.StructAddress + MarioConfig.ZOffset);

            HandleRetrieveOffset(ref xDestination, ref yDestination, ref zDestination);

            return ChangeValues(posAddressAngles, xDestination, yDestination, zDestination, Change.SET, false, affects);
        }

        public static bool CloneObject(uint objAddress, bool updateAction = true)
        {
            bool success = true;
            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            uint lastObject = Config.Stream.GetUInt32(MarioConfig.StructAddress + MarioConfig.HeldObjectPointerOffset);
            
            // Set clone action flags
            if (lastObject == 0x00000000U && updateAction)
            {
                // Set Next action
                uint currentAction = Config.Stream.GetUInt32(MarioConfig.StructAddress + MarioConfig.ActionOffset);
                uint nextAction = TableConfig.MarioActions.GetAfterCloneValue(currentAction);
                success &= Config.Stream.SetValue(nextAction, MarioConfig.StructAddress + MarioConfig.ActionOffset);
            }

            // Set new held value
            success &= Config.Stream.SetValue(objAddress, MarioConfig.StructAddress + MarioConfig.HeldObjectPointerOffset);

            if (!streamAlreadySuspended) Config.Stream.Resume();
            return success;
        }

        public static bool UnCloneObject(bool updateAction = true)
        {
            bool success = true;
            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            // Set mario's next action
            if (updateAction)
            {
                uint currentAction = Config.Stream.GetUInt32(MarioConfig.StructAddress + MarioConfig.ActionOffset);
                uint nextAction = TableConfig.MarioActions.GetAfterUncloneValue(currentAction);
                success &= Config.Stream.SetValue(nextAction, MarioConfig.StructAddress + MarioConfig.ActionOffset);
            }

            // Clear mario's held object
            success &= Config.Stream.SetValue(0x00000000U, MarioConfig.StructAddress + MarioConfig.HeldObjectPointerOffset);

            if (!streamAlreadySuspended) Config.Stream.Resume();
            return success;
        }

        public static bool UnloadObject(List<uint> addresses)
        {
            if (addresses.Count == 0)
                return false;

            bool success = true;
            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            foreach (var address in addresses)
            {
                var test = Config.Stream.GetUInt16(address + ObjectConfig.ActiveOffset);
                success &= Config.Stream.SetValue((short) 0x0000, address + ObjectConfig.ActiveOffset);
            }

            if (!streamAlreadySuspended) Config.Stream.Resume();
            return success;
        }

        public static bool ReviveObject(List<uint> addresses)
        {
            if (addresses.Count == 0)
                return false;

            bool success = true;
            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            foreach (var address in addresses)
            {
                // Find process group
                uint scriptAddress = Config.Stream.GetUInt32(address + ObjectConfig.BehaviorScriptOffset);
                if (scriptAddress == 0x00000000)
                    continue;
                uint firstScriptAction = Config.Stream.GetUInt32(scriptAddress);
                if ((firstScriptAction & 0xFF000000U) != 0x00000000U)
                    continue;
                byte processGroup = (byte)((firstScriptAction & 0x00FF0000U) >> 16);

                // Read first object in group
                uint groupAddress = ObjectSlotsConfig.FirstGroupingAddress + processGroup * ObjectSlotsConfig.ProcessGroupStructSize;

                // Loop through and find last object in group
                uint lastGroupObj = groupAddress;
                while (Config.Stream.GetUInt32(lastGroupObj + ObjectConfig.ProcessedNextLinkOffset) != groupAddress)
                    lastGroupObj = Config.Stream.GetUInt32(lastGroupObj + ObjectConfig.ProcessedNextLinkOffset);

                // Remove object from current group
                uint nextObj = Config.Stream.GetUInt32(address + ObjectConfig.ProcessedNextLinkOffset);
                uint prevObj = Config.Stream.GetUInt32(ObjectSlotsConfig.VactantPointerAddress);
                if (prevObj == address)
                {
                    // Set new vacant pointer
                    success &= Config.Stream.SetValue(nextObj, ObjectSlotsConfig.VactantPointerAddress);
                }
                else
                {
                    for (int i = 0; i < ObjectSlotsConfig.MaxSlots; i++)
                    {
                        uint obj = Config.Stream.GetUInt32(prevObj + ObjectConfig.ProcessedNextLinkOffset);
                        if (obj == address)
                            break;
                        prevObj = obj;
                    }
                    success &= Config.Stream.SetValue(nextObj, prevObj + ObjectConfig.ProcessedNextLinkOffset);
                }

                // Insert object in new group
                nextObj = Config.Stream.GetUInt32(lastGroupObj + ObjectConfig.ProcessedNextLinkOffset);
                success &= Config.Stream.SetValue(address, nextObj + ObjectConfig.ProcessedPreviousLinkOffset);
                success &= Config.Stream.SetValue(address, lastGroupObj + ObjectConfig.ProcessedNextLinkOffset);
                success &= Config.Stream.SetValue(lastGroupObj, address + ObjectConfig.ProcessedPreviousLinkOffset);
                success &= Config.Stream.SetValue(nextObj, address + ObjectConfig.ProcessedNextLinkOffset);

                success &= Config.Stream.SetValue((short)0x0101, address + ObjectConfig.ActiveOffset);

                if (addresses.Count > 1)
                    if (!Config.Stream.RefreshRam() || !success)
                        break;
            }

            if (!streamAlreadySuspended) Config.Stream.Resume();
            return success;
        }

        public static bool ReleaseObject(List<uint> addresses, bool useThrownValue = true)
        {
            if (addresses.Count == 0)
                return false;

            uint releasedValue = useThrownValue ? ObjectConfig.ReleaseStatusThrownValue : ObjectConfig.ReleaseStatusDroppedValue;

            bool success = true;
            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            foreach (var address in addresses)
            {
                success &= Config.Stream.SetValue(releasedValue, address + ObjectConfig.ReleaseStatusOffset);
                success &= Config.Stream.SetValue(ObjectConfig.StackIndexReleasedValue, address + ObjectConfig.StackIndexOffset);
            }

            if (!streamAlreadySuspended) Config.Stream.Resume();
            return success;
        }

        public static bool UnReleaseObject(List<uint> addresses)
        {
            if (addresses.Count == 0)
                return false;

            bool success = true;
            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            foreach (var address in addresses)
            {
                uint initialReleaseStatus = Config.Stream.GetUInt32(address + ObjectConfig.InitialReleaseStatusOffset);
                success &= Config.Stream.SetValue(initialReleaseStatus, address + ObjectConfig.ReleaseStatusOffset);
                success &= Config.Stream.SetValue(ObjectConfig.StackIndexUnReleasedValue, address + ObjectConfig.StackIndexOffset);
            }

            if (!streamAlreadySuspended) Config.Stream.Resume();
            return success;
        }

        public static bool InteractObject(List<uint> addresses)
        {
            if (addresses.Count == 0)
                return false;

            bool success = true;
            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            foreach (var address in addresses)
            {
                success &= Config.Stream.SetValue(0xFFFFFFFF, address + ObjectConfig.InteractionStatusOffset);
            }

            if (!streamAlreadySuspended) Config.Stream.Resume();
            return success;
        }

        public static bool UnInteractObject(List<uint> addresses)
        {
            if (addresses.Count == 0)
                return false;

            bool success = true;
            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            foreach (var address in addresses)
            {
                success &= Config.Stream.SetValue(0x00000000, address + ObjectConfig.InteractionStatusOffset);
            }

            if (!streamAlreadySuspended) Config.Stream.Resume();
            return success;
        }

        public static bool ToggleHandsfree()
        {
            bool success = true;
            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            var heldObj = Config.Stream.GetUInt32(MarioConfig.StructAddress + MarioConfig.HeldObjectPointerOffset);

            if (heldObj != 0x00000000U)
            {
                uint currentAction = Config.Stream.GetUInt32(MarioConfig.StructAddress + MarioConfig.ActionOffset);
                uint nextAction = TableConfig.MarioActions.GetHandsfreeValue(currentAction);
                success = Config.Stream.SetValue(nextAction, MarioConfig.StructAddress + MarioConfig.ActionOffset);
            }

            if (!streamAlreadySuspended) Config.Stream.Resume();
            return success;
        }

        public static bool ToggleVisibility()
        {
            bool success = true;
            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            var marioObjRef = Config.Stream.GetUInt32(MarioObjectConfig.PointerAddress);
            if (marioObjRef != 0x00000000U)
            {
                var marioGraphics = Config.Stream.GetUInt32(marioObjRef + ObjectConfig.BehaviorGfxOffset);
                if (marioGraphics == 0)
                { 
                    success &= Config.Stream.SetValue(MarioObjectConfig.GraphicValue, marioObjRef + ObjectConfig.BehaviorGfxOffset);
                }
                else
                {
                    success &= Config.Stream.SetValue(0x00000000U, marioObjRef + ObjectConfig.BehaviorGfxOffset);
                }
            }

            if (!streamAlreadySuspended) Config.Stream.Resume();
            return success;
        }

        public static bool TranslateMario(float xOffset, float yOffset, float zOffset, bool useRelative)
        {
            List<TripleAddressAngle> posAddressAngles =
                new List<TripleAddressAngle> {
                    new TripleAddressAngle(
                        MarioConfig.StructAddress + MarioConfig.XOffset,
                        MarioConfig.StructAddress + MarioConfig.YOffset,
                        MarioConfig.StructAddress + MarioConfig.ZOffset,
                        Config.Stream.GetUInt16(MarioConfig.StructAddress + MarioConfig.YawFacingOffset))
                };

            return ChangeValues(posAddressAngles, xOffset, yOffset, zOffset, Change.ADD, useRelative);
        }

        public static bool SetMarioPosition(float xValue, float yValue, float zValue)
        {
            List<TripleAddressAngle> posAddressAngles =
                new List<TripleAddressAngle> {
                    new TripleAddressAngle(
                        MarioConfig.StructAddress + MarioConfig.XOffset,
                        MarioConfig.StructAddress + MarioConfig.YOffset,
                        MarioConfig.StructAddress + MarioConfig.ZOffset)
                };

            return ChangeValues(posAddressAngles, xValue, yValue, zValue, Change.SET);
        }

        public static bool GotoHOLP((bool affectX, bool affectY, bool affectZ)? affects = null)
        {
            List<TripleAddressAngle> posAddressAngles =
                new List<TripleAddressAngle> {
                    new TripleAddressAngle(
                        MarioConfig.StructAddress + MarioConfig.XOffset,
                        MarioConfig.StructAddress + MarioConfig.YOffset,
                        MarioConfig.StructAddress + MarioConfig.ZOffset)
                };

            float xDestination = Config.Stream.GetSingle(MarioConfig.StructAddress + MarioConfig.HOLPXOffset);
            float yDestination = Config.Stream.GetSingle(MarioConfig.StructAddress + MarioConfig.HOLPYOffset);
            float zDestination = Config.Stream.GetSingle(MarioConfig.StructAddress + MarioConfig.HOLPZOffset);

            return ChangeValues(posAddressAngles, xDestination, yDestination, zDestination, Change.SET, false, affects);
        }

        public static bool RetrieveHOLP((bool affectX, bool affectY, bool affectZ)? affects = null)
        {
            List<TripleAddressAngle> posAddressAngles =
                new List<TripleAddressAngle> {
                    new TripleAddressAngle(
                        MarioConfig.StructAddress + MarioConfig.HOLPXOffset,
                        MarioConfig.StructAddress + MarioConfig.HOLPYOffset,
                        MarioConfig.StructAddress + MarioConfig.HOLPZOffset)
                };

            float xDestination = Config.Stream.GetSingle(MarioConfig.StructAddress + MarioConfig.XOffset);
            float yDestination = Config.Stream.GetSingle(MarioConfig.StructAddress + MarioConfig.YOffset);
            float zDestination = Config.Stream.GetSingle(MarioConfig.StructAddress + MarioConfig.ZOffset);

            return ChangeValues(posAddressAngles, xDestination, yDestination, zDestination, Change.SET, false, affects);
        }

        public static bool TranslateHOLP(float xOffset, float yOffset, float zOffset, bool useRelative)
        {
            List<TripleAddressAngle> posAddressAngles =
                new List<TripleAddressAngle> {
                    new TripleAddressAngle(
                        MarioConfig.StructAddress + MarioConfig.HOLPXOffset,
                        MarioConfig.StructAddress + MarioConfig.HOLPYOffset,
                        MarioConfig.StructAddress + MarioConfig.HOLPZOffset,
                        Config.Stream.GetUInt16(MarioConfig.StructAddress + MarioConfig.YawFacingOffset))
                };

            return ChangeValues(posAddressAngles, xOffset, yOffset, zOffset, Change.ADD, useRelative);
        }

        public static bool MarioChangeYaw(int yawOffset)
        {
            ushort yaw = Config.Stream.GetUInt16(MarioConfig.StructAddress + MarioConfig.YawFacingOffset);
            yaw += (ushort)yawOffset;

            bool success = true;
            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            success &= Config.Stream.SetValue(yaw, MarioConfig.StructAddress + MarioConfig.YawFacingOffset);

            if (!streamAlreadySuspended) Config.Stream.Resume();
            return success;
        }

        public static bool MarioChangeHspd(float hspdOffset)
        {
            float hspd = Config.Stream.GetSingle(MarioConfig.StructAddress + MarioConfig.HSpeedOffset);
            hspd += hspdOffset;

            bool success = true;
            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            success &= Config.Stream.SetValue(hspd, MarioConfig.StructAddress + MarioConfig.HSpeedOffset);

            if (!streamAlreadySuspended) Config.Stream.Resume();
            return success;
        }

        public static bool MarioChangeVspd(float vspdOffset)
        {
            float vspd = Config.Stream.GetSingle(MarioConfig.StructAddress + MarioConfig.VSpeedOffset);
            vspd += vspdOffset;

            bool success = true;
            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            success &= Config.Stream.SetValue(vspd, MarioConfig.StructAddress + MarioConfig.VSpeedOffset);

            if (!streamAlreadySuspended) Config.Stream.Resume();
            return success;
        }

        public static void MarioChangeSlidingSpeedX(float xOffset)
        {
            float slidingSpeedX = Config.Stream.GetSingle(MarioConfig.StructAddress + MarioConfig.SlidingSpeedXOffset);
            float slidingSpeedZ = Config.Stream.GetSingle(MarioConfig.StructAddress + MarioConfig.SlidingSpeedZOffset);

            float newSlidingSpeedX = slidingSpeedX + xOffset;
            ushort newSlidingSpeedYaw = MoreMath.AngleTo_AngleUnitsRounded(newSlidingSpeedX, slidingSpeedZ);

            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            Config.Stream.SetValue(newSlidingSpeedX, MarioConfig.StructAddress + MarioConfig.SlidingSpeedXOffset);
            Config.Stream.SetValue(newSlidingSpeedYaw, MarioConfig.StructAddress + MarioConfig.SlidingYawOffset);

            if (!streamAlreadySuspended) Config.Stream.Resume();
        }

        public static void MarioChangeSlidingSpeedZ(float zOffset)
        {
            float slidingSpeedX = Config.Stream.GetSingle(MarioConfig.StructAddress + MarioConfig.SlidingSpeedXOffset);
            float slidingSpeedZ = Config.Stream.GetSingle(MarioConfig.StructAddress + MarioConfig.SlidingSpeedZOffset);

            float newSlidingSpeedZ = slidingSpeedZ + zOffset;
            ushort newSlidingSpeedYaw = MoreMath.AngleTo_AngleUnitsRounded(slidingSpeedX, newSlidingSpeedZ);

            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            Config.Stream.SetValue(newSlidingSpeedZ, MarioConfig.StructAddress + MarioConfig.SlidingSpeedZOffset);
            Config.Stream.SetValue(newSlidingSpeedYaw, MarioConfig.StructAddress + MarioConfig.SlidingYawOffset);

            if (!streamAlreadySuspended) Config.Stream.Resume();
        }

        public static void MarioChangeSlidingSpeedH(float hOffset)
        {
            float slidingSpeedX = Config.Stream.GetSingle(MarioConfig.StructAddress + MarioConfig.SlidingSpeedXOffset);
            float slidingSpeedZ = Config.Stream.GetSingle(MarioConfig.StructAddress + MarioConfig.SlidingSpeedZOffset);
            double slidingSpeedH = MoreMath.GetHypotenuse(slidingSpeedX, slidingSpeedZ);

            double? slidingSpeedYawComputed = MoreMath.AngleTo_AngleUnitsNullable(slidingSpeedX, slidingSpeedZ);
            ushort slidingSpeedYawMemory = Config.Stream.GetUInt16(MarioConfig.StructAddress + MarioConfig.SlidingYawOffset);
            double slidingSpeedYaw = slidingSpeedYawComputed ?? slidingSpeedYawMemory;

            double newSlidingSpeedH = slidingSpeedH + hOffset;
            (double newSlidingSpeedX, double newSlidingSpeedZ) = MoreMath.GetComponentsFromVector(newSlidingSpeedH, slidingSpeedYaw);
            double newSlidingSpeedYaw = MoreMath.AngleTo_AngleUnits(newSlidingSpeedX, newSlidingSpeedZ);

            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            Config.Stream.SetValue((float)newSlidingSpeedX, MarioConfig.StructAddress + MarioConfig.SlidingSpeedXOffset);
            Config.Stream.SetValue((float)newSlidingSpeedZ, MarioConfig.StructAddress + MarioConfig.SlidingSpeedZOffset);
            Config.Stream.SetValue(MoreMath.NormalizeAngleUshort(newSlidingSpeedYaw), MarioConfig.StructAddress + MarioConfig.SlidingYawOffset);

            if (!streamAlreadySuspended) Config.Stream.Resume();
        }

        public static void MarioChangeSlidingSpeedYaw(float yawOffset)
        {
            float slidingSpeedX = Config.Stream.GetSingle(MarioConfig.StructAddress + MarioConfig.SlidingSpeedXOffset);
            float slidingSpeedZ = Config.Stream.GetSingle(MarioConfig.StructAddress + MarioConfig.SlidingSpeedZOffset);
            double slidingSpeedH = MoreMath.GetHypotenuse(slidingSpeedX, slidingSpeedZ);

            double? slidingSpeedYawComputed = MoreMath.AngleTo_AngleUnitsNullable(slidingSpeedX, slidingSpeedZ);
            ushort slidingSpeedYawMemory = Config.Stream.GetUInt16(MarioConfig.StructAddress + MarioConfig.SlidingYawOffset);
            double slidingSpeedYaw = slidingSpeedYawComputed ?? slidingSpeedYawMemory;

            double newSlidingSpeedYaw = slidingSpeedYaw + yawOffset;
            (double newSlidingSpeedX, double newSlidingSpeedZ) = MoreMath.GetComponentsFromVector(slidingSpeedH, newSlidingSpeedYaw);

            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            Config.Stream.SetValue((float)newSlidingSpeedX, MarioConfig.StructAddress + MarioConfig.SlidingSpeedXOffset);
            Config.Stream.SetValue((float)newSlidingSpeedZ, MarioConfig.StructAddress + MarioConfig.SlidingSpeedZOffset);
            Config.Stream.SetValue(MoreMath.NormalizeAngleUshort(newSlidingSpeedYaw), MarioConfig.StructAddress + MarioConfig.SlidingYawOffset);

            if (!streamAlreadySuspended) Config.Stream.Resume();
        }

        public static bool FullHp()
        {
            return Config.Stream.SetValue(HudConfig.FullHp, MarioConfig.StructAddress + HudConfig.HpCountOffset);
        }

        public static bool Die()
        {
            return Config.Stream.SetValue((short)255, MarioConfig.StructAddress + HudConfig.HpCountOffset);
        }

        public static bool StandardHud()
        {
            bool success = true;
            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            success &= Config.Stream.SetValue(HudConfig.FullHp, MarioConfig.StructAddress + HudConfig.HpCountOffset);
            success &= Config.Stream.SetValue(HudConfig.StandardCoins, MarioConfig.StructAddress + HudConfig.CoinCountOffset);
            success &= Config.Stream.SetValue(HudConfig.StandardLives, MarioConfig.StructAddress + HudConfig.LifeCountOffset);
            success &= Config.Stream.SetValue(HudConfig.StandardStars, MarioConfig.StructAddress + HudConfig.StarCountOffset);

            success &= Config.Stream.SetValue(HudConfig.FullHpInt, MarioConfig.StructAddress + HudConfig.HpDisplayOffset);
            success &= Config.Stream.SetValue(HudConfig.StandardCoins, MarioConfig.StructAddress + HudConfig.CoinDisplayOffset);
            success &= Config.Stream.SetValue((short)HudConfig.StandardLives, MarioConfig.StructAddress + HudConfig.LifeDisplayOffset);
            success &= Config.Stream.SetValue(HudConfig.StandardStars, MarioConfig.StructAddress + HudConfig.StarDisplayOffset);

            if (!streamAlreadySuspended) Config.Stream.Resume();
            return success;
        }

        public static bool Coins99()
        {
            bool success = true;
            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            success &= Config.Stream.SetValue((short)99, MarioConfig.StructAddress + HudConfig.CoinCountOffset);
            success &= Config.Stream.SetValue((short)99, MarioConfig.StructAddress + HudConfig.CoinDisplayOffset);

            if (!streamAlreadySuspended) Config.Stream.Resume();
            return success;
        }

        public static bool Lives100()
        {
            bool success = true;
            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            success &= Config.Stream.SetValue((sbyte)100, MarioConfig.StructAddress + HudConfig.LifeCountOffset);
            success &= Config.Stream.SetValue((short)100, MarioConfig.StructAddress + HudConfig.LifeDisplayOffset);

            if (!streamAlreadySuspended) Config.Stream.Resume();
            return success;
        }

        public static bool GotoTriangle(uint triangleAddress, int vertex, bool _useMisalignmentOffset = false)
        {
            if (triangleAddress == 0x0000)
                return false;

            float newX, newY, newZ;
            switch(vertex)
            {
                case 1:
                    newX = Config.Stream.GetInt16(triangleAddress + TriangleOffsetsConfig.X1);
                    newY = Config.Stream.GetInt16(triangleAddress + TriangleOffsetsConfig.Y1);
                    newZ = Config.Stream.GetInt16(triangleAddress + TriangleOffsetsConfig.Z1);
                    break;

                case 2:
                    newX = Config.Stream.GetInt16(triangleAddress + TriangleOffsetsConfig.X2);
                    newY = Config.Stream.GetInt16(triangleAddress + TriangleOffsetsConfig.Y2);
                    newZ = Config.Stream.GetInt16(triangleAddress + TriangleOffsetsConfig.Z2);
                    break;

                case 3:
                    newX = Config.Stream.GetInt16(triangleAddress + TriangleOffsetsConfig.X3);
                    newY = Config.Stream.GetInt16(triangleAddress + TriangleOffsetsConfig.Y3);
                    newZ = Config.Stream.GetInt16(triangleAddress + TriangleOffsetsConfig.Z3);
                    break;

                default:
                    throw new Exception("There are only 3 vertices in a triangle. You are an idiot :).");
            }

            if (_useMisalignmentOffset)
            {
                newX += (newX >= 0) ? 0.5f : -0.5f;
                newZ += (newZ >= 0) ? 0.5f : -0.5f;
            }

            // Move mario to triangle (while in same Pu)
            return PuUtilities.MoveToInCurrentPu(newX, newY, newZ);
        }

        public static bool RetrieveTriangle(uint triangleAddress)
        {
            if (triangleAddress == 0x0000)
                return false;

            float normX, normY, normZ, oldNormOffset;
            normX = Config.Stream.GetSingle(triangleAddress + TriangleOffsetsConfig.NormX);
            normY = Config.Stream.GetSingle(triangleAddress + TriangleOffsetsConfig.NormY);
            normZ = Config.Stream.GetSingle(triangleAddress + TriangleOffsetsConfig.NormZ);
            oldNormOffset = Config.Stream.GetSingle(triangleAddress + TriangleOffsetsConfig.NormOffset);

            // Get Mario position
            float marioX, marioY, marioZ;
            marioX = Config.Stream.GetSingle(MarioConfig.StructAddress + MarioConfig.XOffset);
            marioY = Config.Stream.GetSingle(MarioConfig.StructAddress + MarioConfig.YOffset);
            marioZ = Config.Stream.GetSingle(MarioConfig.StructAddress + MarioConfig.ZOffset);

            float normOffset = -(normX * marioX + normY * marioY + normZ * marioZ);
            float normDiff = normOffset - oldNormOffset;

            short yOffset = (short)(-normDiff * normY);

            short v1Y, v2Y, v3Y;
            v1Y = (short)(Config.Stream.GetInt16(triangleAddress + TriangleOffsetsConfig.Y1) + yOffset);
            v2Y = (short)(Config.Stream.GetInt16(triangleAddress + TriangleOffsetsConfig.Y2) + yOffset);
            v3Y = (short)(Config.Stream.GetInt16(triangleAddress + TriangleOffsetsConfig.Y3) + yOffset);

            short yMin = (short)(Math.Min(Math.Min(v1Y, v2Y), v3Y) - 5);
            short yMax = (short)(Math.Max(Math.Max(v1Y, v2Y), v3Y) + 5);

            bool success = true;
            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            success &= Config.Stream.SetValue(v1Y, triangleAddress + TriangleOffsetsConfig.Y1);
            success &= Config.Stream.SetValue(v2Y, triangleAddress + TriangleOffsetsConfig.Y2);
            success &= Config.Stream.SetValue(v3Y, triangleAddress + TriangleOffsetsConfig.Y3);
            success &= Config.Stream.SetValue(yMin, triangleAddress + TriangleOffsetsConfig.YMin);
            success &= Config.Stream.SetValue(yMax, triangleAddress + TriangleOffsetsConfig.YMax);
            success &= Config.Stream.SetValue(normOffset, triangleAddress + TriangleOffsetsConfig.NormOffset);

            if (!streamAlreadySuspended) Config.Stream.Resume();
            return success;
        }

        public static bool NeutralizeTriangle(uint triangleAddress, bool? use21Nullable = null)
        {
            if (triangleAddress == 0x0000)
                return false;


            short neutralizeValue = OptionsConfig.NeutralizeTriangleValue(use21Nullable);

            bool success = true;
            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            success &= Config.Stream.SetValue(neutralizeValue, triangleAddress + TriangleOffsetsConfig.SurfaceType);

            if (!streamAlreadySuspended) Config.Stream.Resume();
            return success;
        }

        public static bool DisableCamCollisionForTriangle(uint triangleAddress)
        {
            if (triangleAddress == 0x0000)
                return false;

            bool success = true;
            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            byte oldFlags = Config.Stream.GetByte(triangleAddress + TriangleOffsetsConfig.Flags);
            byte newFlags = MoreMath.ApplyValueToMaskedByte(oldFlags, TriangleOffsetsConfig.NoCamCollisionMask, true);
            success &= Config.Stream.SetValue(newFlags, triangleAddress + TriangleOffsetsConfig.Flags);

            if (!streamAlreadySuspended) Config.Stream.Resume();
            return success;
        }

        public static bool AnnihilateTriangle(uint triangleAddress)
        {
            if (triangleAddress == 0x0000)
                return false;

            short xzCoordinate = 16000;
            short yCoordinate = 30000;
            short v1X = xzCoordinate;
            short v1Y = yCoordinate;
            short v1Z = xzCoordinate;
            short v2X = xzCoordinate;
            short v2Y = yCoordinate;
            short v2Z = xzCoordinate;
            short v3X = xzCoordinate;
            short v3Y = yCoordinate;
            short v3Z = xzCoordinate;
            float normX = 0;
            float normY = 0;
            float normZ = 0;
            float normOffset = 16000;

            bool success = true;
            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            success &= Config.Stream.SetValue(v1X, triangleAddress + TriangleOffsetsConfig.X1);
            success &= Config.Stream.SetValue(v1Y, triangleAddress + TriangleOffsetsConfig.Y1);
            success &= Config.Stream.SetValue(v1Z, triangleAddress + TriangleOffsetsConfig.Z1);
            success &= Config.Stream.SetValue(v2X, triangleAddress + TriangleOffsetsConfig.X2);
            success &= Config.Stream.SetValue(v2Y, triangleAddress + TriangleOffsetsConfig.Y2);
            success &= Config.Stream.SetValue(v2Z, triangleAddress + TriangleOffsetsConfig.Z2);
            success &= Config.Stream.SetValue(v3X, triangleAddress + TriangleOffsetsConfig.X3);
            success &= Config.Stream.SetValue(v3Y, triangleAddress + TriangleOffsetsConfig.Y3);
            success &= Config.Stream.SetValue(v3Z, triangleAddress + TriangleOffsetsConfig.Z3);
            success &= Config.Stream.SetValue(normX, triangleAddress + TriangleOffsetsConfig.NormX);
            success &= Config.Stream.SetValue(normY, triangleAddress + TriangleOffsetsConfig.NormY);
            success &= Config.Stream.SetValue(normZ, triangleAddress + TriangleOffsetsConfig.NormZ);
            success &= Config.Stream.SetValue(normOffset, triangleAddress + TriangleOffsetsConfig.NormOffset);

            if (!streamAlreadySuspended) Config.Stream.Resume();
            return success;
        }

        public static bool MoveTriangle(uint triangleAddress,
            float xOffset, float yOffset, float zOffset, bool useRelative)
        {
            if (triangleAddress == 0x0000)
                return false;

            HandleScaling(ref xOffset, ref zOffset);

            float normX, normY, normZ, oldNormOffset;
            normX = Config.Stream.GetSingle(triangleAddress + TriangleOffsetsConfig.NormX);
            normY = Config.Stream.GetSingle(triangleAddress + TriangleOffsetsConfig.NormY);
            normZ = Config.Stream.GetSingle(triangleAddress + TriangleOffsetsConfig.NormZ);
            oldNormOffset = Config.Stream.GetSingle(triangleAddress + TriangleOffsetsConfig.NormOffset);

            ushort relativeAngle = MoreMath.getUphillAngle(normX, normY, normZ);
            HandleRelativeAngle(ref xOffset, ref zOffset, useRelative, relativeAngle);

            float newNormOffset = oldNormOffset - normX * xOffset - normY * yOffset - normZ * zOffset;

            short newX1, newY1, newZ1, newX2, newY2, newZ2, newX3, newY3, newZ3;
            newX1 = (short)(Config.Stream.GetInt16(triangleAddress + TriangleOffsetsConfig.X1) + xOffset);
            newY1 = (short)(Config.Stream.GetInt16(triangleAddress + TriangleOffsetsConfig.Y1) + yOffset);
            newZ1 = (short)(Config.Stream.GetInt16(triangleAddress + TriangleOffsetsConfig.Z1) + zOffset);
            newX2 = (short)(Config.Stream.GetInt16(triangleAddress + TriangleOffsetsConfig.X2) + xOffset);
            newY2 = (short)(Config.Stream.GetInt16(triangleAddress + TriangleOffsetsConfig.Y2) + yOffset);
            newZ2 = (short)(Config.Stream.GetInt16(triangleAddress + TriangleOffsetsConfig.Z2) + zOffset);
            newX3 = (short)(Config.Stream.GetInt16(triangleAddress + TriangleOffsetsConfig.X3) + xOffset);
            newY3 = (short)(Config.Stream.GetInt16(triangleAddress + TriangleOffsetsConfig.Y3) + yOffset);
            newZ3 = (short)(Config.Stream.GetInt16(triangleAddress + TriangleOffsetsConfig.Z3) + zOffset);

            short newYMin = (short)(Math.Min(Math.Min(newY1, newY2), newY3) - 5);
            short newYMax = (short)(Math.Max(Math.Max(newY1, newY2), newY3) + 5);

            bool success = true;
            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            success &= Config.Stream.SetValue(newNormOffset, triangleAddress + TriangleOffsetsConfig.NormOffset);
            success &= Config.Stream.SetValue(newX1, triangleAddress + TriangleOffsetsConfig.X1);
            success &= Config.Stream.SetValue(newY1, triangleAddress + TriangleOffsetsConfig.Y1);
            success &= Config.Stream.SetValue(newZ1, triangleAddress + TriangleOffsetsConfig.Z1);
            success &= Config.Stream.SetValue(newX2, triangleAddress + TriangleOffsetsConfig.X2);
            success &= Config.Stream.SetValue(newY2, triangleAddress + TriangleOffsetsConfig.Y2);
            success &= Config.Stream.SetValue(newZ2, triangleAddress + TriangleOffsetsConfig.Z2);
            success &= Config.Stream.SetValue(newX3, triangleAddress + TriangleOffsetsConfig.X3);
            success &= Config.Stream.SetValue(newY3, triangleAddress + TriangleOffsetsConfig.Y3);
            success &= Config.Stream.SetValue(newZ3, triangleAddress + TriangleOffsetsConfig.Z3);
            success &= Config.Stream.SetValue(newYMin, triangleAddress + TriangleOffsetsConfig.YMin);
            success &= Config.Stream.SetValue(newYMax, triangleAddress + TriangleOffsetsConfig.YMax);

            if (!streamAlreadySuspended) Config.Stream.Resume();
            return success;
        }

        public static bool MoveTriangleNormal(uint triangleAddress, float normalChange)
        {
            if (triangleAddress == 0x0000)
                return false;

            float normX, normY, normZ, oldNormOffset;
            normX = Config.Stream.GetSingle(triangleAddress + TriangleOffsetsConfig.NormX);
            normY = Config.Stream.GetSingle(triangleAddress + TriangleOffsetsConfig.NormY);
            normZ = Config.Stream.GetSingle(triangleAddress + TriangleOffsetsConfig.NormZ);
            oldNormOffset = Config.Stream.GetSingle(triangleAddress + TriangleOffsetsConfig.NormOffset);

            float newNormOffset = oldNormOffset - normalChange;

            double xChange = normalChange * normX;
            double yChange = normalChange * normY;
            double zChange = normalChange * normZ;

            short newX1, newY1, newZ1, newX2, newY2, newZ2, newX3, newY3, newZ3;
            newX1 = (short)(Config.Stream.GetInt16(triangleAddress + TriangleOffsetsConfig.X1) + xChange);
            newY1 = (short)(Config.Stream.GetInt16(triangleAddress + TriangleOffsetsConfig.Y1) + yChange);
            newZ1 = (short)(Config.Stream.GetInt16(triangleAddress + TriangleOffsetsConfig.Z1) + zChange);
            newX2 = (short)(Config.Stream.GetInt16(triangleAddress + TriangleOffsetsConfig.X2) + xChange);
            newY2 = (short)(Config.Stream.GetInt16(triangleAddress + TriangleOffsetsConfig.Y2) + yChange);
            newZ2 = (short)(Config.Stream.GetInt16(triangleAddress + TriangleOffsetsConfig.Z2) + zChange);
            newX3 = (short)(Config.Stream.GetInt16(triangleAddress + TriangleOffsetsConfig.X3) + xChange);
            newY3 = (short)(Config.Stream.GetInt16(triangleAddress + TriangleOffsetsConfig.Y3) + yChange);
            newZ3 = (short)(Config.Stream.GetInt16(triangleAddress + TriangleOffsetsConfig.Z3) + zChange);

            short newYMin = (short)(Math.Min(Math.Min(newY1, newY2), newY3) - 5);
            short newYMax = (short)(Math.Max(Math.Max(newY1, newY2), newY3) + 5);

            bool success = true;
            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            success &= Config.Stream.SetValue(newNormOffset, triangleAddress + TriangleOffsetsConfig.NormOffset);
            success &= Config.Stream.SetValue(newX1, triangleAddress + TriangleOffsetsConfig.X1);
            success &= Config.Stream.SetValue(newY1, triangleAddress + TriangleOffsetsConfig.Y1);
            success &= Config.Stream.SetValue(newZ1, triangleAddress + TriangleOffsetsConfig.Z1);
            success &= Config.Stream.SetValue(newX2, triangleAddress + TriangleOffsetsConfig.X2);
            success &= Config.Stream.SetValue(newY2, triangleAddress + TriangleOffsetsConfig.Y2);
            success &= Config.Stream.SetValue(newZ2, triangleAddress + TriangleOffsetsConfig.Z2);
            success &= Config.Stream.SetValue(newX3, triangleAddress + TriangleOffsetsConfig.X3);
            success &= Config.Stream.SetValue(newY3, triangleAddress + TriangleOffsetsConfig.Y3);
            success &= Config.Stream.SetValue(newZ3, triangleAddress + TriangleOffsetsConfig.Z3);
            success &= Config.Stream.SetValue(newYMin, triangleAddress + TriangleOffsetsConfig.YMin);
            success &= Config.Stream.SetValue(newYMax, triangleAddress + TriangleOffsetsConfig.YMax);

            if (!streamAlreadySuspended) Config.Stream.Resume();
            return success;
        }

        public static bool TranslateCamera(float xOffset, float yOffset, float zOffset, bool useRelative)
        {
            List<TripleAddressAngle> posAddressAngles =
                new List<TripleAddressAngle> {
                    new TripleAddressAngle(
                        CameraConfig.CameraStructAddress + CameraConfig.XOffset,
                        CameraConfig.CameraStructAddress + CameraConfig.YOffset,
                        CameraConfig.CameraStructAddress + CameraConfig.ZOffset,
                        Config.Stream.GetUInt16(CameraConfig.CameraStructAddress + CameraConfig.YawFacingOffset))
                };

            return ChangeValues(posAddressAngles, xOffset, yOffset, zOffset, Change.ADD, useRelative);
        }

        public static bool TranslateCameraSpherically(float radiusOffset, float thetaOffset, float phiOffset, (float, float, float) pivotPoint)
        {
            float pivotX, pivotY, pivotZ;
            (pivotX, pivotY, pivotZ) = pivotPoint;

            HandleScaling(ref thetaOffset, ref phiOffset);

            float oldX, oldY, oldZ;
            oldX = Config.Stream.GetSingle(CameraConfig.CameraStructAddress + CameraConfig.XOffset);
            oldY = Config.Stream.GetSingle(CameraConfig.CameraStructAddress + CameraConfig.YOffset);
            oldZ = Config.Stream.GetSingle(CameraConfig.CameraStructAddress + CameraConfig.ZOffset);

            double newX, newY, newZ;
            (newX, newY, newZ) = MoreMath.OffsetSphericallyAboutPivot(oldX, oldY, oldZ, radiusOffset, thetaOffset, phiOffset, pivotX, pivotY, pivotZ);

            bool success = true;
            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            success &= Config.Stream.SetValue((float)newX, CameraConfig.CameraStructAddress + CameraConfig.XOffset);
            success &= Config.Stream.SetValue((float)newY, CameraConfig.CameraStructAddress + CameraConfig.YOffset);
            success &= Config.Stream.SetValue((float)newZ, CameraConfig.CameraStructAddress + CameraConfig.ZOffset);

            if (!streamAlreadySuspended) Config.Stream.Resume();
            return success;
        }

        private static ushort getCamHackYawFacing(CamHackMode camHackMode)
        {
            switch (camHackMode)
            {
                case CamHackMode.REGULAR:
                    return Config.Stream.GetUInt16(CameraConfig.CameraStructAddress + CameraConfig.YawFacingOffset);

                case CamHackMode.RELATIVE_ANGLE:
                case CamHackMode.ABSOLUTE_ANGLE:
                case CamHackMode.FIXED_POS:
                case CamHackMode.FIXED_ORIENTATION:
                    float camHackPosX = Config.Stream.GetSingle(CameraHackConfig.CameraHackStruct + CameraHackConfig.CameraXOffset);
                    float camHackPosZ = Config.Stream.GetSingle(CameraHackConfig.CameraHackStruct + CameraHackConfig.CameraZOffset);
                    float camHackFocusX = Config.Stream.GetSingle(CameraHackConfig.CameraHackStruct + CameraHackConfig.FocusXOffset);
                    float camHackFocusZ = Config.Stream.GetSingle(CameraHackConfig.CameraHackStruct + CameraHackConfig.FocusZOffset);
                    return MoreMath.AngleTo_AngleUnitsRounded(camHackPosX, camHackPosZ, camHackFocusX, camHackFocusZ);

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static TripleAddressAngle getCamHackFocusTripleAddressController(CamHackMode camHackMode)
        {
            uint camHackObject = Config.Stream.GetUInt32(CameraHackConfig.CameraHackStruct + CameraHackConfig.ObjectOffset);
            switch (camHackMode)
            {
                case CamHackMode.REGULAR:
                    return new TripleAddressAngle(
                        CameraConfig.CameraStructAddress + CameraConfig.FocusXOffset,
                        CameraConfig.CameraStructAddress + CameraConfig.FocusYOffset,
                        CameraConfig.CameraStructAddress + CameraConfig.FocusZOffset,
                        getCamHackYawFacing(camHackMode));
                
                case CamHackMode.RELATIVE_ANGLE:
                case CamHackMode.ABSOLUTE_ANGLE:
                case CamHackMode.FIXED_POS:
                    if (camHackObject == 0) // focused on Mario
                    {
                        return new TripleAddressAngle(
                            MarioConfig.StructAddress + MarioConfig.XOffset,
                            MarioConfig.StructAddress + MarioConfig.YOffset,
                            MarioConfig.StructAddress + MarioConfig.ZOffset,
                            getCamHackYawFacing(camHackMode));
                    }
                    else // focused on object
                    {
                        return new TripleAddressAngle(
                            camHackObject + ObjectConfig.XOffset,
                            camHackObject + ObjectConfig.YOffset,
                            camHackObject + ObjectConfig.ZOffset,
                            getCamHackYawFacing(camHackMode));
                    }
                
                case CamHackMode.FIXED_ORIENTATION:
                    return new TripleAddressAngle(
                        CameraHackConfig.CameraHackStruct + CameraHackConfig.FocusXOffset,
                        CameraHackConfig.CameraHackStruct + CameraHackConfig.FocusYOffset,
                        CameraHackConfig.CameraHackStruct + CameraHackConfig.FocusZOffset,
                        getCamHackYawFacing(camHackMode));

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static bool TranslateCameraHack(CamHackMode camHackMode, float xOffset, float yOffset, float zOffset, bool useRelative)
        {
            switch (camHackMode)
            {
                case CamHackMode.REGULAR:
                {
                    return TranslateCamera(xOffset, yOffset, zOffset, useRelative);
                }

                case CamHackMode.FIXED_POS:
                case CamHackMode.FIXED_ORIENTATION:
                {
                    return ChangeValues(
                        new List<TripleAddressAngle> {
                            new TripleAddressAngle(
                                CameraHackConfig.CameraHackStruct + CameraHackConfig.CameraXOffset,
                                CameraHackConfig.CameraHackStruct + CameraHackConfig.CameraYOffset,
                                CameraHackConfig.CameraHackStruct + CameraHackConfig.CameraZOffset,
                                getCamHackYawFacing(camHackMode))
                        },
                        xOffset,
                        yOffset,
                        zOffset,
                        Change.ADD,
                        useRelative);
                }

                case CamHackMode.RELATIVE_ANGLE:
                case CamHackMode.ABSOLUTE_ANGLE:
                {
                    HandleScaling(ref xOffset, ref zOffset);

                    HandleRelativeAngle(ref xOffset, ref zOffset, useRelative, getCamHackYawFacing(camHackMode));
                    float xDestination = xOffset + Config.Stream.GetSingle(CameraHackConfig.CameraHackStruct + CameraHackConfig.CameraXOffset);
                    float yDestination = yOffset + Config.Stream.GetSingle(CameraHackConfig.CameraHackStruct + CameraHackConfig.CameraYOffset);
                    float zDestination = zOffset + Config.Stream.GetSingle(CameraHackConfig.CameraHackStruct + CameraHackConfig.CameraZOffset);

                    float xFocus = Config.Stream.GetSingle(CameraHackConfig.CameraHackStruct + CameraHackConfig.FocusXOffset);
                    float yFocus = Config.Stream.GetSingle(CameraHackConfig.CameraHackStruct + CameraHackConfig.FocusYOffset);
                    float zFocus = Config.Stream.GetSingle(CameraHackConfig.CameraHackStruct + CameraHackConfig.FocusZOffset);

                    double radius, theta, height;
                    (radius, theta, height) = MoreMath.EulerToCylindricalAboutPivot(xDestination, yDestination, zDestination, xFocus, yFocus, zFocus);

                    ushort relativeYawOffset = 0;
                    if (camHackMode == CamHackMode.RELATIVE_ANGLE)
                    {
                        uint camHackObject = Config.Stream.GetUInt32(CameraHackConfig.CameraHackStruct + CameraHackConfig.ObjectOffset);
                        relativeYawOffset = camHackObject == 0
                            ? Config.Stream.GetUInt16(MarioConfig.StructAddress + MarioConfig.YawFacingOffset)
                            : Config.Stream.GetUInt16(camHackObject + ObjectConfig.YawFacingOffset);
                    }

                    bool success = true;
                    bool streamAlreadySuspended = Config.Stream.IsSuspended;
                    if (!streamAlreadySuspended) Config.Stream.Suspend();

                    success &= Config.Stream.SetValue((float)radius, CameraHackConfig.CameraHackStruct + CameraHackConfig.RadiusOffset);
                    success &= Config.Stream.SetValue(MoreMath.NormalizeAngleUshort(theta + 32768 - relativeYawOffset), CameraHackConfig.CameraHackStruct + CameraHackConfig.ThetaOffset);
                    success &= Config.Stream.SetValue((float)height, CameraHackConfig.CameraHackStruct + CameraHackConfig.RelativeHeightOffset);

                    if (!streamAlreadySuspended) Config.Stream.Resume();
                    return success;
                }

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static bool TranslateCameraHackSpherically(CamHackMode camHackMode, float radiusOffset, float thetaOffset, float phiOffset)
        {
            switch (camHackMode)
            {
                case CamHackMode.REGULAR:
                {
                    float xFocus = Config.Stream.GetSingle(CameraConfig.CameraStructAddress + CameraConfig.FocusXOffset);
                    float yFocus = Config.Stream.GetSingle(CameraConfig.CameraStructAddress + CameraConfig.FocusYOffset);
                    float zFocus = Config.Stream.GetSingle(CameraConfig.CameraStructAddress + CameraConfig.FocusZOffset);
                    return TranslateCameraSpherically(radiusOffset, thetaOffset, phiOffset, (xFocus, yFocus, zFocus));
                }

                case CamHackMode.FIXED_POS:
                case CamHackMode.FIXED_ORIENTATION:
                {
                    HandleScaling(ref thetaOffset, ref phiOffset);

                    TripleAddressAngle focusTripleAddressAngle = getCamHackFocusTripleAddressController(camHackMode);
                    uint focusXAddress, focusYAddress, focusZAddress;
                    (focusXAddress, focusYAddress, focusZAddress) = focusTripleAddressAngle.GetTripleAddress();

                    float xFocus = Config.Stream.GetSingle(focusTripleAddressAngle.XAddress);
                    float yFocus = Config.Stream.GetSingle(focusTripleAddressAngle.YAddress);
                    float zFocus = Config.Stream.GetSingle(focusTripleAddressAngle.ZAddress);

                    float xCamPos = Config.Stream.GetSingle(CameraHackConfig.CameraHackStruct + CameraHackConfig.CameraXOffset);
                    float yCamPos = Config.Stream.GetSingle(CameraHackConfig.CameraHackStruct + CameraHackConfig.CameraYOffset);
                    float zCamPos = Config.Stream.GetSingle(CameraHackConfig.CameraHackStruct + CameraHackConfig.CameraZOffset);

                    double xDestination, yDestination, zDestination;
                    (xDestination, yDestination, zDestination) =
                        MoreMath.OffsetSphericallyAboutPivot(xCamPos, yCamPos, zCamPos, radiusOffset, thetaOffset, phiOffset, xFocus, yFocus, zFocus);

                    return ChangeValues(
                        new List<TripleAddressAngle> {
                            new TripleAddressAngle(
                                CameraHackConfig.CameraHackStruct + CameraHackConfig.CameraXOffset,
                                CameraHackConfig.CameraHackStruct + CameraHackConfig.CameraYOffset,
                                CameraHackConfig.CameraHackStruct + CameraHackConfig.CameraZOffset)
                        },
                        (float)xDestination,
                        (float)yDestination,
                        (float)zDestination,
                        Change.SET);
                }

                case CamHackMode.RELATIVE_ANGLE:
                case CamHackMode.ABSOLUTE_ANGLE:
                {
                    HandleScaling(ref thetaOffset, ref phiOffset);

                    float xCamPos = Config.Stream.GetSingle(CameraHackConfig.CameraHackStruct + CameraHackConfig.CameraXOffset);
                    float yCamPos = Config.Stream.GetSingle(CameraHackConfig.CameraHackStruct + CameraHackConfig.CameraYOffset);
                    float zCamPos = Config.Stream.GetSingle(CameraHackConfig.CameraHackStruct + CameraHackConfig.CameraZOffset);

                    float xFocus = Config.Stream.GetSingle(CameraHackConfig.CameraHackStruct + CameraHackConfig.FocusXOffset);
                    float yFocus = Config.Stream.GetSingle(CameraHackConfig.CameraHackStruct + CameraHackConfig.FocusYOffset);
                    float zFocus = Config.Stream.GetSingle(CameraHackConfig.CameraHackStruct + CameraHackConfig.FocusZOffset);

                    double xDestination, yDestination, zDestination;
                    (xDestination, yDestination, zDestination) =
                        MoreMath.OffsetSphericallyAboutPivot(xCamPos, yCamPos, zCamPos, radiusOffset, thetaOffset, phiOffset, xFocus, yFocus, zFocus);

                    double radius, theta, height;
                    (radius, theta, height) = MoreMath.EulerToCylindricalAboutPivot(xDestination, yDestination, zDestination, xFocus, yFocus, zFocus);

                    ushort relativeYawOffset = 0;
                    if (camHackMode == CamHackMode.RELATIVE_ANGLE)
                    {
                        uint camHackObject = Config.Stream.GetUInt32(CameraHackConfig.CameraHackStruct + CameraHackConfig.ObjectOffset);
                        relativeYawOffset = camHackObject == 0
                            ? Config.Stream.GetUInt16(MarioConfig.StructAddress + MarioConfig.YawFacingOffset)
                            : Config.Stream.GetUInt16(camHackObject + ObjectConfig.YawFacingOffset);
                    }

                    bool success = true;
                    bool streamAlreadySuspended = Config.Stream.IsSuspended;
                    if (!streamAlreadySuspended) Config.Stream.Suspend();

                    success &= Config.Stream.SetValue((float)radius, CameraHackConfig.CameraHackStruct + CameraHackConfig.RadiusOffset);
                    success &= Config.Stream.SetValue(MoreMath.NormalizeAngleUshort(theta + 32768 - relativeYawOffset), CameraHackConfig.CameraHackStruct + CameraHackConfig.ThetaOffset);
                    success &= Config.Stream.SetValue((float)height, CameraHackConfig.CameraHackStruct + CameraHackConfig.RelativeHeightOffset);

                    if (!streamAlreadySuspended) Config.Stream.Resume();
                    return success;
                }

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static bool TranslateCameraHackFocus(CamHackMode camHackMode, float xOffset, float yOffset, float zOffset, bool useRelative)
        {
            return ChangeValues(
                new List<TripleAddressAngle> { getCamHackFocusTripleAddressController(camHackMode) },
                xOffset,
                yOffset,
                zOffset,
                Change.ADD,
                useRelative);
        }

        public static bool TranslateCameraHackFocusSpherically(CamHackMode camHackMode, float radiusOffset, float thetaOffset, float phiOffset)
        {
            HandleScaling(ref thetaOffset, ref phiOffset);

            TripleAddressAngle focusTripleAddressAngle = getCamHackFocusTripleAddressController(camHackMode);
            uint focusXAddress, focusYAddress, focusZAddress;
            (focusXAddress, focusYAddress, focusZAddress) = focusTripleAddressAngle.GetTripleAddress();

            float xFocus = Config.Stream.GetSingle(focusTripleAddressAngle.XAddress);
            float yFocus = Config.Stream.GetSingle(focusTripleAddressAngle.YAddress);
            float zFocus = Config.Stream.GetSingle(focusTripleAddressAngle.ZAddress);

            float xCamPos = Config.Stream.GetSingle(CameraHackConfig.CameraHackStruct + CameraHackConfig.CameraXOffset);
            float yCamPos = Config.Stream.GetSingle(CameraHackConfig.CameraHackStruct + CameraHackConfig.CameraYOffset);
            float zCamPos = Config.Stream.GetSingle(CameraHackConfig.CameraHackStruct + CameraHackConfig.CameraZOffset);

            double xDestination, yDestination, zDestination;
            (xDestination, yDestination, zDestination) =
                MoreMath.OffsetSphericallyAboutPivot(xFocus, yFocus, zFocus, radiusOffset, thetaOffset, phiOffset, xCamPos, yCamPos, zCamPos);

            return ChangeValues(
                new List<TripleAddressAngle> { focusTripleAddressAngle },
                (float)xDestination,
                (float)yDestination,
                (float)zDestination,
                Change.SET);
        }

        public static bool TranslateCameraHackBoth(CamHackMode camHackMode, float xOffset, float yOffset, float zOffset, bool useRelative)
        {
            bool success = true;
            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            if (camHackMode != CamHackMode.RELATIVE_ANGLE && camHackMode != CamHackMode.ABSOLUTE_ANGLE)
            {
                success &= TranslateCameraHack(camHackMode, xOffset, yOffset, zOffset, useRelative);
            }
            success &= TranslateCameraHackFocus(camHackMode, xOffset, yOffset, zOffset, useRelative);

            if (!streamAlreadySuspended) Config.Stream.Resume();
            return success;
        }

        public static bool SetHudVisibility(bool hudOn)
        {
            byte currentHudVisibility = Config.Stream.GetByte(MarioConfig.StructAddress + HudConfig.VisibilityOffset);
            byte newHudVisibility = MoreMath.ApplyValueToMaskedByte(currentHudVisibility, HudConfig.VisibilityMask, hudOn);

            bool success = true;
            bool streamAlreadySuspended = Config.Stream.IsSuspended;
            if (!streamAlreadySuspended) Config.Stream.Suspend();

            success &= Config.Stream.SetValue(newHudVisibility, MarioConfig.StructAddress + HudConfig.VisibilityOffset);
            success &= Config.Stream.SetValue((short)(hudOn ? 1 : 0), MiscConfig.LevelIndexAddress);

            if (!streamAlreadySuspended) Config.Stream.Resume();
            return success;
        }
    }
}
