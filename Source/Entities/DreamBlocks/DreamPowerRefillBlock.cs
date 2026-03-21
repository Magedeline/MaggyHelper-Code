using MaggyHelper.Extensions;
using global::MaggyHelper.Extensions.Kirby;

namespace MaggyHelper.Entities
{
    /// <summary>
    /// A dream-styled pick-up entity that provides different refills depending on
    /// which character mode is active.
    ///
    /// Kirby mode player:
    ///   • Restores hover stamina to full.
    ///   • If <c>grantPower</c> is set to anything other than None, grants that
    ///     copy ability (replacing the current one).
    ///
    /// Normal Madeline (or any non-Kirby player):
    ///   • Restores one dash charge (when <c>refillDash</c> is true).
    ///   • No copy-ability effect.
    ///
    /// Respawns after <see cref="RespawnTime"/> seconds unless <c>oneUse</c> is set.
    /// </summary>
    [CustomEntity("MaggyHelper/DreamPowerRefillBlock")]
    [Tracked]
    [HotReloadable]
    public class DreamPowerRefillBlock : Entity
    {
        // ── constants ─────────────────────────────────────────────────────────────
        private const float RespawnTime = 2.5f;

        // ── configuration ─────────────────────────────────────────────────────────
        private readonly bool                      _oneUse;
        private readonly bool                      _refillDash;
        private readonly KirbyMode.KirbyPowerState _grantPower;

        // ── state ─────────────────────────────────────────────────────────────────
        private bool  _depleted;
        private float _respawnTimer;

        // ── visual components ─────────────────────────────────────────────────────
        private Sprite      _sprite;
        private Wiggler     _wiggler;
        private BloomPoint  _bloom;
        private VertexLight _light;

        // particle types built at Add time
        private ParticleType _pKirby;
        private ParticleType _pNormal;

        // ── palette ───────────────────────────────────────────────────────────────
        private static readonly Color KirbyTint  = Calc.HexToColor("FF69B4");
        private static readonly Color NormalTint  = Calc.HexToColor("5fcde4");

        // ── constructor ───────────────────────────────────────────────────────────
        public DreamPowerRefillBlock(EntityData data, Vector2 offset)
            : base(data.Position + offset)
        {
            _oneUse     = data.Bool("oneUse", false);
            _refillDash = data.Bool("refillDash", true);

            string powerStr = data.Attr("grantPower", "None");
            if (!Enum.TryParse<KirbyMode.KirbyPowerState>(powerStr, out _grantPower))
                _grantPower = KirbyMode.KirbyPowerState.None;

            Collider = new Hitbox(16f, 16f, -8f, -8f);

            Add(new PlayerCollider(OnPlayer));
            Add(_wiggler = Wiggler.Create(1f, 4f, v =>
            {
                if (_sprite != null)
                    _sprite.Scale = Vector2.One * (1f + v * 0.2f);
            }));
            Add(_bloom = new BloomPoint(0.8f, 16f));
            Add(_light = new VertexLight(KirbyTint, 1f, 16, 48));
        }

        // ── lifecycle ─────────────────────────────────────────────────────────────
        public override void Added(Scene scene)
        {
            base.Added(scene);

            // Sprite: fall back to vanilla refill art if custom sheet is absent.
            const string customPath = "objects/Ingeste/dreamPowerRefillBlock/";
            const string fallback   = "objects/refill/";

            string path = GFX.Game.Has(customPath + "idle00") ? customPath : fallback;
            _sprite = new Sprite(GFX.Game, path);
            _sprite.AddLoop("idle", "idle", 0.1f);
            _sprite.CenterOrigin();
            _sprite.Play("idle");
            _sprite.Color = KirbyTint;
            Add(_sprite);

            // Build particle descriptors.
            _pKirby = new ParticleType(Refill.P_Regen)
            {
                Color  = KirbyTint,
                Color2 = Calc.HexToColor("FFB6C1"),
            };
            _pNormal = new ParticleType(Refill.P_Regen)
            {
                Color  = NormalTint,
                Color2 = Color.White,
            };
        }

        // ── update ────────────────────────────────────────────────────────────────
        public override void Update()
        {
            base.Update();

            if (_depleted)
            {
                // Count down to respawn (skipped when oneUse).
                if (!_oneUse)
                {
                    _respawnTimer -= Engine.DeltaTime;
                    if (_respawnTimer <= 0f)
                        Respawn();
                }
                return;
            }

            // Gentle light pulse.
            _light.Alpha = 0.8f + (float)Math.Sin(Scene.TimeActive * 2f) * 0.2f;
        }

        // ── player collision ──────────────────────────────────────────────────────
        private void OnPlayer(Player player)
        {
            if (_depleted)
                return;

            var    level     = SceneAs<Level>();
            bool   activated = false;

            if (player.IsKirbyMode())
            {
                var kirbyExt = level?.Tracker.GetEntity<KirbyPlayerExtension>();
                if (kirbyExt != null)
                {
                    // Restore hover stamina.
                    kirbyExt.CurrentStamina = kirbyExt.MaxStamina;

                    // Grant copy ability if one was specified.
                    if (_grantPower != KirbyMode.KirbyPowerState.None)
                        kirbyExt.SetPowerState(_grantPower);

                    level?.ParticlesBG.Emit(_pKirby, 20, Center, Vector2.One * 4f);
                    Audio.Play(SFX.game_gen_diamond_touch, Position);
                    activated = true;
                }
            }
            else if (_refillDash && player.Dashes < player.MaxDashes)
            {
                player.RefillDash();
                level?.ParticlesBG.Emit(_pNormal, 20, Center, Vector2.One * 4f);
                Audio.Play(SFX.game_gen_diamond_touch, Position);
                activated = true;
            }

            if (activated)
            {
                _wiggler.Start();
                Deplete();
            }
        }

        // ── helpers ───────────────────────────────────────────────────────────────
        private void Deplete()
        {
            _depleted          = true;
            _respawnTimer      = RespawnTime;
            _sprite.Visible    = false;
            _bloom.Alpha       = 0f;
            _light.Alpha       = 0f;
        }

        private void Respawn()
        {
            _depleted       = false;
            _sprite.Visible = true;
            _bloom.Alpha    = 1f;
            _light.Alpha    = 1f;
            _wiggler.Start();
            Audio.Play(SFX.game_gen_diamond_return, Position);
            SceneAs<Level>()?.ParticlesBG.Emit(_pKirby, 12, Center, Vector2.One * 4f);
        }
    }
}
