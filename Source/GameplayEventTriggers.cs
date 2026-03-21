using MaggyHelper.Entities;
using global::MaggyHelper.Extensions.Core;
using MaggyHelper.Utils;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;

namespace MaggyHelper
{
    // =============================================
    // EnemyWaveTrigger - Spawns waves of enemies
    // =============================================
    [CustomEntity("MaggyHelper/EnemyWaveTrigger")]
    public class EnemyWaveTrigger : Trigger
    {
        private int totalWaves;
        private int enemiesPerWave;
        private float waveDelay;
        private string enemyType;
        private bool onlyOnce;
        private bool triggered = false;
        private int currentWave = 0;
        private string completionFlag;

        public EnemyWaveTrigger(EntityData data, Vector2 offset)
            : base(data, offset)
        {
            totalWaves = data.Int("totalWaves", 3);
            enemiesPerWave = data.Int("enemiesPerWave", 3);
            waveDelay = data.Float("waveDelay", 2f);
            enemyType = data.Attr("enemyType", "patrol");
            onlyOnce = data.Bool("onlyOnce", true);
            completionFlag = data.Attr("completionFlag", "wave_complete");
        }

        public override void OnEnter(Player player)
        {
            base.OnEnter(player);
            if (triggered && onlyOnce) return;
            triggered = true;
            Add(new Coroutine(WaveRoutine()));
        }

        private IEnumerator WaveRoutine()
        {
            for (int wave = 0; wave < totalWaves; wave++)
            {
                currentWave = wave + 1;
                Audio.Play("event:/game/general/fallblock_shake", Position);

                for (int i = 0; i < enemiesPerWave; i++)
                {
                    float x = Left + Calc.Random.NextFloat(Width);
                    float y = Top + 16f;
                    Vector2 spawnPos = new Vector2(x, y);

                    Enemy enemy = enemyType switch
                    {
                        "patrol" => new PatrolEnemy(CreateEntityData(spawnPos), Vector2.Zero),
                        "flying" => new FlyingEnemy(CreateEntityData(spawnPos), Vector2.Zero),
                        "jumping" => new JumpingEnemy(CreateEntityData(spawnPos), Vector2.Zero),
                        _ => new PatrolEnemy(CreateEntityData(spawnPos), Vector2.Zero)
                    };

                    Scene.Add(enemy);
                }

                // Wait for all enemies in wave to be defeated
                yield return waveDelay;
                while (CountEnemiesInArea() > 0)
                {
                    yield return 0.2f;
                }
            }

            SceneAs<Level>().Session.SetFlag(completionFlag, true);
            Audio.Play("event:/game/general/touchswitch_last_cutoff", Position);
        }

        private int CountEnemiesInArea()
        {
            int count = 0;
            foreach (Enemy e in Scene.Tracker.GetEntities<Enemy>())
            {
                if (e.X > Left && e.X < Right && e.Y > Top && e.Y < Bottom)
                    count++;
            }
            return count;
        }

        private EntityData CreateEntityData(Vector2 pos)
        {
            EntityData data = new EntityData();
            data.Position = pos;
            data.Values = new Dictionary<string, object>
            {
                { "health", 1 },
                { "speed", 30f },
                { "patrolDistance", 60f },
                { "detectionRange", 100f }
            };
            return data;
        }
    }

    // =============================================
    // TimerStartTrigger - Starts countdown timer
    // =============================================
    [CustomEntity("MaggyHelper/TimerStartTrigger")]
    public class TimerStartTrigger : Trigger
    {
        private float countdownTime;
        private string timerFlag;
        private string failFlag;
        private bool onlyOnce;
        private bool triggered = false;

        public TimerStartTrigger(EntityData data, Vector2 offset)
            : base(data, offset)
        {
            countdownTime = data.Float("time", 30f);
            timerFlag = data.Attr("timerFlag", "timer_active");
            failFlag = data.Attr("failFlag", "timer_failed");
            onlyOnce = data.Bool("onlyOnce", true);
        }

        public override void OnEnter(Player player)
        {
            base.OnEnter(player);
            if (triggered && onlyOnce) return;
            triggered = true;

            Level level = SceneAs<Level>();
            level.Session.SetFlag(timerFlag, true);
            level.Session.SetFlag(failFlag, false);
            Add(new Coroutine(CountdownRoutine()));
        }

