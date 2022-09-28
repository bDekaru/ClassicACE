using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using ACE.Database.Models.World;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using log4net;
using static System.Collections.Specialized.BitVector32;

namespace ACE.Database.SQLFormatters.World
{
    public class LandblockInstanceWriter : SQLWriter
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Default is formed from: (input.ObjCellId >> 16).ToString("X4")
        /// </summary>
        public static string GetDefaultFileName(LandblockInstance input)
        {
            var landblock = input.ObjCellId >> 16;
            return GetDefaultFileName(landblock);
        }

        public static string GetDefaultFileName(uint landblockId)
        {
            var name = GetNameFromPortalDestination(landblockId);

            string fileName;
            if (name != "")
                fileName = $"{landblockId.ToString("X4")} - {name}";
            else
                fileName = landblockId.ToString("X4");
            fileName = IllegalInFileName.Replace(fileName, "_");
            fileName += ".sql";
            return fileName;
        }

        private class NameResults
        {
            public Models.World.Weenie Weenie{ get; set; }
            public WeeniePropertiesPosition Dest { get; set; }
            public WeeniePropertiesString Name { get; set; }
        }

        private static List<NameResults> Results = null;

        public static string GetNameFromPortalDestination(uint landblock)
        {
            using (var ctx = new WorldDbContext())
            {
                if (Results == null)
                {
                    var query = from weenie in ctx.Weenie
                                join wdest in ctx.WeeniePropertiesPosition on weenie.ClassId equals wdest.ObjectId
                                join wname in ctx.WeeniePropertiesString on weenie.ClassId equals wname.ObjectId
                                where weenie.Type == (int)WeenieType.Portal && wdest.PositionType == (int)PositionType.Destination && wname.Type == (int)PropertyString.Name
                                select new NameResults
                                {
                                    Weenie = weenie,
                                    Dest = wdest,
                                    Name = wname
                                };

                    Results = query.ToList();
                }

                var resultsTest = Results.Where(i => i.Dest.ObjCellId >> 16 == landblock).ToList();

                var name = Results.Where(i => i.Dest.ObjCellId >> 16 == landblock && i.Name != null && !i.Name.Value.Contains("Surface") && !i.Name.Value.Contains("Gateway") && !i.Name.Value.Contains("Exit")).Select(i => i.Name.Value).FirstOrDefault();

                if (name == null)
                {
                    var resultsAlternative = Results.Where(i => i.Dest.ObjCellId >> 16 == landblock).ToList();
                    foreach (var entry in resultsAlternative)
                    {
                        var portalInstances = DatabaseManager.World.GetLandblockInstancesByWcid(entry.Weenie.ClassId);
                        foreach (var instance in portalInstances)
                        {
                            var dungeonLandblock = instance.ObjCellId >> 16;
                            name = Results.Where(i => i.Dest.ObjCellId >> 16 == dungeonLandblock && i.Name != null && !i.Name.Value.Contains("Surface") && !i.Name.Value.Contains("Gateway") && !i.Name.Value.StartsWith("Exit")).Select(i => i.Name.Value).FirstOrDefault();
                            if (name != null)
                            {
                                name = $"Surface around {name}";
                                break;
                            }
                        }

                        if (name != null)
                            break;
                    }

                    if (name == null)
                        return "";
                }

                if (name.StartsWith("Portal to "))
                    name = name.Replace("Portal to ", "");
                else if (name.EndsWith(" Portal"))
                    name = name.Replace(" Portal", "");
                else if (name.StartsWith("Way Back to "))
                    name = name.Replace("Way Back to ", "");
                else if (name.EndsWith(" Exit"))
                    name = name.Replace(" Exit", "");

                return name;
            }
        }

        public void CreateSQLDELETEStatement(IList<LandblockInstance> input, StreamWriter writer)
        {
            writer.WriteLine($"DELETE FROM `landblock_instance` WHERE `landblock` = 0x{(input[0].ObjCellId >> 16):X4};");
        }

