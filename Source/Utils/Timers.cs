using System;

namespace MaggyHelper.Utils
{
    /// <summary>
    /// Lightweight timer / cooldown utility inspired by MonoGame.Extended's <c>CooldownComponent</c>.
    /// Replaces the scattered <c>float timer -= Engine.DeltaTime</c> patterns
    /// found in triggers, obstacles, and projectiles.
    /// </summary>
    public struct Cooldown
    {
        /// <summary>Total duration in seconds.</summary>
        public float Duration { get; set; }

        /// <summary>Time remaining in seconds. Clamps to [0, <see cref="Duration"/>].</summary>
        public float Remaining { get; private set; }

        /// <summary>True when <see cref="Remaining"/> has reached zero.</summary>
        public bool IsReady => Remaining <= 0f;

        /// <summary>True while the timer is still counting down.</summary>
        public bool IsActive => Remaining > 0f;

        /// <summary>Progress from 0 (just started) to 1 (ready). Useful for lerp/alpha.</summary>
        public float Progress => Duration > 0f
            ? MathF.Min(1f, 1f - Remaining / Duration)
            : 1f;

        /// <summary>Create a cooldown with the given duration (starts ready by default).</summary>
        public Cooldown(float duration, bool startReady = true)
        {
            Duration = duration;
            Remaining = startReady ? 0f : duration;
        }

        /// <summary>Tick the timer by <paramref name="dt"/> seconds. Returns true if it just became ready.</summary>
        public bool Update(float dt)
        {
            if (Remaining <= 0f) return false;
            Remaining -= dt;
            if (Remaining <= 0f)
            {
                Remaining = 0f;
                return true; // just became ready
            }
            return false;
        }

        /// <summary>Reset the cooldown to full duration.</summary>
        public void Reset()
        {
            Remaining = Duration;
        }

        /// <summary>Reset with a new duration.</summary>
        public void Reset(float newDuration)
        {
            Duration = newDuration;
            Remaining = newDuration;
        }

        /// <summary>Force the cooldown to ready immediately.</summary>
        public void ForceReady()
        {
            Remaining = 0f;
        }
    }

    /// <summary>
    /// A repeating interval timer. Fires periodically every <see cref="Interval"/> seconds.
    /// Replaces <c>Scene.OnInterval()</c> patterns where frame-rate-independent timing is needed.
    /// </summary>
    public struct IntervalTimer
    {
        public float Interval { get; set; }
        private float _elapsed;

        public IntervalTimer(float interval)
        {
            Interval = interval;
            _elapsed = 0f;
        }

        /// <summary>Tick the timer. Returns true each time an interval elapses. Handles multiple ticks per frame.</summary>
        public bool Update(float dt)
        {
            _elapsed += dt;
            if (_elapsed >= Interval)
            {
                _elapsed -= Interval;
                return true;
            }
            return false;
        }

        /// <summary>Reset the elapsed time to zero.</summary>
        public void Reset()
        {
            _elapsed = 0f;
        }
    }
}
