use chrono::{DateTime, Utc};
use serde::{Deserialize, Serialize};
use std::collections::HashMap;

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct Config {
    #[serde(default)]
    pub ingame_name: Option<String>,

    #[serde(default = "default_websocket_url")]
    pub websocket_url: String,

    #[serde(default = "default_web_gui_port")]
    pub web_gui_port: u16,

    #[serde(default = "default_flip_action_delay")]
    pub flip_action_delay: u64,

    /// Minimum delay between consecutive queued commands in milliseconds.
    /// Prevents back-to-back Hypixel interactions from overlapping.
    /// Default: 500ms.
    #[serde(default = "default_command_delay_ms")]
    pub command_delay_ms: u64,

    #[serde(default = "default_bed_spam_click_delay")]
    pub bed_spam_click_delay: u64,

    #[serde(default)]
    pub bed_multiple_clicks_delay: u64,

    #[serde(default = "default_bazaar_order_check_interval_seconds")]
    pub bazaar_order_check_interval_seconds: u64,

    #[serde(default = "default_bazaar_order_cancel_minutes")]
    pub bazaar_order_cancel_minutes: u64,

    #[serde(default)]
    pub enable_bazaar_flips: bool,

    #[serde(default = "default_true")]
    pub enable_ah_flips: bool,

    #[serde(default)]
    pub bed_spam: bool,

    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub freemoney: Option<bool>,

    #[serde(default = "default_true")]
    pub use_cofl_chat: bool,

    #[serde(default)]
    pub auto_cookie: u64,

    /// Enable fastbuy (window-skip): click BIN buy (slot 31) and pre-click confirm (slot 11).
    /// Disabled by default and omitted from generated config unless manually added.
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub fastbuy: Option<bool>,

    #[serde(default = "default_true")]
    pub enable_console_input: bool,

    #[serde(default = "default_auction_duration_hours")]
    pub auction_duration_hours: u64,

    #[serde(default)]
    pub skip: SkipConfig,

    #[serde(default)]
    pub proxy_enabled: bool,

    #[serde(default)]
    pub proxy: Option<String>,

    #[serde(default)]
    pub proxy_username: Option<String>,

    #[serde(default)]
    pub proxy_password: Option<String>,

    #[serde(default)]
    /// Discord webhook URL for notifications.
    /// `None` = not yet configured (prompts on next startup).
    /// `Some("")` = explicitly disabled (no further prompts).
    /// `Some(url)` = active webhook.
    pub webhook_url: Option<String>,

    #[serde(default)]
    pub web_gui_password: Option<String>,

    #[serde(default)]
    pub accounts: Option<String>,

    #[serde(default)]
    pub auto_switching: Option<String>,

    #[serde(default)]
    pub sessions: HashMap<String, CoflSession>,

    /// Discord bot token for start/stop commands.
    /// `None` = Discord bot disabled.
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub discord_bot_token: Option<String>,

    /// Discord channel ID to restrict bot commands to (optional).
    /// If not set, the bot responds in any channel it can see.
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub discord_channel_id: Option<u64>,

    // ── Anti-detection / humanization ────────────────────────────────────────

    /// Anti-detection configuration block.
    #[serde(default)]
    pub anti_detection: AntiDetectionConfig,
}

