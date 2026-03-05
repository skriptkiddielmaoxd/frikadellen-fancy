/// Anti-detection and humanization utilities
///
/// # Overview
///
/// This module implements all mechanisms that make bot behaviour statistically
/// indistinguishable from a human player, reducing the risk of Hypixel Watchdog
/// bans.  Every strategy is calibrated to balance **speed** (AH hot-path must
/// remain competitive) against **safety** (movement, signing, and session
/// timing must look organic).
///
/// # Design Principles
///
/// * **Gaussian jitter** – timing randomness approximated via Box-Muller
///   transform so the distribution of observed delays matches human reaction
///   time (bell-curve, not uniform).
/// * **Reactive over polling** – clicking triggered by packet arrival, not
///   fixed interval sleeps, so latency matches the natural server round-trip
///   variance rather than a suspiciously constant cadence.
/// * **Layered randomness** – movement, session length, dummy activities, and
///   human pauses each have independent random seeds logged for reproducibility.
/// * **Safety-first fallback** – every async task catches its own panics/errors
///   and falls back to baseline delays; the bot never runs with *zero* delay.
use rand::Rng;
use std::time::Duration;
use tokio::time::sleep;
use tracing::info;

// ─── Jitter profiles ─────────────────────────────────────────────────────────

/// Identifies which timing profile to use for [`jittered_delay`].
///
/// Each variant maps to a different jitter percentage, trading latency for
/// safety.  Profiles are listed in ascending order of safety (descending order
/// of speed).
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum JitterProfile {
    /// AH hot-path for high-value flips (≥ profit threshold).
    ///
    /// **Priority: speed.**  Client-side jitter only (±5 ms max regardless of
    /// base delay) so the purchase packet reaches the server as fast as
    /// possible.
    AhHighValue,

    /// AH hot-path for normal-value flips.
    ///
    /// **Priority: speed with plausibility.** ±10–20 ms so consecutive
    /// clicks never arrive in identical cadence.
    AhNormal,

    /// GUI navigation and window interaction delays (opening menus, slot
    /// polling, confirm waits).
    ///
    /// **Priority: balanced.** ±10–30 % of the base delay.
    GuiNavigation,

    /// Bazaar order flow and idle background activities.
    ///
    /// **Priority: safety.** ±30 % of the base delay so periodic checks
    /// are never suspiciously punctual.
    BazaarAndIdle,
}

/// Apply a Gaussian-distributed jitter to `base_ms` and sleep for the result.
///
/// The distribution is approximated via the Box-Muller transform using two
/// uniform random samples.  The result is clamped so it is always ≥ 1 ms and
/// never exceeds `base_ms * 2` (safety guard).
///
/// A seed value derived from the current random state is logged at `info` level
/// so operators can correlate the observed delay with the RNG state.
///
/// # Safety fallback
///
/// If arithmetic produces a zero or overflow, the function falls back to
/// `base_ms` with no jitter and logs a warning.  The bot never sleeps for
/// zero milliseconds.
pub async fn jittered_delay(base_ms: u64, profile: JitterProfile) {
    // Compute the actual delay synchronously so no non-Send type is held
    // across the `.await` point (ThreadRng is !Send).
    let actual_ms = compute_jittered_ms(base_ms, profile);
    info!(
        "[anti_detection] jittered_delay profile={:?} base={}ms actual={}ms",
        profile, base_ms, actual_ms
    );
    sleep(Duration::from_millis(actual_ms)).await;
}

