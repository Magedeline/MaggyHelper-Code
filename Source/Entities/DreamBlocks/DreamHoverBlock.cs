using MaggyHelper.Extensions;
using MaggyHelper.Extensions.Kirby;

namespace MaggyHelper.Entities
{
    /// <summary>
    /// A dream-styled solid block that only Kirby can hover / float through.
    ///
    /// Unlike KirbyDreamBlock, this block does NOT support the vanilla dream dash —
    /// it is intended as a Kirby-exclusive passage (e.g. secret areas, power-gated
    /// routes, or tutorial blocks for the hover mechanic).
    ///
    /// When <c>requireKirbyMode</c> is false, any player can pass through while the
    /// hover input is held (useful for teaching the mechanic before unlocking Kirby).
    ///
    /// Visual: solid black background with pink/rose dream particles and a hot-pink
    /// outline, clearly distinguishing it from vanilla dream blocks.
    /// </summary>
    [CustomEntity("MaggyHelper/DreamHoverBlock")]
    [Tracked]
    [HotReloadable]
    public class DreamHoverBlock : Solid
    {
        // ── palette ──────────────────────────────────────────────────────────────
        private static readonly Color BackColor = Color.Black;
        private static readonly Color OutlineColor = Calc.HexToColor("FF69B4");

        private static readonly Color[] ParticlePalette =
        {
            Calc.HexToColor("FF69B4"),
            Calc.HexToColor("FF1493"),
            Calc.HexToColor("FFB6C1"),
            Calc.HexToColor("DB7093"),
        };

        // ── particles ────────────────────────────────────────────────────────────
        private struct HoverParticle
        {
            public Vector2 Position;
            public int     Layer;
            public Color   Color;
            public float   TimeOffset;
        }

        private HoverParticle[] _particles;
        private MTexture[]      _particleTextures;
        private float           _animTimer;
        private float           _wobbleEase;
        private float           _wobbleFrom;
        private float           _wobbleTo;

        // ── state ─────────────────────────────────────────────────────────────────
        private bool _kirbyPassActive;
        private bool _requireKirbyMode;

        // ── constructor ───────────────────────────────────────────────────────────
        public DreamHoverBlock(EntityData data, Vector2 offset)
            : base(data.Position + offset, data.Width, data.Height, true)
        {
            Depth = -11000;
            _requireKirbyMode = data.Bool("requireKirbyMode", true);

            _wobbleFrom = Calc.Random.NextFloat((float)(Math.PI * 2));
            _wobbleTo   = Calc.Random.NextFloat((float)(Math.PI * 2));

            _particleTextures = new MTexture[4]
            {
                GFX.Game["objects/dreamblock/particles"].GetSubtexture(14, 0, 7, 7),
                GFX.Game["objects/dreamblock/particles"].GetSubtexture(7,  0, 7, 7),
                GFX.Game["objects/dreamblock/particles"].GetSubtexture(0,  0, 7, 7),
                GFX.Game["objects/dreamblock/particles"].GetSubtexture(7,  0, 7, 7),
            };
        }

        // ── lifecycle ─────────────────────────────────────────────────────────────
        public override void Added(Scene scene)
        {
            base.Added(scene);
            SetupParticles();
        }

        private void SetupParticles()
        {
            _particles = new HoverParticle[(int)(Width / 8f * (Height / 8f) * 0.7f)];
            for (int i = 0; i < _particles.Length; i++)
            {
                _particles[i] = new HoverParticle
                {
                    Position  = new Vector2(Calc.Random.NextFloat(Width), Calc.Random.NextFloat(Height)),
                    Layer     = Calc.Random.Choose(0, 1, 1, 2, 2, 2),
                    TimeOffset = Calc.Random.NextFloat(),
                    Color     = Calc.Random.Choose(ParticlePalette),
                };
            }
        }

        // ── update ────────────────────────────────────────────────────────────────
        public override void Update()
        {
            base.Update();

            _animTimer += 6f * Engine.DeltaTime;
            _wobbleEase += Engine.DeltaTime * 2f;
            if (_wobbleEase > 1f)
            {
                _wobbleEase = 0f;
                _wobbleFrom = _wobbleTo;
                _wobbleTo   = Calc.Random.NextFloat((float)(Math.PI * 2));
            }

            HandleHoverPassthrough();
        }

        private void HandleHoverPassthrough()
        {
            var player = Scene?.Tracker.GetEntity<Player>();
            if (player == null)
                return;

            bool inKirbyMode = player.IsKirbyMode();

            // If this block requires Kirby mode and the player isn't in it, stay solid.
            if (_requireKirbyMode && !inKirbyMode)
            {
                RestoreCollidable();
                return;
            }

            // Only open for Kirby when hovering; or for any player when
            // requireKirbyMode is off and they press the hover button.
            var kirbyExt  = (Scene as Level)?.Tracker.GetEntity<KirbyPlayerExtension>();
            bool isHovering = kirbyExt?.AbilityManager?.Get<KirbyHoverAbility>()?.IsHovering ?? false;

            if (isHovering)
            {
                bool nearOrInside = CollideCheck(player)
                    || (player.Speed.LengthSquared() > 4f
                        && CollideCheck(player, player.Position + player.Speed.SafeNormalize(4f)));

                if (nearOrInside)
                {
                    Collidable       = false;
                    _kirbyPassActive = true;
                }
                else if (_kirbyPassActive && !CollideCheck(player))
                {
                    RestoreCollidable();
                }
            }
            else
            {
                RestoreCollidable();
            }
        }

        private void RestoreCollidable()
        {
            if (_kirbyPassActive)
            {
                Collidable       = true;
                _kirbyPassActive = false;
            }
        }

        // ── render ────────────────────────────────────────────────────────────────
        public override void Render()
        {
            // Solid black background.
            Draw.Rect(X, Y, Width, Height, BackColor);

            // Scrolling dream particles.
            if (_particles != null)
            {
                for (int i = 0; i < _particles.Length; i++)
                {
                    int layer = _particles[i].Layer;

                    float scrollX = (_particles[i].Position.X + _animTimer * (1f + layer * 0.15f)) % Width;
                    float scrollY = (_particles[i].Position.Y + _animTimer * (0.5f + layer * 0.07f)) % Height;
                    if (scrollX < 0f) scrollX += Width;
                    if (scrollY < 0f) scrollY += Height;

                    Vector2 drawPos = new Vector2(X + scrollX, Y + scrollY);
                    Color   c       = _particles[i].Color * (0.5f + layer / 2f * 0.5f);
                    float   scale   = 0.5f + (float)Math.Sin(_animTimer * 1.5f + _particles[i].TimeOffset * 9f) * 0.15f;

                    _particleTextures[layer % _particleTextures.Length].DrawCentered(drawPos, c, scale);
                }
            }

            // Hot-pink dream border.
            Draw.HollowRect(X, Y, Width, Height, OutlineColor * 0.8f);
        }
    }
}
