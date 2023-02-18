using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;

using ACE.Common;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.Physics;
using ACE.Server.Physics.Common;

namespace ACE.Server.WorldObjects
{
    partial class WorldObject
    {
        private Dictionary<ObjectGuid, double> AwareList;
        private double NextAwarenessCheck;

        private static int AwarenessCheckInterval = 15;
        private static int RangeSquared = 50 * 50;
        private static int VanishRangeSquared = 20 * 20;

        public void AwarenessHeartbeat(double currentUnixTime)
        {
            if (NextAwarenessCheck <= currentUnixTime)
            {
                NextAwarenessCheck = Time.GetFutureUnixTime(AwarenessCheckInterval);

                CheckAwareness();

                if (AwareList != null && AwareList.Count > 0)
                {
                    var newList = new Dictionary<ObjectGuid, double>();
                    foreach (var entry in AwareList)
                    {
                        if (entry.Value < currentUnixTime)
                        {
                            var player = CurrentLandblock.GetObject(entry.Key) as Player;
                            if (player != null)
                            {
                                var distSquared = Location.SquaredDistanceTo(player.Location);
                                if (distSquared > VanishRangeSquared)
                                    player.RemoveTrackedObject(this, false);
                                else
                                    newList.Add(entry.Key, entry.Value);
                            }
                        }
                        else
                            newList.Add(entry.Key, entry.Value);
                    }

                    AwareList = newList;
                }
            }
        }

        public void CheckAwareness()
        {
            var isDungeon = CurrentLandblock.PhysicsLandblock != null && CurrentLandblock.PhysicsLandblock.IsDungeon;
            foreach (var player in PhysicsObj.ObjMaint.GetKnownPlayersValuesAsPlayer())
            {
                if (isDungeon && Location.Landblock != player.Location.Landblock)
                    continue;

                var distSquared = Location.SquaredDistanceTo(player.Location);
                if (distSquared <= RangeSquared)
                {
                    if (player.TestAwareness(this))
                        MakeAware(player);
                }
            }
        }

        public bool IsAware(Player player)
        {
            if (AwareList == null)
                return false;

            if (!ResistAwareness.HasValue)
                return false;

            if ((Tier ?? 0) == 0)
                return false;

            return AwareList.ContainsKey(player.Guid);
        }

        public void MakeAware(Player player)
        {
            if (AwareList == null)
                AwareList = new Dictionary<ObjectGuid, double>();

            var expireTime = Time.GetFutureUnixTime(AwarenessCheckInterval);
            if (AwareList.ContainsKey(player.Guid))
                AwareList[player.Guid] = expireTime;
            else
            {
                AwareList.Add(player.Guid, expireTime);
                player.PhysicsObj.enqueue_obj(PhysicsObj);
            }
        }
    }
}