        private IEnumerator CountdownRoutine()
        {
            float time = countdownTime;
            while (time > 0)
            {
                time -= Engine.DeltaTime;
                // Store remaining time as flag
                if (time <= 5f && Scene.OnInterval(1f))
                {
                    Audio.Play("event:/game/general/assist_screenbottom", Position);
                }
                yield return null;
            }

            Level level = SceneAs<Level>();
            if (level.Session.GetFlag(timerFlag))
            {
                level.Session.SetFlag(failFlag, true);
                level.Session.SetFlag(timerFlag, false);
                // Kill player on timer expire
                Player player = Scene.Tracker.GetEntity<Player>();
                if (player != null) player.Die(Vector2.Zero);
            }
        }
    }

    // =============================================
    // TimerStopTrigger - Stops countdown timer
    // =============================================
    [CustomEntity("MaggyHelper/TimerStopTrigger")]
    public class TimerStopTrigger : Trigger
    {
        private string timerFlag;
        private string bonusFlag;

        public TimerStopTrigger(EntityData data, Vector2 offset)
            : base(data, offset)
        {
            timerFlag = data.Attr("timerFlag", "timer_active");
            bonusFlag = data.Attr("bonusFlag", "timer_bonus");
        }

        public override void OnEnter(Player player)
        {
            base.OnEnter(player);
            Level level = SceneAs<Level>();
            level.Session.SetFlag(timerFlag, false);
            level.Session.SetFlag(bonusFlag, true);
            Audio.Play("event:/game/general/touchswitch_last_cutoff", Position);
        }
    }

    // =============================================
    // CheckpointTrigger - Custom checkpoint
    // =============================================
    [CustomEntity("MaggyHelper/CheckpointTrigger")]
    public class CheckpointTrigger : Trigger
    {
        private bool savesAbilities;
        private bool onlyOnce;
        private bool triggered = false;
        private string checkpointId;

        public CheckpointTrigger(EntityData data, Vector2 offset)
            : base(data, offset)
        {
            savesAbilities = data.Bool("savesAbilities", true);
            onlyOnce = data.Bool("onlyOnce", false);
            checkpointId = data.Attr("checkpointId", "checkpoint_1");
        }

        public override void OnEnter(Player player)
        {
            base.OnEnter(player);
            if (triggered && onlyOnce) return;
            triggered = true;

            Level level = SceneAs<Level>();
            level.Session.SetFlag("checkpoint_" + checkpointId, true);
            level.Session.RespawnPoint = player.Position;
            MaggyProgressionManager.RecordCheckpoint(level, player.Position, checkpointId);
            Audio.Play("event:/game/general/seed_touch", Position);
        }
    }

    // =============================================
    // TeleportTrigger - Teleports player
    // =============================================
    [CustomEntity("MaggyHelper/TeleportTrigger")]
    public class TeleportTrigger : Trigger
    {
        private Vector2 targetPosition;
        private string targetRoom;
        private bool useTransition;
        private bool onlyOnce;
        private bool triggered = false;

        public TeleportTrigger(EntityData data, Vector2 offset)
            : base(data, offset)
        {
            targetRoom = data.Attr("targetRoom", "");
            useTransition = data.Bool("useTransition", true);
            onlyOnce = data.Bool("onlyOnce", false);

            if (data.Nodes.Length > 0)
                targetPosition = data.NodesOffset(offset)[0];
            else
                targetPosition = data.Position + offset;
        }

        public override void OnEnter(Player player)
        {
            base.OnEnter(player);
            if (triggered && onlyOnce) return;
            triggered = true;

            if (!string.IsNullOrEmpty(targetRoom))
            {
                Level level = SceneAs<Level>();
                level.OnEndOfFrame += () =>
                {
                    level.TeleportTo(player, targetRoom, Player.IntroTypes.Transition);
                };
            }
            else
            {
                player.Position = targetPosition;
                Audio.Play("event:/game/general/cassette_bubblereturn", Position);
                (Scene as Level)?.Flash(Color.White * 0.3f);
            }
        }
    }

    // =============================================
    // AmbushTrigger - Locks room and spawns enemies
    // =============================================
    [CustomEntity("MaggyHelper/AmbushTrigger")]
    public class AmbushTrigger : Trigger
    {
        private int enemyCount;
        private string enemyType;
        private string completionFlag;
        private bool onlyOnce;
        private bool triggered = false;
        private bool ambushActive = false;

