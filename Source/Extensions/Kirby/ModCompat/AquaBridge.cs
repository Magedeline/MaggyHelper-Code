namespace MaggyHelper.Extensions.Kirby.ModCompat
{
    public sealed class AquaBridge : IKirbyModBridge
    {
        private const int HookStateNone = 0;
        private const int HookStateEmitting = 1;
        private const int HookStateRevoking = 2;
        private const int HookStateBouncing = 3;
        private const int HookStateFixed = 4;
        private const int HookStateAttracted = 5;

        private readonly AquaInterop _interop = new();
        private KirbyPlayerExtension _boundKirby;
        private bool _kirbyMarkedNonHookable;
        private bool _kirbyCallbacksRegistered;
        private bool _kirbyCollisionRegistered;
        private Entity _pendingHookFromCallback;

        private readonly Action<Entity> _onKirbyHookAttached;
        private readonly Action<Entity> _onKirbyHookDetached;
        private readonly Action<Entity> _onKirbyHookCollision;

        public AquaBridge()
        {
            _onKirbyHookAttached = OnKirbyHookAttached;
            _onKirbyHookDetached = OnKirbyHookDetached;
            _onKirbyHookCollision = OnKirbyHookCollision;
        }

        public string ModName => "Aqua";
        public bool IsActive => KirbyModCompatManager.AquaLoaded;

        public void Load()
        {
            if (!IsActive)
            {
                return;
            }

            _interop.Resolve();
            ResetKirbyBindingState();
            On.Celeste.Player.Die += OnPlayerDie_AquaCompat;

            if (_interop.IsAvailable)
            {
                Logger.Log(LogLevel.Info, "KirbyModCompat", "Aqua bridge: loaded optional grappling hook compat");
            }
        }

        public void Unload()
        {
            On.Celeste.Player.Die -= OnPlayerDie_AquaCompat;
            ResetKirbyBindingState();
        }

        public void UpdateKirby(KirbyPlayerExtension kirby, Player player, Level level)
        {
            if (!IsActive || !_interop.IsAvailable || kirby == null || player == null)
            {
                return;
            }

            EnsureBoundKirby(kirby);
            EnsureKirbyIsNotHookable(kirby);
            EnsureKirbyHookCallbacks(kirby);
            kirby.CompatAnimationOverride = null;

            Entity hook = _interop.GetGrapplingHook(player);
            if (hook == null)
            {
                _pendingHookFromCallback = null;
                return;
            }

            if (_interop.IsHookAttached(kirby))
            {
                _interop.RevokeGrapplingHook(hook);
                return;
            }

            if (_pendingHookFromCallback != null)
            {
                _interop.RevokeGrapplingHook(_pendingHookFromCallback);
                _pendingHookFromCallback = null;
                return;
            }

            int hookState = _interop.GetGrapplingHookState(hook);
            if (hookState == HookStateFixed || hookState == HookStateAttracted)
            {
                CancelConflictingAbilities(kirby);
            }

            kirby.CompatAnimationOverride = ResolveHookAnimationOverride(player, hook, hookState);
        }

        public bool OnDamage(KirbyPlayerExtension kirby, int amount, Vector2 source)
        {
            return false;
        }

        public bool OnDeath(KirbyPlayerExtension kirby, Player player)
        {
            CleanupHookOnDeath(player);
            return false;
        }

        public void OnDashCountChanged(KirbyPlayerExtension kirby, Player player, int newCount)
        {
        }

        private PlayerDeadBody OnPlayerDie_AquaCompat(
            On.Celeste.Player.orig_Die orig,
            Player self,
            Vector2 direction,
            bool evenIfInvincible,
            bool registerDeathInStats)
        {
            CleanupHookOnDeath(self);
            return orig(self, direction, evenIfInvincible, registerDeathInStats);
        }

        private void CleanupHookOnDeath(Player player)
        {
            if (!IsActive || !_interop.IsAvailable || player?.Scene is not Level level)
            {
                return;
            }

            if (!player.IsKirbyMode() || level.Tracker.GetEntity<KirbyPlayerExtension>() == null)
            {
                return;
            }

            Entity hook = _interop.GetGrapplingHook(player);
            int hookState = _interop.GetGrapplingHookState(hook);
            if (hook != null && hookState != HookStateNone && hookState != HookStateRevoking)
            {
                _interop.RevokeGrapplingHook(hook);
            }

            _pendingHookFromCallback = null;
        }

        private void EnsureBoundKirby(KirbyPlayerExtension kirby)
        {
            if (ReferenceEquals(_boundKirby, kirby))
            {
                return;
            }

            ResetKirbyBindingState();
            _boundKirby = kirby;
        }

        private void ResetKirbyBindingState()
        {
            _boundKirby = null;
            _kirbyMarkedNonHookable = false;
            _kirbyCallbacksRegistered = false;
            _kirbyCollisionRegistered = false;
            _pendingHookFromCallback = null;
        }

        private void EnsureKirbyIsNotHookable(KirbyPlayerExtension kirby)
        {
            if (_kirbyMarkedNonHookable && !_interop.IsEntityHookable(kirby))
            {
                return;
            }

            if (_interop.SetEntityHookable(kirby, false))
            {
                _kirbyMarkedNonHookable = true;
            }
        }

        private void EnsureKirbyHookCallbacks(KirbyPlayerExtension kirby)
        {
            if (!_kirbyCallbacksRegistered)
            {
                _kirbyCallbacksRegistered = _interop.SetEntityAttachCallbacks(kirby, _onKirbyHookAttached, _onKirbyHookDetached);
            }

            if (!_kirbyCollisionRegistered)
            {
                _kirbyCollisionRegistered = _interop.RegisterGrapplingHookCollision(kirby, _onKirbyHookCollision);
            }
        }

        private void OnKirbyHookAttached(Entity hook)
        {
            _pendingHookFromCallback = hook;
        }

        private void OnKirbyHookDetached(Entity hook)
        {
            if (_pendingHookFromCallback == hook)
            {
                _pendingHookFromCallback = null;
            }
        }

        private void OnKirbyHookCollision(Entity hook)
        {
            _pendingHookFromCallback = hook;
        }

        private static void CancelConflictingAbilities(KirbyPlayerExtension kirby)
        {
            KirbyHoverAbility hover = kirby.Hover;
            if (hover != null && hover.IsHovering)
            {
                hover.Cancel();
            }

            KirbyInhaleAbility inhale = kirby.Inhale;
            if (inhale != null && inhale.IsExecuting)
            {
                inhale.Cancel();
            }
        }

        private string ResolveHookAnimationOverride(Player player, Entity hook, int hookState)
        {
            switch (hookState)
            {
                case HookStateEmitting:
                case HookStateBouncing:
                    return "dash";

                case HookStateAttracted:
                    return "hover";

                case HookStateFixed:
                {
                    Vector2 tangent = _interop.GetGrapplingHookTangent(hook);
                    Vector2 ropeDirection = _interop.GetGrapplingHookRopeDirection(hook);

                    if (Math.Abs(player.Speed.Y) > 20f)
                    {
                        return player.Speed.Y < 0f ? "jump" : "fall";
                    }

                    if (Math.Abs(tangent.X) > 0.35f && Math.Abs(player.Speed.X) > 40f)
                    {
                        return "run";
                    }

                    if (ropeDirection.Y > 0.2f && !player.OnGround())
                    {
                        return "hover";
                    }

                    return null;
                }
            }

            return null;
        }
    }
}