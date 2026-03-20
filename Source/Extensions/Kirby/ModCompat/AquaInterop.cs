using System.Reflection;

namespace MaggyHelper.Extensions.Kirby.ModCompat
{
    internal sealed class AquaInterop
    {
        private static readonly EverestModuleMetadata AquaMetadata = new()
        {
            Name = "Aqua",
            Version = new Version(0, 0, 0)
        };

        private bool _resolved;
        private bool _available;
        private MethodInfo _getGrapplingHook;
        private MethodInfo _getGrapplingHookState;
        private MethodInfo _getGrapplingHookRopeDirection;
        private MethodInfo _getGrapplingHookTangent;
        private MethodInfo _revokeGrapplingHook;
        private MethodInfo _isEntityHookable;
        private MethodInfo _setEntityHookable;
        private MethodInfo _isHookAttached;
        private MethodInfo _isIntersectsWithRope;
        private MethodInfo _setEntityAttachCallbacks;
        private MethodInfo _registerGrapplingHookCollision;

        public bool IsAvailable => _available;

        public void Resolve()
        {
            if (_resolved)
            {
                return;
            }

            _resolved = true;

            if (!Everest.Loader.TryGetDependency(AquaMetadata, out EverestModule module))
            {
                return;
            }

            Type exportsType = module.GetType().Assembly.GetType("Celeste.Mod.Aqua.Module.AquaExports", throwOnError: false);
            if (exportsType == null)
            {
                Logger.Log(LogLevel.Warn, "KirbyModCompat", "Aqua detected but AquaExports could not be resolved.");
                return;
            }

            _getGrapplingHook = exportsType.GetMethod("GetGrapplingHook", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Player) }, null);
            _getGrapplingHookState = exportsType.GetMethod("GetGrapplingHookState", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Entity) }, null);
            _getGrapplingHookRopeDirection = exportsType.GetMethod("GetGrapplingHookRopeDirection", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Entity) }, null);
            _getGrapplingHookTangent = exportsType.GetMethod("GetGrapplingHookTangent", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Entity) }, null);
            _revokeGrapplingHook = exportsType.GetMethod("RevokeGrapplingHook", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Entity) }, null);
            _isEntityHookable = exportsType.GetMethod("IsEntityHookable", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Entity) }, null);
            _setEntityHookable = exportsType.GetMethod("SetEntityHookable", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Entity), typeof(bool) }, null);
            _isHookAttached = exportsType.GetMethod("IsHookAttached", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Entity) }, null);
            _isIntersectsWithRope = exportsType.GetMethod("IsIntersectsWithRope", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Entity) }, null);
            _setEntityAttachCallbacks = exportsType.GetMethod("SetEntityAttachCallbacks", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Entity), typeof(Action<Entity>), typeof(Action<Entity>) }, null);
            _registerGrapplingHookCollision = exportsType.GetMethod("RegisterGrapplingHookCollision", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Entity), typeof(Action<Entity>) }, null);

            _available = _getGrapplingHook != null
                && _getGrapplingHookState != null
                && _revokeGrapplingHook != null
                && _setEntityHookable != null
                && _isHookAttached != null;

            if (!_available)
            {
                Logger.Log(LogLevel.Warn, "KirbyModCompat", "Aqua detected but required exports are missing; Aqua bridge disabled.");
            }
        }

        public Entity GetGrapplingHook(Player player)
        {
            return Invoke<Entity>(_getGrapplingHook, null, player);
        }

        public int GetGrapplingHookState(Entity hook)
        {
            if (hook == null)
            {
                return 0;
            }

            return Invoke(_getGrapplingHookState, 0, hook);
        }

        public Vector2 GetGrapplingHookRopeDirection(Entity hook)
        {
            if (hook == null)
            {
                return Vector2.Zero;
            }

            return Invoke(_getGrapplingHookRopeDirection, Vector2.Zero, hook);
        }

        public Vector2 GetGrapplingHookTangent(Entity hook)
        {
            if (hook == null)
            {
                return Vector2.Zero;
            }

            return Invoke(_getGrapplingHookTangent, Vector2.Zero, hook);
        }

        public void RevokeGrapplingHook(Entity hook)
        {
            if (hook == null || _revokeGrapplingHook == null)
            {
                return;
            }

            try
            {
                _revokeGrapplingHook.Invoke(null, new object[] { hook });
            }
            catch
            {
            }
        }

        public bool IsEntityHookable(Entity entity)
        {
            if (entity == null)
            {
                return false;
            }

            return Invoke(_isEntityHookable, false, entity);
        }

        public bool SetEntityHookable(Entity entity, bool hookable)
        {
            if (entity == null || _setEntityHookable == null)
            {
                return false;
            }

            try
            {
                _setEntityHookable.Invoke(null, new object[] { entity, hookable });
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool IsHookAttached(Entity entity)
        {
            if (entity == null)
            {
                return false;
            }

            return Invoke(_isHookAttached, false, entity);
        }

        public bool IsIntersectsWithRope(Entity entity)
        {
            if (entity == null)
            {
                return false;
            }

            return Invoke(_isIntersectsWithRope, false, entity);
        }

        public bool SetEntityAttachCallbacks(Entity entity, Action<Entity> onAttach, Action<Entity> onDetach)
        {
            if (entity == null || _setEntityAttachCallbacks == null)
            {
                return false;
            }

            try
            {
                _setEntityAttachCallbacks.Invoke(null, new object[] { entity, onAttach, onDetach });
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool RegisterGrapplingHookCollision(Entity entity, Action<Entity> onCollision)
        {
            if (entity == null || _registerGrapplingHookCollision == null || onCollision == null)
            {
                return false;
            }

            try
            {
                _registerGrapplingHookCollision.Invoke(null, new object[] { entity, onCollision });
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static T Invoke<T>(MethodInfo method, T fallback, params object[] args)
        {
            if (method == null)
            {
                return fallback;
            }

            try
            {
                object result = method.Invoke(null, args);
                if (result is T typed)
                {
                    return typed;
                }
            }
            catch
            {
            }

            return fallback;
        }
    }
}