        public AmbushTrigger(EntityData data, Vector2 offset)
            : base(data, offset)
        {
            enemyCount = data.Int("enemyCount", 5);
            enemyType = data.Attr("enemyType", "patrol");
            completionFlag = data.Attr("completionFlag", "ambush_complete");
            onlyOnce = data.Bool("onlyOnce", true);
        }

        public override void OnEnter(Player player)
        {
            base.OnEnter(player);
            if (triggered && onlyOnce) return;
            if (SceneAs<Level>().Session.GetFlag(completionFlag)) return;
            triggered = true;

            Add(new Coroutine(AmbushRoutine()));
        }

        private IEnumerator AmbushRoutine()
        {
            Audio.Play("event:/game/general/fallblock_shake", Position);
            (Scene as Level)?.Shake(0.3f);
            ambushActive = true;

            yield return 0.5f;

            // Spawn enemies
            for (int i = 0; i < enemyCount; i++)
            {
                float x = Left + Calc.Random.NextFloat(Width);
                float y = Top + Calc.Random.NextFloat(Height * 0.5f);
                Scene.Add(new PatrolEnemy(CreateEntityData(new Vector2(x, y)), Vector2.Zero));
                yield return 0.2f;
            }

            // Wait for all enemies defeated
            while (true)
            {
                int alive = 0;
                foreach (Enemy e in Scene.Tracker.GetEntities<Enemy>())
                {
                    if (e.X > Left - 32 && e.X < Right + 32 && e.Y > Top - 32 && e.Y < Bottom + 32)
                        alive++;
                }
                if (alive == 0) break;
                yield return 0.5f;
            }

            ambushActive = false;
            SceneAs<Level>().Session.SetFlag(completionFlag, true);
            Audio.Play("event:/game/general/touchswitch_last_cutoff", Position);
        }

        private EntityData CreateEntityData(Vector2 pos)
        {
            EntityData data = new EntityData();
            data.Position = pos;
            data.Values = new Dictionary<string, object>
            {
                { "health", 1 }, { "speed", 30f }, { "patrolDistance", 60f }
            };
            return data;
        }
    }

    // =============================================
    // BossIntroTrigger - Cinematic boss introduction
    // =============================================
    [CustomEntity("MaggyHelper/BossIntroTrigger")]
    public class BossIntroTrigger : Trigger
    {
        private string bossName;
        private string dialogId;
        private Vector2 cameraTarget;
        private float introDuration;
        private bool onlyOnce;
        private bool triggered = false;

        public BossIntroTrigger(EntityData data, Vector2 offset)
            : base(data, offset)
        {
            bossName = data.Attr("bossName", "Boss");
            dialogId = data.Attr("dialogId", "");
            introDuration = data.Float("introDuration", 3f);
            onlyOnce = data.Bool("onlyOnce", true);

            if (data.Nodes.Length > 0)
                cameraTarget = data.NodesOffset(offset)[0];
        }

        public override void OnEnter(Player player)
        {
            base.OnEnter(player);
            if (triggered && onlyOnce) return;
            triggered = true;
            Add(new Coroutine(IntroRoutine(player)));
        }

        private IEnumerator IntroRoutine(Player player)
        {
            Level level = SceneAs<Level>();
            player.StateMachine.State = Player.StDummy;

            // Camera pan to boss
            level.Session.SetFlag("boss_intro_active", true);
            (Scene as Level)?.Shake(0.2f);

            yield return 0.5f;

            if (!string.IsNullOrEmpty(dialogId))
            {
                Scene.Add(new MiniTextbox(dialogId));
            }

            yield return introDuration;

            player.StateMachine.State = Player.StNormal;
            level.Session.SetFlag("boss_intro_active", false);
            level.Session.SetFlag("boss_fight_active", true);
        }
    }

    // =============================================
    // WeatherChangeTrigger - Changes weather effects
    // =============================================
    [CustomEntity("MaggyHelper/WeatherChangeTrigger")]
    public class WeatherChangeTrigger : Trigger
    {
        private string weatherType;
        private float intensity;
        private bool persistOnExit;

        public WeatherChangeTrigger(EntityData data, Vector2 offset)
            : base(data, offset)
        {
            weatherType = data.Attr("weatherType", "rain");
            intensity = data.Float("intensity", 1f);
            persistOnExit = data.Bool("persistOnExit", false);
        }

