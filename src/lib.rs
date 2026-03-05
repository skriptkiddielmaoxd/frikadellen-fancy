//! Frikadellen BAF (Bazaar Auction Flipper) for Hypixel Skyblock
//!
//! A high-performance Minecraft bot for automated bazaar and auction house flipping.
//! Rust port of the original TypeScript implementation using the Azalea framework.

pub mod bot;
pub mod config;
pub mod discord;
pub mod gui;
pub mod handlers;
pub mod inventory;
pub mod logging;
pub mod state;
pub mod types;
pub mod utils;
pub mod web;
pub mod webhook;
pub mod websocket;

pub use bot::{BotClient, BotEvent, BotEventHandlers};
pub use types::{BazaarFlipRecommendation, BotState, CommandPriority, CommandType, Flip};