/// Pure computation helper: returns the jittered duration in milliseconds.
///
/// Separated from the `async fn` so it can be tested synchronously and wrapped
/// in `catch_unwind`.
pub fn compute_jittered_ms(base_ms: u64, profile: JitterProfile) -> u64 {
    let mut rng = rand::thread_rng();

    // Box-Muller: two uniform samples → standard normal z
    // Lower bound of 1e-10 (not f64::EPSILON ≈ 2.2e-16) for numerical stability
    // in the ln() call — very small values produce large-magnitude outliers.
    let u1: f64 = rng.gen_range(1e-10_f64..1.0);
    let u2: f64 = rng.gen_range(0.0..1.0);
    let z = (-2.0_f64 * u1.ln()).sqrt() * (2.0 * std::f64::consts::PI * u2).cos();

    // Profile-specific sigma as a fraction of base_ms
    let sigma_frac: f64 = match profile {
        JitterProfile::AhHighValue => {
            // ±5 ms absolute cap regardless of base_ms
            let cap_ms = 5.0_f64;
            let jitter_ms = (z * cap_ms).clamp(-cap_ms, cap_ms);
            let result = (base_ms as f64 + jitter_ms).round().max(1.0) as u64;
            return result.min(base_ms.saturating_mul(2));
        }
        JitterProfile::AhNormal => 0.15, // ±15 % ≈ ±10–20 ms at 150 ms base
        JitterProfile::GuiNavigation => 0.20, // ±20 % (within ±10–30 % band)
        JitterProfile::BazaarAndIdle => 0.30, // ±30 %
    };

    let jitter_ms = z * (base_ms as f64) * sigma_frac;
    let result_f = base_ms as f64 + jitter_ms;
    (result_f.round().max(1.0) as u64).min(base_ms.saturating_mul(2))
}

// ─── Confirm-retry escalating delays ─────────────────────────────────────────

/// 3-step escalating confirm-retry delays (ms).
///
/// Replaces a static constant retry interval with a human-like ramp:
/// 70 ms → 180 ms → 320 ms.  Each step gets its own Gaussian jitter so no two
/// retries arrive with a suspiciously identical inter-packet gap.
pub const CONFIRM_RETRY_STEPS_MS: [u64; 3] = [70, 180, 320];

/// Sleep for the nth confirm-retry step (0-indexed, clamped to last entry).
///
/// Uses [`JitterProfile::GuiNavigation`] jitter to keep retries organic.
pub async fn confirm_retry_delay(step: usize) {
    let base = CONFIRM_RETRY_STEPS_MS[step.min(CONFIRM_RETRY_STEPS_MS.len() - 1)];
    jittered_delay(base, JitterProfile::GuiNavigation).await;
}

// ─── Sign typing delay ────────────────────────────────────────────────────────

/// Sleep for a human-like typing delay before sending `ServerboundSignUpdate`.
///
/// A real player reads the prompt, thinks, then types.  Delays below 300 ms are
/// machine-fast; delays above 800 ms start to feel too slow.  The function
/// chooses uniformly within the range and then applies a small Gaussian
/// perturbation.
pub async fn sign_typing_delay(min_ms: u64, max_ms: u64) {
    // ThreadRng dropped before .await.
    let actual_ms = {
        let base_ms: u64 = rand::thread_rng().gen_range(min_ms..=max_ms);
        compute_jittered_ms(base_ms, JitterProfile::AhNormal)
    };
    info!("[anti_detection] sign_typing_delay actual={}ms", actual_ms);
    sleep(Duration::from_millis(actual_ms)).await;
}

// ─── Human pause after successful flip ───────────────────────────────────────

/// Sleep for a human-like pause after completing a flip.
///
/// Humans naturally pause between actions.  Most pauses are 1–5 s, but
/// occasionally (controlled by `long_pause_probability`) a longer 10–40 s
/// pause occurs — emulating the player glancing at chat, checking inventory,
/// or reading price trends.
///
/// # Arguments
///
/// * `min_ms` – minimum short pause (default 1 000 ms)
/// * `max_ms` – maximum short pause (default 5 000 ms)
/// * `long_pause_probability` – probability [0.0, 1.0] of a 10–40 s pause
pub async fn human_pause_after_flip(min_ms: u64, max_ms: u64, long_pause_probability: f64) {
    // All RNG calls are scoped so ThreadRng is dropped before the .await.
    let (actual_ms, label) = {
        let mut rng = rand::thread_rng();
        let (base_ms, lbl) = if rng.gen::<f64>() < long_pause_probability {
            let long_ms: u64 = rng.gen_range(10_000..=40_000);
            (long_ms, "long")
        } else {
            let short_ms: u64 = rng.gen_range(min_ms..=max_ms);
            (short_ms, "short")
        };
        (
            compute_jittered_ms(base_ms, JitterProfile::BazaarAndIdle),
            lbl,
        )
    };
    info!(
        "[anti_detection] human_pause_after_flip type={} actual={}ms",
        label, actual_ms
    );
    sleep(Duration::from_millis(actual_ms)).await;
}

// ─── Bazaar periodic check interval ──────────────────────────────────────────