        public override void OnEnter(Player player)
        {
            base.OnEnter(player);
            Level level = SceneAs<Level>();
            // Clear other weather flags
            level.Session.SetFlag("weather_rain", false);
            level.Session.SetFlag("weather_snow", false);
            level.Session.SetFlag("weather_sandstorm", false);
            level.Session.SetFlag("weather_meteor", false);

            level.Session.SetFlag("weather_" + weatherType, true);

            if (weatherType == "rain" || weatherType == "snow")
            {
                level.Wind = new Vector2(intensity * 40f, 0f);
            }
        }

        public override void OnLeave(Player player)
        {
            base.OnLeave(player);
            if (!persistOnExit)
            {
                Level level = SceneAs<Level>();
                level.Session.SetFlag("weather_" + weatherType, false);
                level.Wind = Vector2.Zero;
            }
        }
    }

    // =============================================
    // MusicLayerTrigger - Dynamic music layers
    // =============================================
    [CustomEntity("MaggyHelper/MusicLayerTrigger")]
    public class MusicLayerTrigger : Trigger
    {
        private int layer;
        private bool enable;
        private float fadeTime;

        public MusicLayerTrigger(EntityData data, Vector2 offset)
            : base(data, offset)
        {
            layer = data.Int("layer", 1);
            enable = data.Bool("enable", true);
            fadeTime = data.Float("fadeTime", 0.5f);
        }

        public override void OnEnter(Player player)
        {
            base.OnEnter(player);
            Level level = SceneAs<Level>();
            level.Session.Audio.Music.Layer(layer, enable);
            level.Session.Audio.Apply();
        }

        public override void OnLeave(Player player)
        {
            base.OnLeave(player);
            Level level = SceneAs<Level>();
            level.Session.Audio.Music.Layer(layer, !enable);
            level.Session.Audio.Apply();
        }
    }

    // =============================================
    // NarratorTrigger - Floating text narration
    // =============================================
    [CustomEntity("MaggyHelper/NarratorTrigger")]
    public class NarratorTrigger : Trigger
    {
        private string dialogId;
        private bool onlyOnce;
        private bool triggered = false;

        public NarratorTrigger(EntityData data, Vector2 offset)
            : base(data, offset)
        {
            dialogId = data.Attr("dialogId", "NARRATOR_DEFAULT");
            onlyOnce = data.Bool("onlyOnce", true);
        }

        public override void OnEnter(Player player)
        {
            base.OnEnter(player);
            if (triggered && onlyOnce) return;
            triggered = true;
            Scene.Add(new MiniTextbox(dialogId));
        }
    }

    // =============================================
    // FlashbackTrigger - Visual flashback sequence
    // =============================================
    [CustomEntity("MaggyHelper/FlashbackTrigger")]
    public class FlashbackTrigger : Trigger
    {
        private string dialogId;
        private float duration;
        private bool onlyOnce;
        private bool triggered = false;

        public FlashbackTrigger(EntityData data, Vector2 offset)
            : base(data, offset)
        {
            dialogId = data.Attr("dialogId", "FLASHBACK_DEFAULT");
            duration = data.Float("duration", 3f);
            onlyOnce = data.Bool("onlyOnce", true);
        }

        public override void OnEnter(Player player)
        {
            base.OnEnter(player);
            if (triggered && onlyOnce) return;
            triggered = true;
            Add(new Coroutine(FlashbackRoutine(player)));
        }

        private IEnumerator FlashbackRoutine(Player player)
        {
            Level level = SceneAs<Level>();
            player.StateMachine.State = Player.StDummy;
            level.SnapColorGrade("oldsite");
            (Scene as Level)?.Flash(Color.White * 0.5f);

            yield return 0.5f;
            Scene.Add(new MiniTextbox(dialogId));
            yield return duration;

            level.SnapColorGrade(null);
            (Scene as Level)?.Flash(Color.White * 0.3f);
            player.StateMachine.State = Player.StNormal;
        }
    }

    // =============================================
    // SecretRevealTrigger - Reveals hidden areas
    // =============================================
    [CustomEntity("MaggyHelper/SecretRevealTrigger")]
    public class SecretRevealTrigger : Trigger
    {
        private string secretFlag;
        private bool onlyOnce;
        private bool triggered = false;

