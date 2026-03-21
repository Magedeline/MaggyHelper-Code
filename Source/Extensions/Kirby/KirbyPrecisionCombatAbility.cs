using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;

namespace MaggyHelper.Extensions.Kirby
{
    /// <summary>
    /// Precision combat controller for Kirby.
    ///
    /// This ability introduces a deterministic punch/parry state machine with
    /// frame-data driven timings and input buffering. It is intentionally scoped
    /// as a foundation layer: it handles timing, animation, and mode switching
    /// while leaving hit resolution/parry reactions for dedicated combat systems.
    /// </summary>
    public sealed class KirbyPrecisionCombatAbility : KirbyAbilityBase, IKirbyAnimationProvider
    {
        public override string AbilityId => "precision_combat";
        public override string DisplayName => "Precision Combat";

        private enum CombatState
        {
            Idle,
            PunchStartup,
            PunchActive,
            PunchRecovery,
            ParryStartup,
            ParryActive,
            ParryRecovery
        }

        private readonly struct FrameData
        {
            public readonly int Startup;
            public readonly int Active;
            public readonly int Recovery;

            public FrameData(int startup, int active, int recovery)
            {
                Startup = startup;
                Active = active;
                Recovery = recovery;
            }
        }

        private CombatState _state;
        private float _stateTimer;
        private float _punchBufferTimer;
        private float _parryBufferTimer;
        private int _punchChainStep;
        private float _punchChainTimer;
        private readonly HashSet<Refill> _consumedRefills = new();
        private int _combatRefillCharges;
        private bool _chargedPunch;
        private bool _chargedParry;

        private static readonly FrameData DefaultPunchData = new(startup: 4, active: 3, recovery: 7);
        private static readonly FrameData DefaultParryData = new(startup: 2, active: 6, recovery: 10);

        private const float PunchChainWindowSeconds = 0.30f;
        private const int MaxCombatRefillCharges = 3;
        private const int ChargedParryBonusFrames = 2;

        public bool CombatModeActive { get; private set; }
        public bool IsParryWindowActive => _state == CombatState.ParryActive;
        public bool IsPunchActive => _state == CombatState.PunchActive;
        public int CombatRefillCharges => _combatRefillCharges;

        protected override void OnAttach()
        {
            base.OnAttach();

            var session = MaggyHelperModule.Session;
            if (session != null)
            {
                CombatModeActive = session.GetFlag("kirby_precision_combat_mode");
            }
            else
            {
                CombatModeActive = Settings?.CombatModeDefault ?? false;
            }
        }

        protected override void OnUpdate()
        {
            if (Player == null || Extension.IsDead || Settings == null || !Settings.PrecisionCombatEnabled)
            {
                if (IsExecuting)
                {
                    Cancel();
                }
                return;
            }

            if (Settings.IsKeyPressed("CombatToggle"))
            {
                SetCombatMode(!CombatModeActive);
            }

            TickTimers();

            if (!CombatModeActive)
            {
                if (IsExecuting)
                {
                    Cancel();
                }
                _state = CombatState.Idle;
                return;
            }

            TryCaptureCombatRefill();

            CaptureInputBuffer();

            if (_state == CombatState.Idle)
            {
                TryConsumeBuffer();
                return;
            }

            _stateTimer -= Engine.DeltaTime;
            if (_stateTimer <= 0f)
            {
                AdvanceState();
            }
        }

        protected override void OnCancel()
        {
            _state = CombatState.Idle;
            _stateTimer = 0f;
            _punchBufferTimer = 0f;
            _parryBufferTimer = 0f;
            _punchChainTimer = 0f;
            _punchChainStep = 0;
            _chargedPunch = false;
            _chargedParry = false;
        }

        public bool TryParry(Vector2 source)
        {
            if (!CombatModeActive || !IsParryWindowActive || Level == null)
            {
                return false;
            }

            // Lightweight reaction feedback; gameplay consequences are handled elsewhere.
            Level.DirectionalShake((Player.Position - source).SafeNormalize(), 0.08f);
            EmitParticles(ParticleTypes.SparkyDust, Player.Position, 6, Vector2.One * 8f);
            return true;
        }

        public string GetAnimationId()
        {
            if (!CombatModeActive)
            {
                return null;
            }

            return _state switch
            {
                CombatState.PunchStartup or CombatState.PunchActive or CombatState.PunchRecovery
                    => _punchChainStep >= 2 ? KirbyAnimIds.CombatPunchB : KirbyAnimIds.CombatPunchA,
                CombatState.ParryStartup or CombatState.ParryActive or CombatState.ParryRecovery
                    => KirbyAnimIds.CombatBackflip,
                _ => null
            };
        }

        private void TickTimers()
        {
            if (_punchBufferTimer > 0f)
            {
                _punchBufferTimer = Math.Max(0f, _punchBufferTimer - Engine.DeltaTime);
            }

            if (_parryBufferTimer > 0f)
            {
                _parryBufferTimer = Math.Max(0f, _parryBufferTimer - Engine.DeltaTime);
            }

            if (_punchChainTimer > 0f)
            {
                _punchChainTimer = Math.Max(0f, _punchChainTimer - Engine.DeltaTime);
                if (_punchChainTimer <= 0f)
                {
                    _punchChainStep = 0;
                }
            }
        }