/// Return a random interval (in seconds) for the next bazaar order check.
///
/// Range: 25–45 s.  Occasionally returns a longer 60–120 s gap to simulate
/// the player being briefly distracted.
///
/// # Returns
///
/// Duration to sleep before the next check.
pub fn bazaar_check_interval() -> Duration {
    let secs: u64 = if rand::thread_rng().gen_bool(0.15) {
        // ~15 % chance of a longer gap
        rand::thread_rng().gen_range(60..=120)
    } else {
        rand::thread_rng().gen_range(25..=45)
    };
    info!("[anti_detection] bazaar_check_interval next={}s", secs);
    Duration::from_secs(secs)
}

// ─── Movement simulation ──────────────────────────────────────────────────────

/// Configuration for the movement simulation background task.
///
/// All intervals are in seconds; all angles are in degrees.  Defaults match
/// the behaviour of a player idly browsing the auction house or bazaar.
#[derive(Debug, Clone)]
pub struct MovementSimConfig {
    /// Minimum seconds between yaw/pitch rotations (default 5).
    pub rotation_interval_min_secs: u64,
    /// Maximum seconds between yaw/pitch rotations (default 40).
    pub rotation_interval_max_secs: u64,
    /// Maximum yaw delta per rotation event (degrees, default 15).
    pub max_yaw_delta_deg: f32,
    /// Maximum pitch delta per rotation event (degrees, default 8).
    pub max_pitch_delta_deg: f32,
    /// Minimum seconds between jump events (default 15).
    pub jump_interval_min_secs: u64,
    /// Maximum seconds between jump events (default 45).
    pub jump_interval_max_secs: u64,
    /// Minimum seconds between short walk/sprint events (default 20).
    pub walk_interval_min_secs: u64,
    /// Maximum seconds between short walk/sprint events (default 60).
    pub walk_interval_max_secs: u64,
    /// Maximum blocks to walk per event (default 3).
    pub max_walk_blocks: u8,
    /// Probability of a sneak toggle on each walk event (default 0.25).
    pub sneak_probability: f64,
    /// Minimum seconds between passive island hops (default 60).
    pub island_hop_interval_min_secs: u64,
    /// Maximum seconds between passive island hops (default 300).
    pub island_hop_interval_max_secs: u64,
}

impl Default for MovementSimConfig {
    fn default() -> Self {
        Self {
            rotation_interval_min_secs: 5,
            rotation_interval_max_secs: 40,
            max_yaw_delta_deg: 15.0,
            max_pitch_delta_deg: 8.0,
            jump_interval_min_secs: 15,
            jump_interval_max_secs: 45,
            walk_interval_min_secs: 20,
            walk_interval_max_secs: 60,
            max_walk_blocks: 3,
            sneak_probability: 0.25,
            island_hop_interval_min_secs: 60,
            island_hop_interval_max_secs: 300,
        }
    }
}

/// Token used to signal the movement simulation task to stop.
pub type MovementStopSignal = tokio::sync::watch::Sender<bool>;
/// Receiver end of the movement stop signal.
pub type MovementStopReceiver = tokio::sync::watch::Receiver<bool>;

/// Create a linked stop-signal pair.
pub fn movement_stop_channel() -> (MovementStopSignal, MovementStopReceiver) {
    tokio::sync::watch::channel(false)
}

/// Callback type for sending movement/interaction packets.
///
/// The bot implementation provides a concrete closure; we use a trait object so
/// the simulation task does not need to know about Azalea internals.
pub type SendPacketFn = Box<dyn Fn(MovementPacket) + Send + Sync + 'static>;

/// Simplified representation of the movement packets the simulation emits.
///
/// The bot layer translates these into the corresponding Azalea packet calls.
#[derive(Debug, Clone)]
pub enum MovementPacket {
    /// Rotate the player to the given absolute yaw/pitch (degrees).
    Rotate { yaw: f32, pitch: f32 },
    /// Send a jump input (ground → air transition).
    Jump,
    /// Walk in direction `yaw_deg` for `blocks` blocks.
    Walk { yaw_deg: f32, blocks: u8 },
    /// Toggle sneak state.
    ToggleSneak { sneaking: bool },
}