        public SecretRevealTrigger(EntityData data, Vector2 offset)
            : base(data, offset)
        {
            secretFlag = data.Attr("flag", "secret_revealed");
            onlyOnce = data.Bool("onlyOnce", true);
        }

        public override void OnEnter(Player player)
        {
            base.OnEnter(player);
            if (triggered && onlyOnce) return;
            triggered = true;

            Level level = SceneAs<Level>();
            level.Session.SetFlag(secretFlag, true);
            Audio.Play("event:/game/general/touchswitch_last_cutoff", Position);
            (Scene as Level)?.Flash(Color.Gold * 0.3f);
            (Scene as Level)?.Shake(0.3f);
        }
    }

    // =============================================
    // CountdownEscapeTrigger - Escape sequence
    // =============================================
    [CustomEntity("MaggyHelper/CountdownEscapeTrigger")]
    public class CountdownEscapeTrigger : Trigger
    {
        private float escapeTime;
        private string escapeFlag;
        private bool onlyOnce;
        private bool triggered = false;

        public CountdownEscapeTrigger(EntityData data, Vector2 offset)
            : base(data, offset)
        {
            escapeTime = data.Float("escapeTime", 30f);
            escapeFlag = data.Attr("escapeFlag", "escape_active");
            onlyOnce = data.Bool("onlyOnce", true);
        }

        public override void OnEnter(Player player)
        {
            base.OnEnter(player);
            if (triggered && onlyOnce) return;
            triggered = true;

            Level level = SceneAs<Level>();
            level.Session.SetFlag(escapeFlag, true);
            Audio.Play("event:/game/general/fallblock_shake", Position);
            Add(new Coroutine(EscapeRoutine()));
        }

        private IEnumerator EscapeRoutine()
        {
            float time = escapeTime;
            Level level = SceneAs<Level>();

            while (time > 0 && level.Session.GetFlag(escapeFlag))
            {
                time -= Engine.DeltaTime;

                // Increasing shake as time runs out
                if (time < escapeTime * 0.3f && Scene.OnInterval(0.5f))
                {
                    (Scene as Level)?.Shake(0.1f);
                }

                if (time <= 5f && Scene.OnInterval(1f))
                {
                    Audio.Play("event:/game/general/assist_screenbottom", Position);
                }

                yield return null;
            }

            if (level.Session.GetFlag(escapeFlag))
            {
                // Time's up - kill player
                Player player = Scene.Tracker.GetEntity<Player>();
                if (player != null) player.Die(Vector2.Zero);
                level.Session.SetFlag(escapeFlag, false);
            }
        }
    }

    // =============================================
    // CharacterSwapTrigger - Swap between Kirby/Madeline
    // =============================================
    [CustomEntity("MaggyHelper/CharacterSwapTrigger")]
    public class CharacterSwapTrigger : Trigger
    {
        private string targetCharacter;
        private bool onlyOnce;
        private bool triggered = false;

        public CharacterSwapTrigger(EntityData data, Vector2 offset)
            : base(data, offset)
        {
            targetCharacter = PlayerCharacter.NormalizeId(data.Attr("targetCharacter", PlayerCharacterIds.Kirby));
            onlyOnce = data.Bool("onlyOnce", true);
        }

        public override void OnEnter(Player player)
        {
            base.OnEnter(player);
            if (triggered && onlyOnce) return;
            triggered = true;

            Level level = SceneAs<Level>();
            level.Session.SetFlag("character_kirby", targetCharacter == PlayerCharacterIds.Kirby);
            level.Session.SetFlag("character_madeline", targetCharacter == PlayerCharacterIds.Madeline);
            MaggyProgressionManager.RecordPreferredCharacter(level, targetCharacter);
            Audio.Play("event:/game/general/cassette_bubblereturn", Position);
            (Scene as Level)?.Flash(Color.White * 0.4f);
        }
    }

    // =============================================
    // DualCharacterTrigger - Control both characters
    // =============================================
    [CustomEntity("MaggyHelper/DualCharacterTrigger")]
    public class DualCharacterTrigger : Trigger
    {
        private bool enable;

        public DualCharacterTrigger(EntityData data, Vector2 offset)
            : base(data, offset)
        {
            enable = data.Bool("enable", true);
        }