        /// <exception cref="System.Exception">WeenieClassNames must be set, and must have a record for input.ClassId.</exception>
        public void CreateSQLINSERTStatement(IList<LandblockInstance> input, StreamWriter writer)
        {
            var instanceWcids = input.ToDictionary(i => i.Guid, i => i.WeenieClassId);

            input = input.OrderBy(r => r.Guid).ToList();

            foreach (var value in input)
            {
                if (value != input[0])
                    writer.WriteLine();

                writer.WriteLine("INSERT INTO `landblock_instance` (`guid`, `weenie_Class_Id`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`, `is_Link_Child`, `last_Modified`)");

                string label = null;
                string weenieName = null;
                string className = null;
                var level = 0;
                var type = 0;

                if (WeenieNames != null)
                    WeenieNames.TryGetValue(value.WeenieClassId, out weenieName);

                if (WeenieClassNames != null)
                    WeenieClassNames.TryGetValue(value.WeenieClassId, out className);

                if(weenieName != null && className != null)
                    label += weenieName + $"({value.WeenieClassId}/{className})";
                else if (weenieName != null)
                    label = weenieName + $" ({value.WeenieClassId})";
                var parentWeenieName = label;

                if (WeenieLevels != null)
                    WeenieLevels.TryGetValue(value.WeenieClassId, out level);

                if (WeenieTypes != null)
                    WeenieTypes.TryGetValue(value.WeenieClassId, out type);

                if (level > 0)
                    label += $" - Level: {level}";

                var weenie = DatabaseManager.World.GetCachedWeenie(value.WeenieClassId);

                if(weenie == null)
                {
                    var landblockId = value.ObjCellId >> 16;
                    log.Warn($"[LANDBLOCKINSTANCEWRITER] Landblock {landblockId:X4}: LandblockInstance has entry to unknown weeniedClassId: {value.WeenieClassId}");
                }
                else
                {
                    if (TreasureDeath != null)
                    {
                        var deathTreasureType = weenie.GetProperty(PropertyDataId.DeathTreasureType) ?? 0;
                        if (deathTreasureType != 0 && TreasureDeath.TryGetValue(deathTreasureType, out var treasureDeath))
                            label += $" - {(TreasureDeathDesc)treasureDeath.TreasureType} - {GetValueForTreasureData(treasureDeath.TreasureType)}";

                    }

                    if (type == (int)WeenieType.Chest || type == (int)WeenieType.Container || type == (int)WeenieType.Door)
                    {
                        var locked = weenie.GetProperty(PropertyBool.DefaultLocked) ?? false;
                        var resistLockpick = weenie.GetProperty(PropertyInt.ResistLockpick) ?? 0;
                        var key = weenie.GetProperty(PropertyString.LockCode) ?? "";

                        if (locked)
                            label += $" - Locked({resistLockpick}{(key.Length > 0 ? $"/{key})" : ")")}";

                        var content = "";
                        if (TreasureDeath != null)
                        {
                            if (weenie.PropertiesGenerator != null)
                            {
                                bool isFirst = true;
                                foreach (var entry in weenie.PropertiesGenerator)
                                {
                                    if (!isFirst)
                                        content += " / ";
                                    isFirst = false;

                                    var entryWeenie = DatabaseManager.World.GetCachedWeenie(entry.WeenieClassId);
                                    if (entry.WhereCreate.HasFlag(RegenLocationType.Treasure))
                                        content += GetValueForTreasureData(entry.WeenieClassId);
                                    else
                                    {
                                        if (entryWeenie != null)
                                        {
                                            if (WeenieNames != null)
                                            {
                                                string entryWeenieName = null;
                                                WeenieNames.TryGetValue(entry.WeenieClassId, out entryWeenieName);

                                                if (WeenieClassNames != null && WeenieClassNames.TryGetValue(entry.WeenieClassId, out var entryClassName))
                                                    content += $"{entryWeenieName}({entry.WeenieClassId}/{entryClassName})";
                                                else
                                                    content += $"{entryWeenieName}({entry.WeenieClassId})";
                                            }
                                            else
                                                content += $"({entry.WeenieClassId})";
                                        }
                                        else
                                        {
                                            content += $"({entry.WeenieClassId})";
                                            
                                            var landblockId = value.ObjCellId >> 16;
                                            log.Warn($"[LANDBLOCKINSTANCEWRITER] Landblock {landblockId}:{parentWeenieName}: Generator has entry to unknown weeniedClassId: {entry.WeenieClassId}");
                                        }
                                    }
                                }
                            }

                            if (weenie.PropertiesCreateList != null)
                            {
                                bool isFirst = true;
                                foreach (var entry in weenie.PropertiesCreateList)
                                {
                                    if (entry.WeenieClassId == 0)
                                        continue;

                                    if (!isFirst)
                                        content += " / ";
                                    isFirst = false;

                                    var entryWeenie = DatabaseManager.World.GetCachedWeenie(entry.WeenieClassId);
                                    if (entryWeenie != null)
                                    {
                                        if (WeenieNames != null)
                                        {
                                            WeenieNames.TryGetValue(entry.WeenieClassId, out var entryWeenieName);
                                            if (WeenieClassNames != null && WeenieClassNames.TryGetValue(entry.WeenieClassId, out var entryClassName))
                                                content += $"{entryWeenieName}({entry.WeenieClassId}/{entryClassName})";
                                            else
                                                content += $"{entryWeenieName}({entry.WeenieClassId})";

                                            if (WeenieLevels != null)
                                            {
                                                WeenieLevels.TryGetValue(entry.WeenieClassId, out var generatedLevel);
                                                if (generatedLevel > 0)
                                                    content += $" - Level: {generatedLevel}";
                                            }
                                        }
                                        else
                                            content += $"({entry.WeenieClassId})";
                                    }
                                    else
                                    {
                                        content += $"({entry.WeenieClassId})";

                                        var landblockId = value.ObjCellId >> 16;
                                        log.Warn($"[LANDBLOCKINSTANCEWRITER] Landblock {landblockId}:{parentWeenieName}: CreateList has entry to unknown weeniedClassId: {entry.WeenieClassId}");
                                    }
                                }
                            }
                        }

                        if (content.Length > 0)
                            label += $" - Content - {content}";
                    }
                    else// if (type == (int)WeenieType.Generic)
                    {
                        var generated = "";
                        if (TreasureDeath != null)
                        {
                            if (weenie.PropertiesGenerator != null)
                            {
                                bool isFirst = true;
                                foreach (var entry in weenie.PropertiesGenerator)
                                {
                                    if (!isFirst)
                                        generated += " / ";
                                    isFirst = false;

                                    var entryWeenie = DatabaseManager.World.GetCachedWeenie(entry.WeenieClassId);
                                    if (entry.WhereCreate.HasFlag(RegenLocationType.Treasure))
                                        generated += GetValueForTreasureData(entry.WeenieClassId);
                                    else
                                    {
                                        if (entryWeenie != null)
                                        {
                                            if (WeenieNames != null)
                                            {
                                                WeenieNames.TryGetValue(entry.WeenieClassId, out var entryWeenieName);
                                                if (WeenieClassNames != null && WeenieClassNames.TryGetValue(entry.WeenieClassId, out var entryClassName))
                                                    generated += $"{entryWeenieName}({entry.WeenieClassId}/{entryClassName})";
                                                else
                                                    generated += $"{entryWeenieName}({entry.WeenieClassId})";

                                                if (WeenieLevels != null)
                                                {
                                                    WeenieLevels.TryGetValue(entry.WeenieClassId, out var generatedLevel);
                                                    if (generatedLevel > 0)
                                                        generated += $" - Level: {generatedLevel}";
                                                }
                                            }
                                            else
                                                generated += $"({entry.WeenieClassId})";
                                        }
                                        else
                                        {
                                            generated += $"({entry.WeenieClassId})";

                                            var landblockId = value.ObjCellId >> 16;
                                            log.Warn($"[LANDBLOCKINSTANCEWRITER] Landblock {landblockId}:{parentWeenieName}: Generator has entry to unknown weeniedClassId: {entry.WeenieClassId}");
                                        }
                                    }
                                }
                            }

                            if (weenie.PropertiesCreateList != null)
                            {
                                bool isFirst = true;
                                foreach (var entry in weenie.PropertiesCreateList)
                                {
                                    if (entry.WeenieClassId == 0)
                                        continue;

                                    if (!isFirst)
                                        generated += " / ";
                                    isFirst = false;

                                    var entryWeenie = DatabaseManager.World.GetCachedWeenie(entry.WeenieClassId);
                                    if (entryWeenie != null)
                                    {
                                        if (WeenieNames != null)
                                        {
                                            WeenieNames.TryGetValue(entry.WeenieClassId, out var entryWeenieName);
                                            if (WeenieClassNames != null && WeenieClassNames.TryGetValue(entry.WeenieClassId, out var entryClassName))
                                                generated += $"{entryWeenieName}({entry.WeenieClassId}/{entryClassName})";
                                            else
                                                generated += $"{entryWeenieName}({entry.WeenieClassId})";

                                            if (WeenieLevels != null)
                                            {
                                                WeenieLevels.TryGetValue(entry.WeenieClassId, out var generatedLevel);
                                                if (generatedLevel > 0)
                                                    generated += $" - Level: {generatedLevel}";
                                            }
                                        }
                                        else
                                            generated += entry.WeenieClassId;
                                    }
                                    else
                                    {
                                        generated += $"({entry.WeenieClassId})";

                                        var landblockId = value.ObjCellId >> 16;
                                        log.Warn($"[LANDBLOCKINSTANCEWRITER] Landblock {landblockId}:{parentWeenieName}: CreateList has entry to unknown weeniedClassId: {entry.WeenieClassId}");
                                    }
                                }
                            }

                            if (generated.Length > 0)
                                label += $" - Generates - {generated}";
                        }
                    }
                }

