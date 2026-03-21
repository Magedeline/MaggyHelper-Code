using Microsoft.Xna.Framework;
using Monocle;

namespace MaggyHelper.Actors;

/// <summary>
/// C# recreation of Graphics/customplayersprites.xml for an actor-driven Kirby player sprite.
/// This does not replace Celeste's built-in player; it provides an Actor-friendly runtime sprite setup.
/// </summary>
[CustomEntity(ids: "MaggyHelper/KirbyActorPlayer")]
[Tracked]
public class Player : Actor
{
    private static Player lastSpawned;

    public Sprite Sprite { get; }
    public Sprite Sweat { get; }

    public Player(Vector2 position) : base(position)
    {
        Collider = new Hitbox(8f, 16f, -4f, -16f);

        Add(Sprite = CreateKirbyPlayerSprite());
        Add(Sweat = CreateKirbySweatSprite());

        Sweat.Visible = false;
        lastSpawned = this;
    }

    public Player(EntityData data, Vector2 offset) : this(data.Position + offset)
    {
        string startAnim = data.Attr("startAnimation", "idle");
        bool faceLeft = data.Bool("faceLeft", false);
        bool showSweat = data.Bool("showSweat", false);
        string sweatAnim = data.Attr("sweatAnimation", "idle");

        if (faceLeft)
            Sprite.Scale.X = -1f;

        Play(startAnim, true);
        SetSweat(sweatAnim, showSweat);
    }

    public void Play(string animationId, bool restart = false)
    {
        if (Sprite.Has(animationId))
            Sprite.Play(animationId, restart);
    }

    public void SetSweat(string animationId, bool visible)
    {
        Sweat.Visible = visible;
        if (!visible)
            return;

        if (Sweat.Has(animationId))
            Sweat.Play(animationId);
        else if (Sweat.Has("idle"))
            Sweat.Play("idle");
    }

    public IEnumerator DemoCycle(int loops = 1)
    {
        string[] cycle =
        {
            "idle", "walk", "runFast", "jumpSlow", "fall", "dash", "dreamDashIn", "climbup", "swimIdle", "inhale", "spit", "parry"
        };

        loops = Math.Max(1, loops);
        for (int i = 0; i < loops; i++)
        {
            foreach (string anim in cycle)
            {
                Play(anim, true);
                yield return 0.35f;
            }
        }
    }

    [Command("maggy_kirby_actor_spawn", "Spawn Kirby actor player. Usage: maggy_kirby_actor_spawn [animation=idle] [sweat=none]")]
    private static void CmdSpawnKirbyActor(string animation = "idle", string sweat = "none")
    {
        Level level = Engine.Scene as Level;
        if (level == null)
        {
            Log("[MaggyHelper] Kirby actor spawn requires an active level.");
            return;
        }

        global::Celeste.Player player = level.Tracker.GetEntity<global::Celeste.Player>();
        Vector2 spawnAt = player?.Position ?? (level.Camera.Position + new Vector2(160f, 120f));

        Player actor = new Player(spawnAt);
        level.Add(actor);

        actor.Play(animation, true);
        if (!string.Equals(sweat, "none", StringComparison.OrdinalIgnoreCase))
            actor.SetSweat(sweat, true);

        Log($"[MaggyHelper] Spawned KirbyActorPlayer at {spawnAt} with anim '{animation}' and sweat '{sweat}'.");
    }

    [Command("maggy_kirby_actor_anim", "Set animation on last spawned Kirby actor. Usage: maggy_kirby_actor_anim <animation>")]
    private static void CmdKirbyActorAnim(string animation = "idle")
    {
        if (lastSpawned == null || lastSpawned.Scene == null)
        {
            Log("[MaggyHelper] No spawned Kirby actor found. Run maggy_kirby_actor_spawn first.");
            return;
        }

        if (!lastSpawned.Sprite.Has(animation))
        {
            Log($"[MaggyHelper] Kirby actor animation '{animation}' not found.");
            return;
        }

        lastSpawned.Play(animation, true);
        Log($"[MaggyHelper] Kirby actor animation set to '{animation}'.");
    }