        public override void OnEnter(Player player)
        {
            base.OnEnter(player);
            SceneAs<Level>().Session.SetFlag("dual_character_mode", enable);
        }

        public override void OnLeave(Player player)
        {
            base.OnLeave(player);
            if (enable)
                SceneAs<Level>().Session.SetFlag("dual_character_mode", false);
        }
    }

    // =============================================
    // ScoreTrigger - Awards/deducts points
    // =============================================
    [CustomEntity("MaggyHelper/ScoreTrigger")]
    public class ScoreTrigger : Trigger
    {
        private int points;
        private bool onlyOnce;
        private bool triggered = false;

        public ScoreTrigger(EntityData data, Vector2 offset)
            : base(data, offset)
        {
            points = data.Int("points", 100);
            onlyOnce = data.Bool("onlyOnce", true);
        }

        public override void OnEnter(Player player)
        {
            base.OnEnter(player);
            if (triggered && onlyOnce) return;
            triggered = true;

            Level level = SceneAs<Level>();
            level.Session.SetFlag("score_awarded_" + points, true);
            Audio.Play("event:/game/general/seed_touch", Position);
        }
    }

    // =============================================
    // RandomizerTrigger - Randomizes room elements
    // =============================================
    [CustomEntity("MaggyHelper/RandomizerTrigger")]
    public class RandomizerTrigger : Trigger
    {
        private int seed;
        private string randomizeTarget;
        private bool onlyOnce;
        private bool triggered = false;

        public RandomizerTrigger(EntityData data, Vector2 offset)
            : base(data, offset)
        {
            seed = data.Int("seed", 0);
            randomizeTarget = data.Attr("target", "enemies");
            onlyOnce = data.Bool("onlyOnce", true);
        }

        public override void OnEnter(Player player)
        {
            base.OnEnter(player);
            if (triggered && onlyOnce) return;
            triggered = true;

            Level level = SceneAs<Level>();
            Pcg32Random rng = seed > 0
                ? new Pcg32Random((uint)seed)
                : new Pcg32Random(unchecked((ulong)DateTime.UtcNow.Ticks), unchecked((ulong)(uint)Environment.TickCount));
            level.Session.SetFlag("randomizer_seed_" + rng.Next(1000), true);
        }
    }

    // =============================================
    // SaveStateTrigger - Rewind point
    // =============================================
    [CustomEntity("MaggyHelper/SaveStateTrigger")]
    public class SaveStateTrigger : Trigger
    {
        private string saveId;
        private bool onlyOnce;
        private bool triggered = false;

        public SaveStateTrigger(EntityData data, Vector2 offset)
            : base(data, offset)
        {
            saveId = data.Attr("saveId", "save_1");
            onlyOnce = data.Bool("onlyOnce", false);
        }

        public override void OnEnter(Player player)
        {
            base.OnEnter(player);
            if (triggered && onlyOnce) return;
            triggered = true;

            Level level = SceneAs<Level>();
            level.Session.RespawnPoint = player.Position;
            level.Session.SetFlag("savestate_" + saveId, true);
            MaggyProgressionManager.RecordCheckpoint(level, player.Position, saveId);
            Audio.Play("event:/game/general/seed_touch", Position);
        }
    }

    // =============================================
    // CompanionSummonTrigger - Summons/dismisses companion
    // =============================================
    [CustomEntity("MaggyHelper/CompanionSummonTrigger")]
    public class CompanionSummonTrigger : Trigger
    {
        private string companionType;
        private bool summon;
        private bool onlyOnce;
        private bool triggered = false;

        public CompanionSummonTrigger(EntityData data, Vector2 offset)
            : base(data, offset)
        {
            companionType = data.Attr("companionType", "waddle_dee");
            summon = data.Bool("summon", true);
            onlyOnce = data.Bool("onlyOnce", true);
        }

        public override void OnEnter(Player player)
        {
            base.OnEnter(player);
            if (triggered && onlyOnce) return;
            triggered = true;

            Level level = SceneAs<Level>();
            if (summon)
            {
                level.Session.SetFlag("companion_" + companionType, true);
                Audio.Play("event:/game/general/seed_touch", Position);
            }
            else
            {
                level.Session.SetFlag("companion_" + companionType, false);
                foreach (CompanionNPC c in Scene.Tracker.GetEntities<CompanionNPC>())
                {
                    c.Dismiss();
                }
            }
        }
    }
}
