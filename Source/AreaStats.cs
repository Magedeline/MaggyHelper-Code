﻿#pragma warning disable CS0436 // Local patch types intentionally shadow imported Celeste runtime types.

// Decompiled with JetBrains decompiler
// Type: Celeste.AreaStats
// Assembly: Celeste, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 3F0C8D56-DA65-4356-B04B-572A65ED61D1
// Assembly location: M:\code\bin\Celeste\Celeste.exe

using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using MonoMod;

namespace Celeste
{
  [Serializable]
  public class AreaStats
  {
    [XmlAttribute]
    public int ID;
    [XmlAttribute]
    public bool Cassette;
    public AreaModeStats[] Modes;

    public int TotalStrawberries
    {
      get
      {
        int num = 0;
        for (int index = 0; index < this.Modes.Length; ++index)
          num += this.Modes[index].TotalStrawberries;
        return num;
      }
    }

    public int TotalDeaths
    {
      get
      {
        int num = 0;
        for (int index = 0; index < this.Modes.Length; ++index)
          num += this.Modes[index].Deaths;
        return num;
      }
    }

    public long TotalTimePlayed
    {
      get
      {
        long num = 0;
        for (int index = 0; index < this.Modes.Length; ++index)
          num += this.Modes[index].TimePlayed;
        return num;
      }
    }

    public int BestTotalDeaths
    {
      get
      {
        int num = 0;
        for (int index = 0; index < this.Modes.Length; ++index)
          num += this.Modes[index].BestDeaths;
        return num;
      }
    }

    public int BestTotalDashes
    {
      get
      {
        int num = 0;
        for (int index = 0; index < this.Modes.Length; ++index)
          num += this.Modes[index].BestDashes;
        return num;
      }
    }

    public long BestTotalTime
    {
      get
      {
        long num = 0;
        for (int index = 0; index < this.Modes.Length; ++index)
          num += this.Modes[index].BestTime;
        return num;
      }
    }

    public AreaStats(int id)
    {
      this.ID = id;
      this.Modes = new AreaModeStats[Enum.GetValues(typeof (AreaMode)).Length];
      for (int index = 0; index < this.Modes.Length; ++index)
        this.Modes[index] = new AreaModeStats();
    }

    private AreaStats()
    {
      int length = Enum.GetValues(typeof (AreaMode)).Length;
      this.Modes = new AreaModeStats[length];
      for (int index = 0; index < length; ++index)
        this.Modes[index] = new AreaModeStats();
    }

    public AreaStats Clone()
    {
      AreaStats areaStats = new AreaStats()
      {
        ID = this.ID,
        Cassette = this.Cassette
      };
      for (int index = 0; index < this.Modes.Length; ++index)
        areaStats.Modes[index] = this.Modes[index].Clone();
      return areaStats;
    }

    public void CleanCheckpoints()
    {
      foreach (AreaMode areaMode in Enum.GetValues(typeof (AreaMode)))
      {
        if ((AreaMode) AreaData.Get(this.ID).Mode.Length > areaMode)
        {
          AreaModeStats mode = this.Modes[(int) areaMode];
          ModeProperties modeProperties = AreaData.Get(this.ID).Mode[(int) areaMode];
          HashSet<string> stringSet = new HashSet<string>((IEnumerable<string>) mode.Checkpoints);
          mode.Checkpoints.Clear();
          if (modeProperties != null && modeProperties.Checkpoints != null)
          {
            foreach (CheckpointData checkpoint in modeProperties.Checkpoints)
            {
              if (stringSet.Contains(checkpoint.Level))
                mode.Checkpoints.Add(checkpoint.Level);
            }
          }
        }
      }
    }
  }
}

namespace Celeste
{
  public class patch_AreaStats : AreaStats
  {
    [XmlAttribute]
    [MonoModLinkFrom("System.Int32 Celeste.AreaStats::ID_Unsafe")]
    public new int ID;

    [MonoModRemove]
    public int ID_Unsafe;

    [XmlIgnore]
    [MonoModLinkFrom("System.Int32 Celeste.AreaStats::ID")]
    public int ID_Safe
    {
      get
      {
        if (!string.IsNullOrEmpty(SID))
          return patch_SaveData.FindAreaBySid(SID)?.ID ?? ID_Unsafe;
        return ID_Unsafe;
      }
      set
      {
        ID_Unsafe = value;
        if (ID_Unsafe >= 0 && ID_Unsafe < AreaData.Areas.Count)
          SID = AreaData.Areas[ID_Unsafe].SID;
        else
          SID = null;
      }
    }

    [XmlAttribute]
    public string SID;

    public string LevelSet
    {
      get
      {
        string sid = SID;
        if (string.IsNullOrEmpty(sid))
          return "";

        int lastIndexOfSlash = sid.LastIndexOf('/');
        if (lastIndexOfSlash == -1)
          return "";

        return sid.Substring(0, lastIndexOfSlash);
      }
    }

    public patch_AreaStats(int id)
      : base(id)
    {
    }

    [MonoModReplace]
    public new void CleanCheckpoints()
    {
      if (string.IsNullOrEmpty(SID) && (ID_Unsafe < 0 || AreaData.Areas.Count <= ID_Unsafe))
        throw new Exception($"SaveData contains invalid AreaStats with no SID and out-of-range ID of {ID_Unsafe} / {AreaData.Areas.Count}");

      AreaData area = AreaData.Get(ID);

      for (int i = 0; i < Modes.Length; i++)
      {
        AreaMode areaMode = (AreaMode)i;
        AreaModeStats areaModeStats = Modes[i];
        ModeProperties modeProperties = null;
        if (area.HasMode(areaMode))
          modeProperties = area.Mode[i];

        HashSet<string> checkpoints = new HashSet<string>(areaModeStats.Checkpoints);
        areaModeStats.Checkpoints.Clear();

        if (modeProperties != null && modeProperties.Checkpoints != null)
        {
          foreach (CheckpointData checkpointData in modeProperties.Checkpoints)
          {
            if (checkpoints.Contains(checkpointData.Level))
              areaModeStats.Checkpoints.Add(checkpointData.Level);
          }
        }
      }
    }
  }

  [Obsolete("Use AreaStats members instead.")]
  public static class AreaStatsExt
  {
    public static AreaKey ToKey(this patch_AreaStats self, AreaMode mode)
      => new AreaKey(self.ID, mode).SetSID(self.SID);

    [Obsolete("Use AreaStats.LevelSet instead.")]
    public static string GetLevelSet(this AreaStats self)
      => ((patch_AreaStats)self).LevelSet;

    [Obsolete("Use AreaStats.SID instead.")]
    public static string GetSID(this AreaStats self)
      => ((patch_AreaStats)self).SID;

    [Obsolete("Use AreaStats.SID instead.")]
    public static AreaStats SetSID(this AreaStats self, string value)
    {
      ((patch_AreaStats)self).SID = value;
      return self;
    }
  }
}