/// Spawn the movement simulation background task.
///
/// The task runs independently until `stop_rx` receives `true`.  On each wakeup
/// it decides which movement action to perform next, waits a randomised
/// interval, then emits the corresponding [`MovementPacket`] via `send_fn`.
///
/// All errors are caught internally; the task logs a warning and continues
/// rather than propagating panics to the main runtime.
pub fn spawn_movement_simulation(
    config: MovementSimConfig,
    stop_rx: MovementStopReceiver,
    send_fn: SendPacketFn,
) -> tokio::task::JoinHandle<()> {
    tokio::spawn(async move {
        let stop_rx = stop_rx;
        let mut current_yaw: f32 = 0.0;
        let mut current_pitch: f32 = 0.0;
        let mut sneaking = false;

        // Track when each sub-event is next due so they interleave naturally.
        // All RNG calls are scoped to temporary blocks so `ThreadRng` is never
        // held across an `.await` point (ThreadRng is not Send).
        let now = tokio::time::Instant::now();
        let mut next_rotation = now
            + Duration::from_secs({
                let s: u64 = rand::thread_rng().gen_range(1..=config.rotation_interval_min_secs);
                s
            });
        let mut next_jump = now
            + Duration::from_secs({
                let s: u64 = rand::thread_rng().gen_range(5..=config.jump_interval_min_secs);
                s
            });
        let mut next_walk = now
            + Duration::from_secs({
                let s: u64 = rand::thread_rng().gen_range(10..=config.walk_interval_min_secs);
                s
            });
        let mut next_island_hop = now
            + Duration::from_secs({
                let s: u64 = rand::thread_rng().gen_range(
                    config.island_hop_interval_min_secs..=config.island_hop_interval_max_secs,
                );
                s
            });
        // Track previously used walk directions to avoid identical repeats.
        let mut last_walk_yaw: Option<f32> = None;

        loop {
            // Poll the stop signal every tick.
            if *stop_rx.borrow() {
                info!("[movement_sim] Stop signal received — shutting down");
                break;
            }

            let now = tokio::time::Instant::now();

            // ── Rotation ───────────────────────────────────────────────────
            if now >= next_rotation {
                let (delta_yaw, delta_pitch) = {
                    let mut rng = rand::thread_rng();
                    (
                        rng.gen_range(-config.max_yaw_delta_deg..=config.max_yaw_delta_deg),
                        rng.gen_range(-config.max_pitch_delta_deg..=config.max_pitch_delta_deg),
                    )
                };
                current_yaw = (current_yaw + delta_yaw).rem_euclid(360.0);
                current_pitch = (current_pitch + delta_pitch).clamp(-90.0, 90.0);
                info!(
                    "[movement_sim] Rotate yaw={:.1}° pitch={:.1}°",
                    current_yaw, current_pitch
                );
                send_fn(MovementPacket::Rotate {
                    yaw: current_yaw,
                    pitch: current_pitch,
                });
                next_rotation = now
                    + Duration::from_secs({
                        let s: u64 = rand::thread_rng().gen_range(
                            config.rotation_interval_min_secs..=config.rotation_interval_max_secs,
                        );
                        s
                    });
            }

            // ── Jump ──────────────────────────────────────────────────────
            if now >= next_jump {
                info!("[movement_sim] Jump");
                send_fn(MovementPacket::Jump);
                next_jump = now
                    + Duration::from_secs({
                        let s: u64 = rand::thread_rng().gen_range(
                            config.jump_interval_min_secs..=config.jump_interval_max_secs,
                        );
                        s
                    });
            }

            // ── Short walk ───────────────────────────────────────────────
            if now >= next_walk {
                // Choose a direction different from the last walk to avoid
                // pacing back and forth on the same line.
                let walk_yaw = {
                    let mut rng = rand::thread_rng();
                    loop {
                        let candidate: f32 = rng.gen_range(0.0..360.0);
                        if let Some(last) = last_walk_yaw {
                            let diff = (candidate - last)
                                .abs()
                                .min(360.0 - (candidate - last).abs());
                            if diff > 30.0 {
                                break candidate;
                            }
                            // Too similar — try again
                        } else {
                            break candidate;
                        }
                    }
                };
                last_walk_yaw = Some(walk_yaw);
                let blocks: u8 = rand::thread_rng().gen_range(1..=config.max_walk_blocks);
                info!("[movement_sim] Walk yaw={:.1}° blocks={}", walk_yaw, blocks);
                send_fn(MovementPacket::Walk {
                    yaw_deg: walk_yaw,
                    blocks,
                });

                // Occasionally sneak during or after walk
                let do_sneak: bool = rand::thread_rng().gen_bool(config.sneak_probability);
                if do_sneak {
                    sneaking = !sneaking;
                    info!("[movement_sim] ToggleSneak sneaking={}", sneaking);
                    send_fn(MovementPacket::ToggleSneak { sneaking });
                }

                next_walk = now
                    + Duration::from_secs({
                        let s: u64 = rand::thread_rng().gen_range(
                            config.walk_interval_min_secs..=config.walk_interval_max_secs,
                        );
                        s
                    });
            }

            // ── Island hop ───────────────────────────────────────────────
            if now >= next_island_hop {
                let hop_yaw: f32 = rand::thread_rng().gen_range(0.0..360.0);
                let hop_blocks: u8 = rand::thread_rng().gen_range(3..=8);
                info!(
                    "[movement_sim] Island hop yaw={:.1}° blocks={}",
                    hop_yaw, hop_blocks
                );
                // Island hop = jump + walk
                send_fn(MovementPacket::Jump);
                send_fn(MovementPacket::Walk {
                    yaw_deg: hop_yaw,
                    blocks: hop_blocks,
                });
                next_island_hop = now
                    + Duration::from_secs({
                        let s: u64 = rand::thread_rng().gen_range(
                            config.island_hop_interval_min_secs
                                ..=config.island_hop_interval_max_secs,
                        );
                        s
                    });
            }

            // Sleep briefly before next tick to avoid busy-loop.
            tokio::time::sleep(Duration::from_millis(500)).await;
        }
    })
}