                var output = "VALUES (" +
                             $"0x{value.Guid.ToString("X8")}, " +
                             $"{value.WeenieClassId.ToString().PadLeft(5)}, " +
                             $"0x{value.ObjCellId:X8}, " +
                             $"{TrimNegativeZero(value.OriginX):0.######}, " +
                             $"{TrimNegativeZero(value.OriginY):0.######}, " +
                             $"{TrimNegativeZero(value.OriginZ):0.######}, " +
                             $"{TrimNegativeZero(value.AnglesW):0.######}, " +
                             $"{TrimNegativeZero(value.AnglesX):0.######}, " +
                             $"{TrimNegativeZero(value.AnglesY):0.######}, " +
                             $"{TrimNegativeZero(value.AnglesZ):0.######}, " +
                             $"{value.IsLinkChild.ToString().PadLeft(5)}, " +
                             $"'{value.LastModified:yyyy-MM-dd HH:mm:ss}'" +
                             $"); /* {label} */" +
                             Environment.NewLine + $"/* @teleloc 0x{value.ObjCellId:X8} [{TrimNegativeZero(value.OriginX):F6} {TrimNegativeZero(value.OriginY):F6} {TrimNegativeZero(value.OriginZ):F6}] {TrimNegativeZero(value.AnglesW):F6} {TrimNegativeZero(value.AnglesX):F6} {TrimNegativeZero(value.AnglesY):F6} {TrimNegativeZero(value.AnglesZ):F6} */";

