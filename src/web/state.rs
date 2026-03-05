use parking_lot::RwLock;
use std::collections::VecDeque;
use std::sync::atomic::{AtomicBool, AtomicI64, AtomicU32, Ordering};
use std::sync::Arc;
use std::time::Instant;
use tokio::sync::broadcast;

use crate::bot::BotClient;
use crate::config::loader::ConfigLoader;
use crate::config::types::Config;
use crate::state::CommandQueue;
use crate::websocket::CoflWebSocket;

/// Maximum number of events to keep in the ring buffer.
const MAX_EVENTS: usize = 200;

/// Maximum number of flips to retain in the history ring buffer.
const MAX_FLIPS: usize = 500;

/// A single event entry displayed in the web dashboard feed.
#[derive(Clone, serde::Serialize)]
pub struct WebEvent {
    /// Unix-millis timestamp
    pub timestamp: u64,
    /// Category tag used for colour coding in the UI
    pub kind: &'static str,
    /// Human-readable message (may contain Minecraft § colour codes — the
    /// frontend strips or converts them).
    pub message: String,
}

/// Thread-safe ring buffer of recent events for the dashboard feed.
#[derive(Clone)]
pub struct WebEventLog {
    inner: Arc<RwLock<VecDeque<WebEvent>>>,
}

impl WebEventLog {
    pub fn new() -> Self {
        Self {
            inner: Arc::new(RwLock::new(VecDeque::with_capacity(MAX_EVENTS + 1))),
        }
    }

    /// Push a new event. Oldest events are evicted when the buffer is full.
    pub fn push(&self, kind: &'static str, message: String) {
        let event = WebEvent {
            timestamp: std::time::SystemTime::now()
                .duration_since(std::time::UNIX_EPOCH)
                .unwrap_or_default()
                .as_millis() as u64,
            kind,
            message,
        };
        let mut buf = self.inner.write();
        if buf.len() >= MAX_EVENTS {
            buf.pop_front();
        }
        buf.push_back(event);
    }

    /// Return a snapshot of all stored events (oldest first).
    pub fn snapshot(&self) -> Vec<WebEvent> {
        self.inner.read().iter().cloned().collect()
    }
}

/// A single completed flip recorded in the history buffer.
#[derive(Clone, serde::Serialize)]
pub struct FlipEntry {
    pub item: String,
    pub buy_price: i64,
    pub sell_price: i64,
    pub profit: i64,
    /// `"sold"`, `"pending"`, or `"error"`
    pub outcome: &'static str,
    pub timestamp: u64,
    pub buy_speed_ms: Option<u64>,
    pub tag: Option<String>,
}

/// Thread-safe ring buffer of recent flip records.
#[derive(Clone)]
pub struct FlipHistory {
    inner: Arc<RwLock<VecDeque<FlipEntry>>>,
}

impl FlipHistory {
    pub fn new() -> Self {
        Self {
            inner: Arc::new(RwLock::new(VecDeque::with_capacity(MAX_FLIPS + 1))),
        }
    }

    pub fn push(&self, entry: FlipEntry) {
        let mut buf = self.inner.write();
        if buf.len() >= MAX_FLIPS {
            buf.pop_front();
        }
        buf.push_back(entry);
    }

    /// Return up to `limit` most-recent flips (newest first).
    pub fn recent(&self, limit: usize) -> Vec<FlipEntry> {
        let buf = self.inner.read();
        buf.iter().rev().take(limit).cloned().collect()
    }
}

/// Atomic session stats (profit, flip count, win count, coins in/out).
pub struct SessionStats {
    pub session_profit:      AtomicI64,
    pub total_coins_spent:   AtomicI64,
    pub total_coins_earned:  AtomicI64,
    pub total_flips:         AtomicU32,
    pub win_count:           AtomicU32,
}

impl SessionStats {
    pub fn new() -> Self {
        Self {
            session_profit:     AtomicI64::new(0),
            total_coins_spent:  AtomicI64::new(0),
            total_coins_earned: AtomicI64::new(0),
            total_flips:        AtomicU32::new(0),
            win_count:          AtomicU32::new(0),
        }
    }

    pub fn record_flip(&self, buy_price: i64, sell_price: i64) {
        let profit = sell_price - buy_price;
        self.session_profit.fetch_add(profit, Ordering::Relaxed);
        self.total_coins_spent.fetch_add(buy_price, Ordering::Relaxed);
        self.total_coins_earned.fetch_add(sell_price, Ordering::Relaxed);
        self.total_flips.fetch_add(1, Ordering::Relaxed);
        if profit > 0 {
            self.win_count.fetch_add(1, Ordering::Relaxed);
        }
    }
}

/// Shared application state handed to every Axum handler via `State<Arc<WebState>>`.
pub struct WebState {
    pub bot_client: BotClient,
    pub command_queue: CommandQueue,
    pub ws_client: CoflWebSocket,
    pub event_log: WebEventLog,
    pub flip_history: FlipHistory,
    pub session_stats: Arc<SessionStats>,
    pub config: Config,
    pub config_loader: Arc<ConfigLoader>,
    pub start_time: Instant,
    pub ingame_name: String,
    /// Global running flag — when false the script pauses flip processing.
    pub script_running: Arc<AtomicBool>,
    /// Broadcast channel for real-time UI streaming (WebSocket subscribers).
    pub ui_broadcast: broadcast::Sender<String>,
}