// ─── Session management ───────────────────────────────────────────────────────

/// Configuration for automatic session cycling.
///
/// After a random play session, the bot disconnects, waits an idle gap, then
/// reconnects.  This emulates a real player taking breaks.
#[derive(Debug, Clone)]
pub struct SessionConfig {
    /// Enable automatic session cycling.
    pub enabled: bool,
    /// Minimum session length in seconds (default 2 h).
    pub session_min_secs: u64,
    /// Maximum session length in seconds (default 6 h).
    pub session_max_secs: u64,
    /// Minimum idle gap between sessions in seconds (default 5 min).
    pub idle_gap_min_secs: u64,
    /// Maximum idle gap between sessions in seconds (default 30 min).
    pub idle_gap_max_secs: u64,
}

impl Default for SessionConfig {
    fn default() -> Self {
        Self {
            enabled: false, // opt-in; user must explicitly enable
            session_min_secs: 2 * 3600,
            session_max_secs: 6 * 3600,
            idle_gap_min_secs: 5 * 60,
            idle_gap_max_secs: 30 * 60,
        }
    }
}

/// Draw a random session length from the configured range.
pub fn random_session_duration(config: &SessionConfig) -> Duration {
    let secs = rand::thread_rng().gen_range(config.session_min_secs..=config.session_max_secs);
    info!("[anti_detection] session length drawn: {}s", secs);
    Duration::from_secs(secs)
}

/// Draw a random idle gap duration from the configured range.
pub fn random_idle_gap(config: &SessionConfig) -> Duration {
    let secs = rand::thread_rng().gen_range(config.idle_gap_min_secs..=config.idle_gap_max_secs);
    info!("[anti_detection] idle gap drawn: {}s", secs);
    Duration::from_secs(secs)
}

// ─── Benchmarking / statistics ────────────────────────────────────────────────

/// Rolling standard-deviation accumulator for click/movement interval logging.
///
/// After every [`push`] the running mean and variance are updated using
/// Welford's online algorithm.  Call [`log_stats`] periodically to emit an
/// `info` log that operators can compare against documented ban thresholds.
#[derive(Debug, Default)]
pub struct IntervalStats {
    count: u64,
    mean: f64,
    m2: f64,
}

impl IntervalStats {
    /// Create an empty stats accumulator.
    pub fn new() -> Self {
        Self::default()
    }

    /// Record a new observed interval (milliseconds).
    pub fn push(&mut self, value_ms: u64) {
        self.count += 1;
        let x = value_ms as f64;
        let delta = x - self.mean;
        self.mean += delta / self.count as f64;
        let delta2 = x - self.mean;
        self.m2 += delta * delta2;
    }