                output = FixNullFields(output);

                writer.WriteLine(output);

                if (value.LandblockInstanceLink != null && value.LandblockInstanceLink.Count > 0)
                {
                    writer.WriteLine();
                    CreateSQLINSERTStatement(value.LandblockInstanceLink.OrderBy(r => r.ChildGuid).ToList(), instanceWcids, writer);
                }
            }
        }

        private void CreateSQLINSERTStatement(IList<LandblockInstanceLink> input, Dictionary<uint, uint> instanceWcids, StreamWriter writer)
        {
            writer.WriteLine("INSERT INTO `landblock_instance_link` (`parent_GUID`, `child_GUID`, `last_Modified`)");

            var lineGenerator = new Func<int, string>(i =>
            {
                string label = null;

                if (WeenieNames != null && instanceWcids.TryGetValue(input[i].ChildGuid, out var wcid) && WeenieNames.TryGetValue(wcid, out var weenieName))
                {
                    if (WeenieClassNames != null && WeenieClassNames.TryGetValue(wcid, out var className))
                        label += $"/* {weenieName} ({wcid}/{className})";
                    else
                        label = $" /* {weenieName} ({wcid})";

                    if (WeenieLevels != null && WeenieLevels.TryGetValue(wcid, out var weenieLevel) && weenieLevel != 0)
                        label += $" - Level: {weenieLevel}";

                    if (TreasureDeath != null)
                    {
                        var weenie = DatabaseManager.World.GetCachedWeenie(wcid);
                        var deathTreasureType = weenie.GetProperty(PropertyDataId.DeathTreasureType) ?? 0;
                        if (deathTreasureType != 0 && TreasureDeath.TryGetValue(deathTreasureType, out var treasureDeath))
                            label += $" {(TreasureDeathDesc)treasureDeath.TreasureType} - {GetValueForTreasureData(treasureDeath.TreasureType)}";
                    }

                    var type = 0;
                    if (WeenieTypes != null)
                        WeenieTypes.TryGetValue(wcid, out type);

                    if (type == (int)WeenieType.Chest || type == (int)WeenieType.Container || type == (int)WeenieType.Door)
                    {
                        var weenie = DatabaseManager.World.GetCachedWeenie(wcid);
                        var locked = weenie.GetProperty(PropertyBool.DefaultLocked) ?? false;
                        var resistLockpick = weenie.GetProperty(PropertyInt.ResistLockpick) ?? 0;
                        var key = weenie.GetProperty(PropertyString.LockCode) ?? "";

                        if (locked)
                            label += $" - Locked({resistLockpick}{(key.Length > 0 ? $"/{key})" : ")")}";

                        var content = "";
                        if (TreasureDeath != null)
                        {
                            if (weenie.PropertiesGenerator != null)
                            {
                                bool isFirst = true;
                                foreach (var entry in weenie.PropertiesGenerator)
                                {
                                    if (!isFirst)
                                        content += " / ";
                                    isFirst = false;

                                    if (entry.WhereCreate.HasFlag(RegenLocationType.Treasure))
                                        content += GetValueForTreasureData(entry.WeenieClassId);
                                    else
                                    {
                                        if (WeenieNames != null)
                                        {
                                            WeenieNames.TryGetValue(entry.WeenieClassId, out var entryWeenieName);
                                            if (WeenieClassNames != null && WeenieClassNames.TryGetValue(entry.WeenieClassId, out var entryClassName))
                                                content += $"{entryWeenieName}({entry.WeenieClassId}/{entryClassName})";
                                            else
                                                content += $"{entryWeenieName}({entry.WeenieClassId})";
                                        }
                                        else
                                            content += entry.WeenieClassId;
                                    }
                                }
                            }

                            if (weenie.PropertiesCreateList != null)
                            {
                                bool isFirst = true;
                                foreach (var entry in weenie.PropertiesCreateList)
                                {
                                    if (entry.WeenieClassId == 0)
                                        continue;

                                    if (!isFirst)
                                        content += " / ";
                                    isFirst = false;

                                    if (WeenieNames != null)
                                    {
                                        WeenieNames.TryGetValue(entry.WeenieClassId, out var entryWeenieName);
                                        if (WeenieClassNames != null && WeenieClassNames.TryGetValue(entry.WeenieClassId, out var entryClassName))
                                            content += $"{entryWeenieName}({entry.WeenieClassId}/{entryClassName})";
                                        else
                                            content += $"{entryWeenieName}({entry.WeenieClassId})";

                                        if (WeenieLevels != null)
                                        {
                                            WeenieLevels.TryGetValue(entry.WeenieClassId, out var generatedLevel);
                                            if (generatedLevel > 0)
                                                content += $" - Level: {generatedLevel}";
                                        }
                                    }
                                    else
                                        content += entry.WeenieClassId;
                                }
                            }
                        }

                        if (content.Length > 0)
                            label += $" - Content - {content}";
                    }
                    else// if (type == (int)WeenieType.Generic)
                    {
                        var weenie = DatabaseManager.World.GetCachedWeenie(wcid);

                        var generated = "";
                        if (TreasureDeath != null)
                        {
                            if (weenie.PropertiesGenerator != null)
                            {
                                bool isFirst = true;
                                foreach (var entry in weenie.PropertiesGenerator)
                                {
                                    if (!isFirst)
                                        generated += " / ";
                                    isFirst = false;

                                    if (entry.WhereCreate.HasFlag(RegenLocationType.Treasure))
                                        generated += GetValueForTreasureData(entry.WeenieClassId);
                                    else
                                    {
                                        if (WeenieNames != null)
                                        {
                                            WeenieNames.TryGetValue(entry.WeenieClassId, out var entryWeenieName);
                                            if (WeenieClassNames != null && WeenieClassNames.TryGetValue(entry.WeenieClassId, out var entryClassName))
                                                generated += $"{entryWeenieName}({entry.WeenieClassId}/{entryClassName})";
                                            else
                                                generated += $"{entryWeenieName}({entry.WeenieClassId})";

                                            if (WeenieLevels != null)
                                            {
                                                WeenieLevels.TryGetValue(entry.WeenieClassId, out var generatedLevel);
                                                if (generatedLevel > 0)
                                                    generated += $" - Level: {generatedLevel}";
                                            }
                                        }
                                        else
                                            generated += entry.WeenieClassId;
                                    }
                                }
                            }

                            if (weenie.PropertiesCreateList != null)
                            {
                                bool isFirst = true;
                                foreach (var entry in weenie.PropertiesCreateList)
                                {
                                    if (entry.WeenieClassId == 0)
                                        continue;

                                    if (!isFirst)
                                        generated += " / ";
                                    isFirst = false;

                                    if (WeenieNames != null)
                                    {
                                        WeenieNames.TryGetValue(entry.WeenieClassId, out var entryWeenieName);
                                        if (WeenieClassNames != null && WeenieClassNames.TryGetValue(entry.WeenieClassId, out var entryClassName))
                                            generated += $"{entryWeenieName}({entry.WeenieClassId}/{entryClassName})";
                                        else
                                            generated += $"{entryWeenieName}({entry.WeenieClassId})";

                                        if (WeenieLevels != null)
                                        {
                                            WeenieLevels.TryGetValue(entry.WeenieClassId, out var generatedLevel);
                                            if (generatedLevel > 0)
                                                generated += $" - Level: {generatedLevel}";
                                        }
                                    }
                                    else
                                        generated += entry.WeenieClassId;
                                }
                            }
                        }

                        if (generated.Length > 0)
                            label += $" - Generates - {generated}";
                    }

                    label += " */";
                }

                return $"0x{input[i].ParentGuid.ToString("X8")}, 0x{input[i].ChildGuid.ToString("X8")}, '{input[i].LastModified.ToString("yyyy-MM-dd HH:mm:ss")}'){label}";
            });

            ValuesWriter(input.Count, lineGenerator, writer);
        }
    }
}