/// Anti-detection and humanization settings.
///
/// All parameters have conservative, human-like defaults.  They can be tuned
/// in `config.toml` under the `[anti_detection]` table.  Setting `enabled =
/// false` disables all jitter and simulation globally (useful for benchmarking
/// or diagnostics).
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct AntiDetectionConfig {
    /// Master switch — disabling this makes jitter helpers return base delays
    /// unchanged and skips movement/session tasks.  Default: `true`.
    #[serde(default = "default_true")]
    pub enabled: bool,

    // ── AH jitter ────────────────────────────────────────────────────────────

    /// Profit threshold (coins) above which a flip is treated as "high-value"
    /// and the minimum jitter profile is used.  Default: 5 000 000.
    #[serde(default = "default_high_value_threshold")]
    pub high_value_flip_threshold: u64,

    // ── GUI navigation jitter ─────────────────────────────────────────────────
    // GUI navigation delays use ±20% Gaussian jitter by default (GuiNavigation
    // profile).  The base delay comes from flip_action_delay / command_delay_ms
    // in the root config.

    // ── Bazaar / sign timing ──────────────────────────────────────────────────

    /// Minimum human-like typing delay before sending ServerboundSignUpdate
    /// (ms).  Default: 300.
    #[serde(default = "default_sign_typing_min_ms")]
    pub sign_typing_min_ms: u64,

    /// Maximum human-like typing delay before sending ServerboundSignUpdate
    /// (ms).  Default: 800.
    #[serde(default = "default_sign_typing_max_ms")]
    pub sign_typing_max_ms: u64,

    // ── Human pause after flip ────────────────────────────────────────────────

    /// Minimum short pause after a successful flip (ms).  Default: 1 000.
    #[serde(default = "default_human_pause_min_ms")]
    pub human_pause_min_ms: u64,

    /// Maximum short pause after a successful flip (ms).  Default: 5 000.
    #[serde(default = "default_human_pause_max_ms")]
    pub human_pause_max_ms: u64,

    /// Probability [0.0, 1.0] that the next pause is a long one (10–40 s).
    /// Default: 0.05 (5 %).
    #[serde(default = "default_long_pause_probability")]
    pub long_pause_probability: f64,

    // ── Movement simulation ───────────────────────────────────────────────────

    /// Enable the background movement simulation task.  Default: `true`.
    #[serde(default = "default_true")]
    pub movement_simulation_enabled: bool,

    /// Minimum seconds between yaw/pitch rotation events.  Default: 5.
    #[serde(default = "default_rotation_interval_min")]
    pub rotation_interval_min_secs: u64,

    /// Maximum seconds between yaw/pitch rotation events.  Default: 40.
    #[serde(default = "default_rotation_interval_max")]
    pub rotation_interval_max_secs: u64,

    /// Maximum absolute yaw change per rotation event (degrees).  Default: 15.
    #[serde(default = "default_max_yaw_delta")]
    pub max_yaw_delta_deg: f32,

    /// Maximum absolute pitch change per rotation event (degrees).  Default: 8.
    #[serde(default = "default_max_pitch_delta")]
    pub max_pitch_delta_deg: f32,

    /// Minimum seconds between jump events.  Default: 15.
    #[serde(default = "default_jump_interval_min")]
    pub jump_interval_min_secs: u64,

    /// Maximum seconds between jump events.  Default: 45.
    #[serde(default = "default_jump_interval_max")]
    pub jump_interval_max_secs: u64,

    /// Minimum seconds between short walk events.  Default: 20.
    #[serde(default = "default_walk_interval_min")]
    pub walk_interval_min_secs: u64,

    /// Maximum seconds between short walk events.  Default: 60.
    #[serde(default = "default_walk_interval_max")]
    pub walk_interval_max_secs: u64,

    /// Maximum blocks to walk per event.  Default: 3.
    #[serde(default = "default_max_walk_blocks")]
    pub max_walk_blocks: u8,

    /// Probability of a sneak toggle on each walk event [0.0, 1.0].  Default: 0.25.
    #[serde(default = "default_sneak_probability")]
    pub sneak_probability: f64,

    /// Minimum seconds between passive island hop events.  Default: 60.
    #[serde(default = "default_island_hop_min")]
    pub island_hop_interval_min_secs: u64,

    /// Maximum seconds between passive island hop events.  Default: 300.
    #[serde(default = "default_island_hop_max")]
    pub island_hop_interval_max_secs: u64,

    // ── Session management ────────────────────────────────────────────────────

    /// Enable automatic session cycling (disconnect → idle → reconnect).
    /// Default: `false` (opt-in).
    #[serde(default)]
    pub session_cycling_enabled: bool,

    /// Minimum play session length (seconds).  Default: 7 200 (2 h).
    #[serde(default = "default_session_min_secs")]
    pub session_min_secs: u64,

    /// Maximum play session length (seconds).  Default: 21 600 (6 h).
    #[serde(default = "default_session_max_secs")]
    pub session_max_secs: u64,

    /// Minimum idle gap between sessions (seconds).  Default: 300 (5 min).
    #[serde(default = "default_idle_gap_min_secs")]
    pub idle_gap_min_secs: u64,

    /// Maximum idle gap between sessions (seconds).  Default: 1 800 (30 min).
    #[serde(default = "default_idle_gap_max_secs")]
    pub idle_gap_max_secs: u64,

    // ── Dummy activity ────────────────────────────────────────────────────────

    /// Enable random dummy activities (harmless `/ah`, `/bz`, inventory
    /// open/close).  Default: `true`.
    #[serde(default = "default_true")]
    pub dummy_activity_enabled: bool,

    /// Minimum seconds between dummy activity events.  Default: 120.
    #[serde(default = "default_dummy_interval_min")]
    pub dummy_activity_interval_min_secs: u64,

    /// Maximum seconds between dummy activity events.  Default: 600.
    #[serde(default = "default_dummy_interval_max")]
    pub dummy_activity_interval_max_secs: u64,
}