    /// Population standard deviation (returns 0 if < 2 samples).
    pub fn std_dev(&self) -> f64 {
        if self.count < 2 {
            return 0.0;
        }
        (self.m2 / self.count as f64).sqrt()
    }

    /// Log current statistics at `info` level.
    ///
    /// # Arguments
    ///
    /// * `label` – human-readable label, e.g. `"click_interval_ms"`
    pub fn log_stats(&self, label: &str) {
        info!(
            "[anti_detection] {} n={} mean={:.1}ms std_dev={:.1}ms",
            label,
            self.count,
            self.mean,
            self.std_dev()
        );
    }
}

// ─── Tests ────────────────────────────────────────────────────────────────────

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn jitter_ah_high_value_stays_within_cap() {
        // ±5 ms cap must be respected regardless of base
        for base in [50u64, 150, 500, 2000] {
            for _ in 0..500 {
                let result = compute_jittered_ms(base, JitterProfile::AhHighValue);
                let delta = result as i64 - base as i64;
                assert!(
                    delta.abs() <= 5,
                    "base={base} result={result} delta={delta} exceeds ±5ms cap"
                );
            }
        }
    }

    #[test]
    fn jitter_result_always_at_least_one_ms() {
        for profile in [
            JitterProfile::AhHighValue,
            JitterProfile::AhNormal,
            JitterProfile::GuiNavigation,
            JitterProfile::BazaarAndIdle,
        ] {
            for _ in 0..200 {
                let result = compute_jittered_ms(1, profile);
                assert!(result >= 1, "profile={profile:?} got zero delay");
            }
        }
    }

    #[test]
    fn jitter_result_never_exceeds_2x_base() {
        for base in [10u64, 100, 500, 5000] {
            for profile in [
                JitterProfile::AhHighValue,
                JitterProfile::AhNormal,
                JitterProfile::GuiNavigation,
                JitterProfile::BazaarAndIdle,
            ] {
                for _ in 0..200 {
                    let result = compute_jittered_ms(base, profile);
                    assert!(
                        result <= base * 2,
                        "base={base} result={result} exceeds 2x cap"
                    );
                }
            }
        }
    }

    #[test]
    fn interval_stats_std_dev() {
        let mut stats = IntervalStats::new();
        // Push a constant series — std-dev should be near 0
        for _ in 0..100 {
            stats.push(150);
        }
        assert!(
            stats.std_dev() < 1.0,
            "constant series should have ~0 std-dev"
        );

        // Push a high-variance series
        let mut stats2 = IntervalStats::new();
        for i in 0..100u64 {
            stats2.push(i * 10);
        }
        assert!(
            stats2.std_dev() > 100.0,
            "high-variance series should have large std-dev"
        );
    }

    #[test]
    fn confirm_retry_steps_ascending() {
        for i in 1..CONFIRM_RETRY_STEPS_MS.len() {
            assert!(
                CONFIRM_RETRY_STEPS_MS[i] > CONFIRM_RETRY_STEPS_MS[i - 1],
                "retry steps must be strictly ascending"
            );
        }
    }

    #[test]
    fn bazaar_check_interval_within_bounds() {
        for _ in 0..200 {
            let d = bazaar_check_interval();
            let secs = d.as_secs();
            assert!(
                (25..=120).contains(&secs),
                "interval {}s out of expected 25–120s range",
                secs
            );
        }
    }

    #[test]
    fn random_session_duration_within_bounds() {
        let config = SessionConfig::default();
        for _ in 0..50 {
            let d = random_session_duration(&config);
            let secs = d.as_secs();
            assert!(
                secs >= config.session_min_secs && secs <= config.session_max_secs,
                "session {}s out of [{}, {}]",
                secs,
                config.session_min_secs,
                config.session_max_secs
            );
        }
    }

    #[test]
    fn random_idle_gap_within_bounds() {
        let config = SessionConfig::default();
        for _ in 0..50 {
            let d = random_idle_gap(&config);
            let secs = d.as_secs();
            assert!(
                secs >= config.idle_gap_min_secs && secs <= config.idle_gap_max_secs,
                "idle gap {}s out of [{}, {}]",
                secs,
                config.idle_gap_min_secs,
                config.idle_gap_max_secs
            );
        }
    }
}