        private void CaptureInputBuffer()
        {
            if (Settings.IsKeyPressed("Punch"))
            {
                _punchBufferTimer = GetBufferSeconds();
            }

            if (Settings.IsKeyPressed("Parry"))
            {
                _parryBufferTimer = GetBufferSeconds();
            }
        }

        private void TryConsumeBuffer()
        {
            // Parry gets priority because it is a strict defensive window action.
            if (_parryBufferTimer > 0f)
            {
                _parryBufferTimer = 0f;
                BeginParry();
                return;
            }

            if (_punchBufferTimer > 0f)
            {
                _punchBufferTimer = 0f;
                BeginPunch();
            }
        }

        private void BeginPunch()
        {
            IsExecuting = true;
            _state = CombatState.PunchStartup;
            _stateTimer = ToSeconds(GetPunchData().Startup);
            _chargedPunch = ConsumeCombatCharge();

            if (_punchChainTimer > 0f)
            {
                _punchChainStep = Math.Min(2, _punchChainStep + 1);
            }
            else
            {
                _punchChainStep = 0;
            }

            _punchChainTimer = PunchChainWindowSeconds;
        }

        private void BeginParry()
        {
            IsExecuting = true;
            _state = CombatState.ParryStartup;
            _stateTimer = ToSeconds(GetParryData().Startup);
            _chargedParry = ConsumeCombatCharge();
        }

        private void AdvanceState()
        {
            switch (_state)
            {
                case CombatState.PunchStartup:
                    _state = CombatState.PunchActive;
                    _stateTimer = ToSeconds(GetPunchData().Active);
                    if (_chargedPunch)
                    {
                        EmitParticles(ParticleTypes.SparkyDust, Player.Position + FacingDirection * 8f, 4, Vector2.One * 6f);
                        Level?.DirectionalShake(FacingDirection, 0.03f);
                    }
                    break;
                case CombatState.PunchActive:
                    _state = CombatState.PunchRecovery;
                    _stateTimer = ToSeconds(GetPunchData().Recovery);
                    break;
                case CombatState.PunchRecovery:
                    EndAction();
                    break;
                case CombatState.ParryStartup:
                    _state = CombatState.ParryActive;
                    _stateTimer = ToSeconds(GetParryData().Active + (_chargedParry ? ChargedParryBonusFrames : 0));
                    if (_chargedParry)
                    {
                        EmitParticles(ParticleTypes.SparkyDust, Player.Position, 5, Vector2.One * 8f);
                    }
                    break;
                case CombatState.ParryActive:
                    _state = CombatState.ParryRecovery;
                    _stateTimer = ToSeconds(GetParryData().Recovery);
                    break;
                case CombatState.ParryRecovery:
                    EndAction();
                    break;
                default:
                    EndAction();
                    break;
            }
        }

        private void EndAction()
        {
            _state = CombatState.Idle;
            _stateTimer = 0f;
            IsExecuting = false;
            _chargedPunch = false;
            _chargedParry = false;
        }

        private void TryCaptureCombatRefill()
        {
            if (Level?.Tracker == null || Player == null)
                return;

            List<Entity> refillEntities = Level.Tracker.GetEntities<Refill>();
            for (int i = 0; i < refillEntities.Count; i++)
            {
                if (refillEntities[i] is not Refill refill)
                    continue;

                if (!refill.Active || _consumedRefills.Contains(refill))
                    continue;

                if (!Player.CollideCheck(refill))
                    continue;

                _consumedRefills.Add(refill);
                _combatRefillCharges = Math.Min(MaxCombatRefillCharges, _combatRefillCharges + 1);
                EmitParticles(ParticleTypes.SparkyDust, Player.Position, 6, Vector2.One * 6f);
                break;
            }

            _consumedRefills.RemoveWhere(r => r == null || r.Scene == null);
        }

        private bool ConsumeCombatCharge()
        {
            if (_combatRefillCharges <= 0)
                return false;

            _combatRefillCharges--;
            return true;
        }

        private float GetBufferSeconds() => ToSeconds(Settings?.CombatInputBufferFrames ?? 5);

        private float ToSeconds(int frames)
        {
            if (Settings == null)
            {
                return frames / 60f;
            }

            return Settings.FramesToSeconds(frames);
        }

        private FrameData GetPunchData()
        {
            if (Settings == null)
            {
                return DefaultPunchData;
            }

            return new FrameData(Settings.PunchStartupFrames, Settings.PunchActiveFrames, Settings.PunchRecoveryFrames);
        }

        private FrameData GetParryData()
        {
            if (Settings == null)
            {
                return DefaultParryData;
            }

            return new FrameData(Settings.ParryStartupFrames, Settings.ParryActiveFrames, Settings.ParryRecoveryFrames);
        }

        private void SetCombatMode(bool enabled)
        {
            CombatModeActive = enabled;
            if (!enabled)
            {
                Cancel();
            }

            var session = MaggyHelperModule.Session;
            session?.SetFlag("kirby_precision_combat_mode", enabled);
        }
    }
}