    [Command("maggy_kirby_actor_sweat", "Set sweat overlay on last spawned Kirby actor. Usage: maggy_kirby_actor_sweat <on|off> [animation=idle]")]
    private static void CmdKirbyActorSweat(string state = "on", string animation = "idle")
    {
        if (lastSpawned == null || lastSpawned.Scene == null)
        {
            Log("[MaggyHelper] No spawned Kirby actor found. Run maggy_kirby_actor_spawn first.");
            return;
        }

        bool show = !string.Equals(state, "off", StringComparison.OrdinalIgnoreCase);
        lastSpawned.SetSweat(animation, show);
        Log($"[MaggyHelper] Kirby actor sweat {(show ? "enabled" : "disabled")} ({animation}).");
    }

    [Command("maggy_kirby_actor_cycle", "Run quick demo animation cycle on last spawned Kirby actor. Usage: maggy_kirby_actor_cycle [loops=1]")]
    private static void CmdKirbyActorCycle(int loops = 1)
    {
        if (lastSpawned == null || lastSpawned.Scene == null)
        {
            Log("[MaggyHelper] No spawned Kirby actor found. Run maggy_kirby_actor_spawn first.");
            return;
        }

        lastSpawned.Add(new Coroutine(lastSpawned.DemoCycle(loops), true));
        Log($"[MaggyHelper] Kirby actor demo cycle started (loops={Math.Max(1, loops)}).");
    }

    private static void Log(string message)
    {
        Engine.Commands?.Log(message);
        Logger.Log(LogLevel.Info, "MaggyHelper", message);
    }

