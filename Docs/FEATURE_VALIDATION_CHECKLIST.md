# MaggyHelper Feature Validation Checklist

This checklist tracks runtime validation for the 7 advanced systems reviewed from community patterns.

## Quick Command Panel

Run these in the Celeste debug console:

- `maggy_validate`
- `maggy_validate full`
- `maggy_validate_state`
- `maggy_validate_room`

The command output is a compact PASS/FAIL panel for:

1. AreaMapDataExt
2. HeartGem custom sprite/SFX
3. Credits after AreaComplete
4. Integrated boss fight flow
5. Player inventory extension
6. Intro remix vignette
7. Postcard extension

## Manual Regression Pass

Use this after major updates, side unlock changes, or save migration changes.

1. AreaMapDataExt
- Verify all expected chapters appear in chapter select.
- Enter at least one map from each supported side chain (A/B/C/D/DX).
- Confirm chapter icon, mountain camera, and music are correct.

2. HeartGem custom sprite/SFX
- Collect one heart on a vanilla side and one on an extended side.
- Confirm heart visual tint/sprite behavior is side-correct.
- Re-enter room to verify collected heart does not reappear incorrectly.

3. Credits after AreaComplete
- Complete the target chapter/mode that should redirect to credits.
- Confirm non-target chapters still follow default completion flow.
- Check confirm input does not trigger duplicate transitions.

4. Integrated boss fight
- Start a boss fight, die, retry, and complete.
- Confirm runtime boss state resets correctly after transitions.
- Confirm save data increments boss progression once per clear.

5. Player inventory extension
- Start from fresh room and checkpoint respawn to verify ability/inventory consistency.
- Confirm persistent unlocks remain after returning to menu.
- Confirm session-only state is reset when expected.

6. Intro remix vignette
- Trigger B-side and C-side intro vignette sequences.
- Test skip input during intro, title hold, and transition-out.
- Validate fallback behavior when optional textures/audio are missing.

7. Postcard extension
- Trigger unlock postcards for side progression.
- Verify postcard SFX for named IDs, numeric IDs, and direct FMOD event IDs.
- Confirm postcard closes cleanly and returns to expected flow.

## Typical Failure Signals

- Missing chapter entries or wrong side selection: map metadata registration drift.
- Heart visuals not changing on extended sides: HeartGem hook regression.
- Stuck on completion screen: AreaComplete transition path conflict.
- Boss remains active outside encounter: session state not reset.
- Ability or inventory mismatch after respawn: save/session split regression.
- Vignette softlock after skip: coroutine exit path not completing.
- Postcard audio wrong or silent: sound ID parsing or event path issue.

## Recommended Routine

1. Run `maggy_validate full` before manual playtest.
2. Run the manual 7-point pass after large feature merges.
3. Re-run `maggy_validate_state` when diagnosing room-specific issues.
4. Run `maggy_validate_room` while standing in a target room to verify side/postcard/heart readiness.