impl Default for AntiDetectionConfig {
    fn default() -> Self {
        Self {
            enabled: true,
            high_value_flip_threshold: default_high_value_threshold(),
            sign_typing_min_ms: default_sign_typing_min_ms(),
            sign_typing_max_ms: default_sign_typing_max_ms(),
            human_pause_min_ms: default_human_pause_min_ms(),
            human_pause_max_ms: default_human_pause_max_ms(),
            long_pause_probability: default_long_pause_probability(),
            movement_simulation_enabled: true,
            rotation_interval_min_secs: default_rotation_interval_min(),
            rotation_interval_max_secs: default_rotation_interval_max(),
            max_yaw_delta_deg: default_max_yaw_delta(),
            max_pitch_delta_deg: default_max_pitch_delta(),
            jump_interval_min_secs: default_jump_interval_min(),
            jump_interval_max_secs: default_jump_interval_max(),
            walk_interval_min_secs: default_walk_interval_min(),
            walk_interval_max_secs: default_walk_interval_max(),
            max_walk_blocks: default_max_walk_blocks(),
            sneak_probability: default_sneak_probability(),
            island_hop_interval_min_secs: default_island_hop_min(),
            island_hop_interval_max_secs: default_island_hop_max(),
            session_cycling_enabled: false,
            session_min_secs: default_session_min_secs(),
            session_max_secs: default_session_max_secs(),
            idle_gap_min_secs: default_idle_gap_min_secs(),
            idle_gap_max_secs: default_idle_gap_max_secs(),
            dummy_activity_enabled: true,
            dummy_activity_interval_min_secs: default_dummy_interval_min(),
            dummy_activity_interval_max_secs: default_dummy_interval_max(),
        }
    }
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct SkipConfig {
    #[serde(default)]
    pub always: bool,

    #[serde(default = "default_min_profit")]
    pub min_profit: u64,

    #[serde(default)]
    pub user_finder: bool,

    #[serde(default)]
    pub skins: bool,

    #[serde(default = "default_profit_percentage")]
    pub profit_percentage: f64,

    #[serde(default = "default_min_price")]
    pub min_price: u64,
}

impl Default for SkipConfig {
    fn default() -> Self {
        Self {
            always: false,
            min_profit: default_min_profit(),
            user_finder: false,
            skins: false,
            profit_percentage: default_profit_percentage(),
            min_price: default_min_price(),
        }
    }
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct CoflSession {
    pub id: String,
    pub expires: DateTime<Utc>,
}

// Default values
fn default_websocket_url() -> String {
    "wss://sky.coflnet.com/modsocket".to_string()
}

fn default_web_gui_port() -> u16 {
    8080
}

fn default_flip_action_delay() -> u64 {
    150 // TypeScript FLIP_ACTION_DELAY
}

fn default_command_delay_ms() -> u64 {
    500
}

fn default_bed_spam_click_delay() -> u64 {
    100
}

fn default_bazaar_order_check_interval_seconds() -> u64 {
    30
}

fn default_bazaar_order_cancel_minutes() -> u64 {
    5
}

fn default_auction_duration_hours() -> u64 {
    24
}

fn default_true() -> bool {
    true
}

fn default_min_profit() -> u64 {
    1_000_000
}

fn default_profit_percentage() -> f64 {
    50.0
}

fn default_min_price() -> u64 {
    10_000_000
}

// Anti-detection defaults
fn default_high_value_threshold() -> u64 {
    5_000_000
}

fn default_sign_typing_min_ms() -> u64 {
    300
}

fn default_sign_typing_max_ms() -> u64 {
    800
}

fn default_human_pause_min_ms() -> u64 {
    1_000
}

fn default_human_pause_max_ms() -> u64 {
    5_000
}

fn default_long_pause_probability() -> f64 {
    0.05
}

fn default_rotation_interval_min() -> u64 {
    5
}

fn default_rotation_interval_max() -> u64 {
    40
}

fn default_max_yaw_delta() -> f32 {
    15.0
}

fn default_max_pitch_delta() -> f32 {
    8.0
}

fn default_jump_interval_min() -> u64 {
    15
}

fn default_jump_interval_max() -> u64 {
    45
}

fn default_walk_interval_min() -> u64 {
    20
}

fn default_walk_interval_max() -> u64 {
    60
}

fn default_max_walk_blocks() -> u8 {
    3
}

fn default_sneak_probability() -> f64 {
    0.25
}

fn default_island_hop_min() -> u64 {
    60
}

fn default_island_hop_max() -> u64 {
    300
}

fn default_session_min_secs() -> u64 {
    2 * 3600
}

fn default_session_max_secs() -> u64 {
    6 * 3600
}

fn default_idle_gap_min_secs() -> u64 {
    5 * 60
}

fn default_idle_gap_max_secs() -> u64 {
    30 * 60
}

fn default_dummy_interval_min() -> u64 {
    120
}

fn default_dummy_interval_max() -> u64 {
    600
}

impl Default for Config {
    fn default() -> Self {
        Self {
            ingame_name: None,
            websocket_url: default_websocket_url(),
            web_gui_port: default_web_gui_port(),
            flip_action_delay: default_flip_action_delay(),
            command_delay_ms: default_command_delay_ms(),
            bed_spam_click_delay: default_bed_spam_click_delay(),
            bed_multiple_clicks_delay: 0,
            bazaar_order_check_interval_seconds: default_bazaar_order_check_interval_seconds(),
            bazaar_order_cancel_minutes: default_bazaar_order_cancel_minutes(),
            enable_bazaar_flips: false,
            enable_ah_flips: true,
            bed_spam: false,
            freemoney: None,
            use_cofl_chat: true,
            auto_cookie: 0,
            fastbuy: None,
            enable_console_input: true,
            auction_duration_hours: default_auction_duration_hours(),
            skip: SkipConfig::default(),
            proxy_enabled: false,
            proxy: None,
            proxy_username: None,
            proxy_password: None,
            webhook_url: None,
            web_gui_password: None,
            accounts: None,
            auto_switching: None,
            sessions: HashMap::new(),
            discord_bot_token: None,
            discord_channel_id: None,
            anti_detection: AntiDetectionConfig::default(),
        }
    }
}

impl Config {
    pub fn freemoney_enabled(&self) -> bool {
        self.freemoney.unwrap_or(false)
    }