    public static Sprite CreateKirbyPlayerSprite()
    {
        Sprite sprite = new Sprite(GFX.Game, "characters/kirby/");
        sprite.Justify = new Vector2(0.5f, 1f);

        // Idle
        sprite.Add("idle", "idle", 0.1f, "idle", Range(0, 8));
        sprite.Add("idleA", "idleA", 0.12f, "idle", Range(0, 11));
        sprite.Add("idleB", "idleB", 0.16f, "idle", Range(0, 23));
        sprite.Add("idleC", "idleC", 0.05f, "idle", Range(0, 14));
        sprite.Add("idleD", "idleD", 0.12f, "idle", Range(0, 8));
        sprite.Add("idleE", "idleE", 0.05f, "idle", Range(0, 9));
        sprite.AddLoop("idlemouthful", "idlemouthful", 0.1f, Range(0, 7));
        sprite.AddLoop("idlevineboom", "idlevineboom", 0.1f, Range(0, 12));

        // Ground movement
        sprite.Add("lookUp", "lookUp", 0.1f, Range(2, 7));
        sprite.AddLoop("walk", "walk", 0.06f, Range(0, 11));
        sprite.AddLoop("push", "push", 0.1f, Range(0, 15));
        sprite.Add("runSlow", "runSlow", 0.07f, "runFast", Range(0, 11));
        sprite.AddLoop("runFast", "runFast", 0.05f, Range(0, 11));
        sprite.Add("runStumble", "runStumble", 0.05f, "runFast", Range(0, 11));
        sprite.AddLoop("runWind", "run_wind", 0.095f, Range(0, 11));
        sprite.AddLoop("walkmouthful", "walkmouthful", 0.06f, Range(0, 11));

        // Jump / fall
        sprite.AddLoop("jumpSlow", "jumpSlow", 0.1f, 0, 1);
        sprite.AddLoop("jumpFast", "jumpFast", 0.08f, 0, 1);
        sprite.Add("fallSlow", "jumpSlow", 0.1f, 2, 3);
        sprite.Add("fallFast", "jumpFast", 0.08f, 2, 3);
        sprite.Add("fall", "fall", 0.06f, Range(0, 7));
        sprite.Add("fallPose", "fallPose", 0.1f, "idle", Range(0, 10));
        sprite.AddLoop("bigFall", "bigfall", 0.06f, Range(0, 4));
        sprite.Add("bigFallRecover", "bigfall", 0.08f, "idle", 5, 5, 5, 6, 6, 7, 7, 8, 9, 10);
        sprite.Add("land", "land", 0.06f, "idle", 0, 1);

        // Dash / slide
        sprite.AddLoop("dash", "dash", 0.09f, Range(0, 3));
        sprite.Add("flip", "flip", 0.04f, "runFast", Range(0, 7));
        sprite.AddLoop("skid", "flip", 0.04f, 8);
        sprite.AddLoop("slide", "slide", 0.03f, Range(0, 2));
        sprite.AddLoop("duck", "duck", 0f, 0);
        sprite.AddLoop("roll", "roll", 0.05f, Range(0, 16));

        // Dream dash
        sprite.Add("dreamDashIn", "dreamDash", 0.04f, "dreamDashLoop", Range(0, 3));
        sprite.AddLoop("dreamDashLoop", "dreamDash", 0.03f, Range(4, 16));
        sprite.Add("dreamDashOut", "dreamDash", 0.04f, "idle", Range(17, 20));

        // Climb
        sprite.AddLoop("wallslide", "wallslide", 0.1f, 0);
        sprite.AddLoop("climbup", "climb", 0.04f, Range(0, 5));
        sprite.Add("climbLookBackStart", "climb", 0.08f, "climbLookBack", 6, 7, 8);
        sprite.AddLoop("climbLookBack", "climb", 0.1f, 8);
        sprite.Add("climbPush", "climb", 0.04f, 0, 9, 10, 11);
        sprite.Add("climbPull", "climb", 0.04f, 0, 12, 13, 14);

        // Edge / tired
        sprite.AddLoop("edge", "edge", 0.25f, Range(0, 13));
        sprite.AddLoop("edgeBack", "edge_back", 0.25f, Range(0, 13));
        sprite.AddLoop("tired", "tired", 0.18f, Range(0, 3));
        sprite.Add("tiredStill", "tired", 0f, 0);

        // Faint / death
        sprite.Add("faint", "faint", 0.1f, "fainted", Range(0, 10));
        sprite.AddLoop("fainted", "faint", 0.1f, 10);
        sprite.Add("deadside", "death_h", 0.02f, Range(0, 12));
        sprite.Add("deadup", "death_h", 0.02f, Range(0, 12));
        sprite.Add("deaddown", "death_h", 0.02f, Range(0, 12));
        sprite.AddLoop("dangling", "dangling", 0.11f, Range(0, 9));
        sprite.AddLoop("shaking", "shaking", 0.1f, 0);

        // Carrying
        sprite.AddLoop("idle_carry", "idle_carry", 0.1f, Range(0, 8));
        sprite.AddLoop("runSlow_carry", "run_carry", 0.07f, Range(0, 11));
        sprite.AddLoop("jumpSlow_carry", "jump_carry", 0.1f, 0, 1);
        sprite.Add("fallSlow_carry", "jump_carry", 0.1f, 2, 3);
        sprite.Add("pickUp", "pickup", 0.06f, Range(0, 4));
        sprite.Add("throw", "throw", 0.06f, "idle", Range(0, 3));
        sprite.AddLoop("carryTheoWalk", "walk_carry_theo", 0.06f, Range(0, 11));
        sprite.Add("carryTheoCollapse", "walk_carry_theo", 0.06f, Range(12, 18));

        // Swim / star fly
        sprite.AddLoop("swimIdle", "swim", 0.08f, Range(0, 5));
        sprite.AddLoop("swimUp", "swim", 0.08f, Range(6, 11));
        sprite.AddLoop("swimDown", "swim", 0.08f, Range(12, 17));
        sprite.Add("startStarFly", "startStarFly", 0.08f, "starFly", Range(0, 3));
        sprite.AddLoop("starFly", "starFly", 0.08f, 0);
        sprite.AddLoop("bubble", "bubble", 0.08f, 0);

        // Sleep / wake
        sprite.Add("sleep", "sleep", 0.1f, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 10, 10, 10, 10, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23);
        sprite.Add("bagdown", "sleep", 0.1f, Range(0, 10));
        sprite.Add("asleep", "wakeUp/", 0f, 0);
        sprite.Add("wakeUp", "wakeUp/", 0.1f, Range(0, 14));
        sprite.Add("halfWakeUp", "halfWakeUp", 0.1f, 0);

        // Misc
        sprite.AddLoop("hug", "hug", 0.08f, 0);
        sprite.AddLoop("spin", "spin", 0.1f, Range(0, 15));
        sprite.Add("sitDown", "sitDown", 0.1f, Range(0, 17));
        sprite.AddLoop("launch", "launch", 0.06f, Range(0, 7));
        sprite.Add("launchRecover", "launchRecover", 0.06f, Range(0, 10));

        // Transform
        sprite.Add("starMorph", "starMorph", 0.06f, "starMorphIdle", Range(0, 9));
        sprite.AddLoop("starMorphIdle", "starMorph", 0.06f, 10);
        sprite.Add("transform_in", "transform_in", 0.05f, "idle", Range(0, 19));
        sprite.Add("transform_out", "transform_out", 0.05f, Range(0, 17));
        sprite.Add("transformToKirby", "kirby_transform_in", 0.05f, Range(0, 19));
        sprite.Add("transformFromKirby", "kirby_transform_out", 0.05f, "idle", Range(0, 17));

        // Hopes and dreams
        sprite.Add("hopesAndDreamsIn", "dreamDash", 0.04f, "hopesAndDreamsLoop", Range(0, 8));
        sprite.AddLoop("hopesAndDreamsLoop", "dreamDash", 0.02f, Range(4, 16));
        sprite.Add("hopesAndDreamsOut", "dreamDash", 0.04f, "idle", Range(17, 20));

        // Tentacle
        sprite.Add("tentacle_grab", "tentacle/grab", 0.06f, "tentacle_grabbed", Range(0, 14));
        sprite.AddLoop("tentacle_grabbed", "tentacle/grab", 0.1f, Range(15, 23));
        sprite.AddLoop("tentacle_pull", "tentacle/grab", 0.1f, 24);
        sprite.AddLoop("tentacle_dangling", "tentacle/grab", 0.1f, 25);

        // Kirby abilities core
        sprite.AddLoop("inhale", "inhale", 0.1f, Range(0, 6));
        sprite.AddLoop("inhalebegin", "inhalebegin", 0.08f, Range(0, 6));
        sprite.AddLoop("inhaleloop", "inhaleloop", 0.08f, Range(0, 2));
        sprite.Add("inhaleend", "inhaleend", 0.08f, "idle", Range(0, 3));
        sprite.Add("exhale", "exhale", 0.08f, "idle", Range(0, 3));
        sprite.Add("spit", "spit", 0.08f, "idle", 0);

        sprite.AddLoop("float", "float", 0.2f, 0, 1);
        sprite.AddLoop("hover", "hover", 0.1f, 0, 1);
        sprite.AddLoop("crouch", "crouch", 0.1f, 0);

        sprite.Add("parry", "parry", 0.05f, "idle", Range(0, 2));
        sprite.Add("hurt", "hurt", 0.08f, "idle", 0, 1);
        sprite.Add("attack", "attack", 0.06f, "idle", 0);
        sprite.AddLoop("determined", "determined", 0.08f, Range(0, 8));

        sprite.Add("levelUp", "levelUp/levelUp", 0.06f, "idle", Range(0, 26));
        sprite.AddLoop("angry", "extra/angry", 0.1f, Range(0, 5));

        // Power states
        AddPowerState(sprite, "fire", 0.15f, 0.08f, 0.03f, 0.04f, 0.05f,
            "attack", "burst", "dash", 4, 6, 1, 4, 4);

        AddPowerState(sprite, "ice", 0.15f, 0.08f, 0.1f, 0.06f, 0.08f,
            "attack", "freeze", "glide", 4, 6, 1, 2, 2);
        sprite.Add("ice_shatter", "ice/shatter", 0.05f, "ice_idle", 0, 1);

        AddPowerState(sprite, "spark", 0.12f, 0.08f, 0.05f, 0.03f, 0.04f,
            "attack", "chain", "pulse", 4, 6, 1, 3, 4);

        sprite.AddLoop("stone_idle", "stone/idle", 0.2f, 0);
        sprite.AddLoop("stone_walk", "stone/walk", 0.1f, Range(0, 3));
        sprite.Add("stone_transform", "stone/transform", 0.05f, Range(0, 5));
        sprite.AddLoop("stone_form", "stone/form", 0.3f, 0);
        sprite.Add("stone_crush", "stone/crush", 0.08f, "stone_idle", 0);
        sprite.Add("stone_attack", "stone/crush", 0.08f, "stone_idle", 0);
        sprite.Add("stone_counter", "stone/counter", 0.06f, "stone_idle", Range(0, 2));

        AddMeleePower(sprite, "sword", 0.15f, 0.08f,
            ("attack", 0.04f, 1),
            ("attack1", 0.04f, 1),
            ("attack2", 0.05f, 3),
            ("spin", 0.03f, 8),
            ("thrust", 0.05f, 4),
            ("combo", 0.04f, 6));

        AddMeleePower(sprite, "beam", 0.12f, 0.08f,
            ("attack", 0.04f, 1),
            ("whip", 0.05f, 3),
            ("cycle", 0.06f, 6));

        AddMeleePower(sprite, "cutter", 0.15f, 0.08f,
            ("throw", 0.05f, 1),
            ("attack", 0.05f, 1),
            ("boomerang", 0.04f, 3));

        AddMeleePower(sprite, "hammer", 0.15f, 0.1f,
            ("attack", 0.06f, 1),
            ("spin", 0.04f, 8),
            ("slam", 0.08f, 4));

        sprite.AddLoop("wing_idle", "wing/idle", 0.15f, Range(0, 3));
        sprite.AddLoop("wing_fly", "wing/fly", 0.08f, 0, 1);
        sprite.Add("wing_attack", "wing/attack", 0.05f, "wing_idle", Range(0, 3));
        sprite.Add("wing_dive", "wing/dive", 0.06f, Range(0, 3));
        sprite.AddLoop("wing_glide", "wing/glide", 0.1f, 0, 1);

        sprite.AddLoop("archer_idle", "archer/idle", 0.15f, Range(0, 3));
        sprite.AddLoop("archer_aim", "archer/aim", 0.08f, Range(0, 4));
        sprite.Add("archer_shoot", "archer/shoot", 0.04f, "archer_idle", 0);
        sprite.Add("archer_attack", "archer/shoot", 0.04f, "archer_idle", 0);

        sprite.AddLoop("leaf_idle", "leaf/idle", 0.15f, Range(0, 3));
        sprite.AddLoop("leaf_guard", "leaf/guard", 0.06f, Range(0, 2));
        sprite.Add("leaf_burst", "leaf/burst", 0.04f, "leaf_idle", Range(0, 3));
        sprite.Add("leaf_attack", "leaf/burst", 0.04f, "leaf_idle", Range(0, 3));

        sprite.AddLoop("water_idle", "water/idle", 0.15f, Range(0, 3));
        sprite.AddLoop("water_walk", "water/walk", 0.08f, Range(0, 5));
        sprite.Add("water_attack", "water/attack", 0.05f, "water_idle", 0);
        sprite.Add("water_wave", "water/wave", 0.06f, "water_idle", Range(0, 3));
        sprite.AddLoop("water_surf", "water/surf", 0.08f, Range(0, 2));

        sprite.AddLoop("mirror_idle", "mirror/idle", 0.15f, Range(0, 3));
        sprite.Add("mirror_reflect", "mirror/reflect", 0.06f, "mirror_idle", Range(0, 2));
        sprite.AddLoop("mirror_guard", "mirror/guard", 0.08f, Range(0, 2));
        sprite.Add("mirror_attack", "mirror/reflect", 0.06f, "mirror_idle", Range(0, 2));

        sprite.AddLoop("esp_idle", "esp/idle", 0.15f, Range(0, 3));
        sprite.AddLoop("esp_walk", "esp/walk", 0.08f, Range(0, 5));
        sprite.Add("esp_attack", "esp/attack", 0.05f, "esp_idle", 0);

        // Combat set
        sprite.Add("combat_backflip", "combat/backflip_", 0.06f, "idle", Range(0, 5));
        sprite.Add("combat_punchA", "combat/punchA_", 0.05f, "idle", Range(0, 5));
        sprite.Add("combat_punchB", "combat/punchB_", 0.05f, "idle", Range(0, 8));
        sprite.AddLoop("combat_spin", "combat/spin_", 0.04f, Range(0, 7));
        sprite.Add("combat_slide", "combat/slide_", 0.04f, "idle", Range(0, 13));
        sprite.Add("combat_groundpound", "combat/groundpound_", 0.05f, "idle", Range(0, 10));
        sprite.Add("combat_grab_enemy", "combat/grab_enemy_", 0.06f, "idle", Range(0, 5));
        sprite.Add("combat_super_punch", "combat/super_punch", 0.06f, "combat_super_punchloop", 0);
        sprite.AddLoop("combat_super_punchloop", "combat/super_punchloop", 0.04f, Range(0, 3));
        sprite.Add("combat_pre_death", "combat/pre_death_", 0.06f, "combat_mid_death", Range(0, 9));
        sprite.Add("combat_mid_death", "combat/mid_death_", 0.06f, "combat_post_death", Range(0, 9));
        sprite.Add("combat_post_death", "combat/post_death_", 0.06f, Range(0, 9));
        sprite.Add("combat_death", "combat/death_", 0.06f, Range(0, 9));

        sprite.Play("idle");
        return sprite;
    }

