using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Monocle;

namespace MaggyHelper.Extensions.Kirby
{
    /// <summary>
    /// Manages registration, lifecycle, and dispatch for all Kirby abilities.
    /// Acts as the central hub connecting the KirbyPlayerExtension to individual ability modules.
    /// </summary>
    public class KirbyAbilityManager
    {
        private readonly KirbyPlayerExtension _extension;
        private readonly List<KirbyAbilityBase> _abilities = new();
        private readonly Dictionary<string, KirbyAbilityBase> _abilityMap = new();

        public KirbyAbilityManager(KirbyPlayerExtension extension)
        {
            _extension = extension;
        }

        #region Registration

        /// <summary>Register an ability. Calls Attach() immediately.</summary>
        public void Register(KirbyAbilityBase ability)
        {
            if (ability == null || _abilityMap.ContainsKey(ability.AbilityId))
                return;

            _abilities.Add(ability);
            _abilityMap[ability.AbilityId] = ability;
            ability.Attach(_extension);

            IngesteLogger.Debug($"KirbyAbilityManager: Registered ability '{ability.AbilityId}'");
        }

        /// <summary>Unregister an ability. Calls Detach().</summary>
        public void Unregister(string abilityId)
        {
            if (!_abilityMap.TryGetValue(abilityId, out var ability))
                return;

            ability.Cancel();
            ability.Detach();
            _abilities.Remove(ability);
            _abilityMap.Remove(abilityId);
        }

        /// <summary>Get an ability by type.</summary>
        public T Get<T>() where T : KirbyAbilityBase
        {
            foreach (var a in _abilities)
            {
                if (a is T typed) return typed;
            }
            return null;
        }

        /// <summary>Get an ability by id.</summary>
        public KirbyAbilityBase Get(string id)
        {
            _abilityMap.TryGetValue(id, out var a);
            return a;
        }

        /// <summary>All registered abilities (read-only).</summary>
        public IReadOnlyList<KirbyAbilityBase> All => _abilities;

        #endregion

        #region Lifecycle

        /// <summary>Called when the extension entity is added to the scene.</summary>
        public void OnAdded(Scene scene)
        {
            // Abilities may need scene references; they can access via Extension.Player.Scene
        }

        /// <summary>Called when the extension entity is removed from the scene.</summary>
        public void OnRemoved(Scene scene)
        {
            foreach (var a in _abilities)
                a.Cancel();
        }

        /// <summary>Update all abilities. Called every frame from KirbyPlayerExtension.Update().</summary>
        public void Update()
        {
            for (int i = 0; i < _abilities.Count; i++)
            {
                _abilities[i].Update();
            }
        }

        /// <summary>Render all ability overlays.</summary>
        public void Render()
        {
            for (int i = 0; i < _abilities.Count; i++)
            {
                _abilities[i].Render();
            }
        }

        /// <summary>Called on respawn — resets all abilities.</summary>
        public void OnRespawn()
        {
            foreach (var a in _abilities)
            {
                a.Cancel();
                a.Cooldown = 0f;
            }
        }

        #endregion

        #region Animation Priority

        /// <summary>
        /// Returns the animation id from the highest-priority executing ability,
        /// or null if no ability is driving animation.
        /// Priority order: Melee > Range > Inhale > Spit > Copy > Hover
        /// </summary>
        public string GetActiveAnimation()
        {
            // Check each ability in priority order
            foreach (var a in _abilities)
            {
                if (a.IsExecuting && a is IKirbyAnimationProvider provider)
                {
                    string anim = provider.GetAnimationId();
                    if (anim != null) return anim;
                }
            }
            return null;
        }

        #endregion

        #region Queries

        /// <summary>Whether any ability is currently executing.</summary>
        public bool AnyExecuting => _abilities.Any(a => a.IsExecuting);

        /// <summary>Cancel all executing abilities.</summary>
        public void CancelAll()
        {
            foreach (var a in _abilities)
                a.Cancel();
        }

        #endregion
    }

    /// <summary>
    /// Interface for abilities that want to drive the Kirby animation.
    /// </summary>
    public interface IKirbyAnimationProvider
    {
        /// <summary>Return the animation id while this ability is executing, or null.</summary>
        string GetAnimationId();
    }
}