    pub fn fastbuy_enabled(&self) -> bool {
        self.fastbuy.unwrap_or(false)
    }

    /// Returns the webhook URL only if it is non-empty.
    pub fn active_webhook_url(&self) -> Option<&str> {
        self.webhook_url.as_deref().filter(|u| !u.is_empty())
    }

    /// Returns `true` when a flip with the given profit (coins) qualifies as
    /// "high value" and should use the minimum-jitter AH hot-path profile.
    pub fn is_high_value_flip(&self, profit_coins: u64) -> bool {
        self.anti_detection.enabled
            && profit_coins >= self.anti_detection.high_value_flip_threshold
    }
}

#[cfg(test)]
mod tests {
    use super::Config;

    #[test]
    fn default_config_omits_freemoney() {
        let toml =
            toml::to_string_pretty(&Config::default()).expect("default config should serialize");
        assert!(!toml.contains("freemoney"));
    }

    #[test]
    fn manual_freemoney_true_enables_flag() {
        let config: Config = toml::from_str("freemoney = true").expect("config should parse");
        assert!(config.freemoney_enabled());
    }

    #[test]
    fn fastbuy_defaults_to_false() {
        assert!(!Config::default().fastbuy_enabled());
    }

    #[test]
    fn default_config_omits_fastbuy() {
        let toml =
            toml::to_string_pretty(&Config::default()).expect("default config should serialize");
        assert!(!toml.contains("fastbuy"));
    }

    #[test]
    fn confirm_skip_does_not_enable_fastbuy() {
        let config: Config = toml::from_str("confirm_skip = true").expect("config should parse");
        assert!(!config.fastbuy_enabled());
    }

    #[test]
    fn anti_detection_defaults_are_sane() {
        let cfg = Config::default();
        let ad = &cfg.anti_detection;
        assert!(ad.enabled);
        assert!(ad.movement_simulation_enabled);
        assert!(!ad.session_cycling_enabled);
        assert!(ad.dummy_activity_enabled);
        assert!(ad.sign_typing_min_ms < ad.sign_typing_max_ms);
        assert!(ad.human_pause_min_ms < ad.human_pause_max_ms);
        assert!(ad.rotation_interval_min_secs < ad.rotation_interval_max_secs);
        assert!(ad.jump_interval_min_secs < ad.jump_interval_max_secs);
        assert!(ad.walk_interval_min_secs < ad.walk_interval_max_secs);
        assert!(ad.island_hop_interval_min_secs < ad.island_hop_interval_max_secs);
        assert!(ad.session_min_secs < ad.session_max_secs);
        assert!(ad.idle_gap_min_secs < ad.idle_gap_max_secs);
        assert!(ad.dummy_activity_interval_min_secs < ad.dummy_activity_interval_max_secs);
    }

    #[test]
    fn anti_detection_config_round_trips() {
        let original = Config::default();
        let serialized = toml::to_string_pretty(&original).expect("serialize");
        let deserialized: Config = toml::from_str(&serialized).expect("deserialize");
        assert_eq!(
            deserialized.anti_detection.high_value_flip_threshold,
            original.anti_detection.high_value_flip_threshold
        );
        assert_eq!(
            deserialized.anti_detection.sign_typing_min_ms,
            original.anti_detection.sign_typing_min_ms
        );
    }

    #[test]
    fn is_high_value_flip_threshold() {
        let cfg = Config::default();
        assert!(!cfg.is_high_value_flip(1_000_000));
        assert!(cfg.is_high_value_flip(5_000_000));
        assert!(cfg.is_high_value_flip(10_000_000));
    }
}