    public static Sprite CreateKirbyPlayerExtSprite()
    {
        // XML uses copy="kirby_player", so this shares the same animation set.
        return CreateKirbyPlayerSprite();
    }

    public static Sprite CreateKirbySweatSprite()
    {
        Sprite sweat = new Sprite(GFX.Game, "characters/kirby/sweat/");
        sweat.CenterOrigin();

        sweat.AddLoop("idle", "idle", 0.15f, 0);
        sweat.AddLoop("still", "still", 0.15f, Range(0, 5));
        sweat.AddLoop("danger", "danger", 0.1f, Range(0, 5));
        sweat.AddLoop("jump", "jump", 0.1f, Range(0, 3));
        sweat.AddLoop("climb", "climb", 0.08f, Range(0, 7));
        sweat.Play("idle");

        return sweat;
    }

    private static int[] Range(int start, int end)
    {
        int[] values = new int[end - start + 1];
        for (int i = 0; i < values.Length; i++)
            values[i] = start + i;
        return values;
    }

    private static void AddPowerState(
        Sprite sprite,
        string prefix,
        float idleDelay,
        float walkDelay,
        float loopDelay,
        float attackDelay,
        float burstDelay,
        string attackAnim,
        string burstAnim,
        string loopAnim,
        int idleCount,
        int walkCount,
        int attackCount,
        int burstCount,
        int loopCount)
    {
        sprite.AddLoop($"{prefix}_idle", $"{prefix}/idle", idleDelay, Range(0, idleCount - 1));
        sprite.AddLoop($"{prefix}_walk", $"{prefix}/walk", walkDelay, Range(0, walkCount - 1));
        sprite.Add($"{prefix}_attack", $"{prefix}/{attackAnim}", attackDelay, $"{prefix}_idle", Range(0, attackCount - 1));
        sprite.Add($"{prefix}_{burstAnim}", $"{prefix}/{burstAnim}", burstDelay, $"{prefix}_idle", Range(0, burstCount - 1));
        sprite.AddLoop($"{prefix}_{loopAnim}", $"{prefix}/{loopAnim}", loopDelay, Range(0, loopCount - 1));
    }

    private static void AddMeleePower(Sprite sprite, string prefix, float idleDelay, float walkDelay, params (string name, float delay, int frameCount)[] attacks)
    {
        sprite.AddLoop($"{prefix}_idle", $"{prefix}/idle", idleDelay, Range(0, 3));
        sprite.AddLoop($"{prefix}_walk", $"{prefix}/walk", walkDelay, Range(0, 5));

        foreach ((string name, float delay, int frameCount) in attacks)
            sprite.Add($"{prefix}_{name}", $"{prefix}/{name}", delay, $"{prefix}_idle", Range(0, frameCount - 1));
    }
}
