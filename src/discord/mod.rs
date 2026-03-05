use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::Arc;

use serenity::all::ChannelId;
use serenity::async_trait;
use serenity::builder::{CreateEmbed, CreateEmbedFooter, CreateMessage};
use serenity::model::channel::Message;
use serenity::model::gateway::Ready;
use serenity::model::Colour;
use serenity::prelude::*;
use tracing::{error, info, warn};

use crate::bot::BotClient;
use crate::state::CommandQueue;
use crate::web::WebEventLog;

// ── TypeMap keys ────────────────────────────────────────────────────
struct ScriptRunning;
impl TypeMapKey for ScriptRunning {
    type Value = Arc<AtomicBool>;
}
struct BotClientKey;
impl TypeMapKey for BotClientKey {
    type Value = BotClient;
}
struct CommandQueueKey;
impl TypeMapKey for CommandQueueKey {
    type Value = CommandQueue;
}
struct EventLogKey;
impl TypeMapKey for EventLogKey {
    type Value = WebEventLog;
}
struct IngameNameKey;
impl TypeMapKey for IngameNameKey {
    type Value = String;
}
struct AllowedChannelKey;
impl TypeMapKey for AllowedChannelKey {
    type Value = Option<u64>;
}

// ── Helpers ─────────────────────────────────────────────────────────

/// Format a number with M/K suffixes.
fn format_number(n: f64) -> String {
    if n.abs() >= 1_000_000.0 {
        format!("{:.2}M", n / 1_000_000.0)
    } else if n.abs() >= 1_000.0 {
        format!("{:.2}K", n / 1_000.0)
    } else {
        format!("{:.0}", n)
    }
}

fn format_purse(n: u64) -> String {
    if n >= 1_000_000_000 {
        format!("{:.2}b", n as f64 / 1_000_000_000.0)
    } else if n >= 1_000_000 {
        format!("{:.2}m", n as f64 / 1_000_000.0)
    } else if n >= 1_000 {
        format!("{:.1}k", n as f64 / 1_000.0)
    } else {
        n.to_string()
    }
}

fn format_duration(secs: u64) -> String {
    let h = secs / 3600;
    let m = (secs % 3600) / 60;
    let s = secs % 60;
    if h > 0 {
        format!("{}h {}m", h, m)
    } else if m > 0 {
        format!("{}m {}s", m, s)
    } else {
        format!("{}s", s)
    }
}

fn now_unix() -> u64 {
    std::time::SystemTime::now()
        .duration_since(std::time::UNIX_EPOCH)
        .map(|d| d.as_secs())
        .unwrap_or(0)
}

fn sanitize_item_name(name: &str) -> String {
    name.replace(|c: char| !c.is_alphanumeric() && c != '_', "_")
}

fn footer(ingame_name: &str, purse: Option<u64>) -> CreateEmbedFooter {
    let text = match purse {
        Some(p) => format!("BAF • {} • Purse: {} coins", ingame_name, format_purse(p)),
        None => format!("BAF • {}", ingame_name),
    };
    CreateEmbedFooter::new(text).icon_url(format!(
        "https://mc-heads.net/avatar/{}/32.png",
        ingame_name
    ))
}

// ── Event handler ───────────────────────────────────────────────────

struct Handler;

#[async_trait]
impl EventHandler for Handler {
    async fn message(&self, ctx: Context, msg: Message) {
        if msg.author.bot {
            return;
        }
        let content = msg.content.trim().to_lowercase();
        if !content.starts_with("!start")
            && !content.starts_with("!stop")
            && !content.starts_with("!status")
        {
            return;
        }

        // Channel restriction
        {
            let data = ctx.data.read().await;
            if let Some(Some(allowed_id)) = data.get::<AllowedChannelKey>() {
                if msg.channel_id.get() != *allowed_id {
                    return;
                }
            }
        }

        match content.as_str() {
            "!start" => {
                let data = ctx.data.read().await;
                let Some(running) = data.get::<ScriptRunning>() else { return; };
                let running = running.clone();
                let name = data.get::<IngameNameKey>().cloned().unwrap_or_default();
                let was = running.load(Ordering::Relaxed);
                let embed = if was {
                    CreateEmbed::new()
                        .title("⚠️ Script Already Running")
                        .description("The script is already active — no changes made.")
                        .color(Colour::from_rgb(255, 204, 0))
                        .footer(footer(&name, None))
                } else {
                    running.store(true, Ordering::Relaxed);
                    info!("Script started via Discord by {}", msg.author.name);
                    if let Some(log) = data.get::<EventLogKey>() {
                        log.push(
                            "system",
                            format!("Script started via Discord ({})", msg.author.name),
                        );
                    }
                    CreateEmbed::new()
                        .title("✅ Script Started")
                        .description(format!(
                            "Started by **{}**\n<t:{}:R>",
                            msg.author.name,
                            now_unix()
                        ))
                        .color(Colour::from_rgb(61, 220, 132))
                        .footer(footer(&name, None))
                };
                let _ = msg
                    .channel_id
                    .send_message(&ctx.http, CreateMessage::new().embed(embed))
                    .await;
            }
            "!stop" => {
                let data = ctx.data.read().await;
                let Some(running) = data.get::<ScriptRunning>() else { return; };
                let running = running.clone();
                let name = data.get::<IngameNameKey>().cloned().unwrap_or_default();
                let was = running.load(Ordering::Relaxed);
                let embed = if !was {
                    CreateEmbed::new()
                        .title("⚠️ Script Already Stopped")
                        .description("The script is already paused — no changes made.")
                        .color(Colour::from_rgb(255, 204, 0))
                        .footer(footer(&name, None))
                } else {
                    running.store(false, Ordering::Relaxed);
                    info!("Script stopped via Discord by {}", msg.author.name);
                    if let Some(log) = data.get::<EventLogKey>() {
                        log.push(
                            "system",
                            format!("Script stopped via Discord ({})", msg.author.name),
                        );
                    }
                    CreateEmbed::new()
                        .title("🛑 Script Stopped")
                        .description(format!(
                            "Stopped by **{}**\n<t:{}:R>",
                            msg.author.name,
                            now_unix()
                        ))
                        .color(Colour::from_rgb(255, 82, 82))
                        .footer(footer(&name, None))
                };
                let _ = msg
                    .channel_id
                    .send_message(&ctx.http, CreateMessage::new().embed(embed))
                    .await;
            }
            "!status" => {
                let data = ctx.data.read().await;
                let Some(running_arc) = data.get::<ScriptRunning>() else { return; };
                let running = running_arc.load(Ordering::Relaxed);
                let Some(bot) = data.get::<BotClientKey>() else { return; };
                let Some(queue) = data.get::<CommandQueueKey>() else { return; };
                let name = data.get::<IngameNameKey>().cloned().unwrap_or_default();
                let bot_state = format!("{:?}", bot.state());
                let purse = bot.get_purse();
                let queue_depth = queue.len();

                let (status_colour, status_text) = if running {
                    (Colour::from_rgb(61, 220, 132), "🟢 Running")
                } else {
                    (Colour::from_rgb(255, 82, 82), "🔴 Stopped")
                };

                let purse_str = purse
                    .map(|p| format!("{} coins", format_purse(p)))
                    .unwrap_or_else(|| "N/A".to_string());

                let embed = CreateEmbed::new()
                    .title("📊 BAF Status")
                    .color(status_colour)
                    .field("Status", format!("```\n{}\n```", status_text), true)
                    .field("Bot State", format!("```\n{}\n```", bot_state), true)
                    .field("\u{200b}", "\u{200b}", true) // spacer
                    .field("👤 Player", format!("```\n{}\n```", name), true)
                    .field("💰 Purse", format!("```\n{}\n```", purse_str), true)
                    .field("📋 Queue", format!("```\n{}\n```", queue_depth), true)
                    .footer(footer(&name, purse))
                    .timestamp(serenity::model::Timestamp::now());

                let _ = msg
                    .channel_id
                    .send_message(&ctx.http, CreateMessage::new().embed(embed))
                    .await;
            }
            _ => {}
        }
    }

    async fn ready(&self, _: Context, ready: Ready) {
        info!("Discord bot connected as {}", ready.user.name);
    }
}

// ── DiscordNotifier ─────────────────────────────────────────────────
// A lightweight handle that any task in main.rs can clone and use to
// push rich embeds into a Discord channel without touching serenity
// internals.

/// Cloneable handle for pushing event embeds to Discord.
#[derive(Clone)]
pub struct DiscordNotifier {
    http: Arc<serenity::http::Http>,
    channel_id: ChannelId,
    ingame_name: String,
}

impl DiscordNotifier {
    pub async fn send_embed(&self, embed: CreateEmbed) {
        match self
            .channel_id
            .send_message(&self.http, CreateMessage::new().embed(embed))
            .await
        {
            Ok(_) => warn!(
                "[Discord] Embed sent successfully to channel {}",
                self.channel_id
            ),
            Err(e) => warn!(
                "[Discord] Failed to send embed to channel {}: {}",
                self.channel_id, e
            ),
        }
    }

    // ── Canned notification builders ────────────────────────────────

    /// Fires once immediately after the Discord bot is created to confirm connectivity.
    pub async fn notify_bot_online(&self) {
        let embed = CreateEmbed::new()
            .title("🟢 Bot Online")
            .description(format!(
                "**{}** connected and ready for notifications.",
                self.ingame_name
            ))
            .color(Colour::from_rgb(46, 204, 113))
            .footer(footer(&self.ingame_name, None))
            .timestamp(serenity::model::Timestamp::now());
        self.send_embed(embed).await;
    }

    pub async fn notify_startup_complete(
        &self,
        orders_found: u64,
        ah_enabled: bool,
        bazaar_enabled: bool,
        connection_id: Option<&str>,
        premium: Option<(&str, &str)>,
    ) {
        let mut desc = format!(
            "Ready to accept flips!\n\nAH Flips: {}\nBazaar Flips: {}",
            if ah_enabled {
                "✅ Enabled"
            } else {
                "❌ Disabled"
            },
            if bazaar_enabled {
                "✅ Enabled"
            } else {
                "❌ Disabled"
            },
        );
        if let Some((tier, expires)) = premium {
            desc.push_str(&format!("\n\n**Coflnet {}** expires {}", tier, expires));
        }

        let order_disc = if bazaar_enabled {
            format!("```✓ Found {} order(s)```", orders_found)
        } else {
            "```- Skipped (Bazaar disabled)```".to_string()
        };

        let mut embed = CreateEmbed::new()
            .title("🚀 Startup Workflow Complete")
            .description(desc)
            .color(Colour::from_rgb(46, 204, 113))
            .field("1️⃣ Cookie Check", "```✓ Complete```", true)
            .field("2️⃣ Order Discovery", order_disc, true)
            .field("3️⃣ Claim Items", "```✓ Complete```", true)
            .footer(footer(&self.ingame_name, None))
            .timestamp(serenity::model::Timestamp::now());

        if let Some(conn_id) = connection_id {
            embed = embed.field("Connection ID", format!("`{}`", conn_id), false);
        }

        self.send_embed(embed).await;
    }

    pub async fn notify_item_purchased(
        &self,
        item_name: &str,
        price: u64,
        target: Option<u64>,
        profit: Option<i64>,
        purse: Option<u64>,
        buy_speed_ms: Option<u64>,
        auction_uuid: Option<&str>,
    ) {
        let safe_item = sanitize_item_name(item_name);
        let mut embed = CreateEmbed::new()
            .title("🛒 Item Purchased Successfully")
            .description(format!("**{}** • <t:{}:R>", item_name, now_unix()))
            .color(Colour::from_rgb(0, 255, 0))
            .thumbnail(format!("https://sky.coflnet.com/static/icon/{}", safe_item))
            .field(
                "💰 Purchase Price",
                format!("```fix\n{} coins\n```", format_number(price as f64)),
                true,
            )
            .footer(footer(&self.ingame_name, purse));

        if let Some(t) = target {
            embed = embed.field(
                "🎯 Target Price",
                format!("```fix\n{} coins\n```", format_number(t as f64)),
                true,
            );
        }
        if let Some(p) = profit {
            let sign = if p >= 0 { "+" } else { "" };
            let roi_str = if let Some(t) = target {
                if t > 0 && price > 0 {
                    format!(" ({:.1}%)", (p as f64 / price as f64) * 100.0)
                } else {
                    String::new()
                }
            } else {
                String::new()
            };
            embed = embed.field(
                "📈 Expected Profit",
                format!(
                    "```diff\n{}{} coins{}\n```",
                    sign,
                    format_number(p as f64),
                    roi_str
                ),
                true,
            );
        }
        if let Some(ms) = buy_speed_ms {
            embed = embed.field("⚡ Buy Speed", format!("```\n{}ms\n```", ms), true);
        }
        if let Some(uuid) = auction_uuid {
            if !uuid.is_empty() {
                embed = embed.field(
                    "🔗 Auction Link",
                    format!(
                        "[View on Coflnet](https://sky.coflnet.com/auction/{}?refId=9KKPN9)",
                        uuid
                    ),
                    false,
                );
            }
        }

        self.send_embed(embed).await;
    }

    pub async fn notify_item_sold(
        &self,
        item_name: &str,
        price: u64,
        buyer: &str,
        profit: Option<i64>,
        buy_price: Option<u64>,
        time_to_sell_secs: Option<u64>,
        purse: Option<u64>,
        auction_uuid: Option<&str>,
    ) {
        let safe_item = sanitize_item_name(item_name);
        let (status_emoji, title) = match profit {
            Some(p) if p >= 0 => ("✅", "Item Sold (Profit)"),
            Some(_) => ("❌", "Item Sold (Loss)"),
            None => ("✅", "Item Sold"),
        };

        let mut embed = CreateEmbed::new()
            .title(format!("{} {}", status_emoji, title))
            .description(format!("**{}** • <t:{}:R>", item_name, now_unix()))
            .color(Colour::from_rgb(0, 153, 255))
            .thumbnail(format!("https://sky.coflnet.com/static/icon/{}", safe_item))
            .field("👤 Buyer", format!("```\n{}\n```", buyer), true)
            .field(
                "💵 Sale Price",
                format!("```fix\n{} coins\n```", format_number(price as f64)),
                true,
            )
            .footer(footer(&self.ingame_name, purse));

        if let Some(p) = profit {
            let sign = if p >= 0 { "+" } else { "" };
            embed = embed.field(
                "💰 Net Profit",
                format!("```diff\n{}{} coins\n```", sign, format_number(p as f64)),
                true,
            );
            if let Some(bp) = buy_price {
                if bp > 0 {
                    let roi = (p as f64 / bp as f64) * 100.0;
                    embed = embed.field("📊 ROI", format!("```{:.1}%```", roi), true);
                }
            }
        }
        if let Some(secs) = time_to_sell_secs {
            embed = embed.field(
                "⏱️ Time to Sell",
                format!("```\n{}\n```", format_duration(secs)),
                true,
            );
        }
        if let Some(uuid) = auction_uuid {
            if !uuid.is_empty() {
                embed = embed.field(
                    "🔗 Auction Link",
                    format!(
                        "[View on Coflnet](https://sky.coflnet.com/auction/{}?refId=9KKPN9)",
                        uuid
                    ),
                    false,
                );
            }
        }

        self.send_embed(embed).await;
    }

    pub async fn notify_auction_listed(
        &self,
        item_name: &str,
        starting_bid: u64,
        duration_hours: u64,
        purse: Option<u64>,
    ) {
        let safe_item = sanitize_item_name(item_name);
        let expires_unix = now_unix() + duration_hours * 3600;

        let embed = CreateEmbed::new()
            .title("🏷️ BIN Auction Listed")
            .description(format!("**{}** • <t:{}:R>", item_name, now_unix()))
            .color(Colour::from_rgb(230, 126, 34))
            .thumbnail(format!("https://sky.coflnet.com/static/icon/{}", safe_item))
            .field(
                "💵 BIN Price",
                format!("```fix\n{} coins\n```", format_number(starting_bid as f64)),
                true,
            )
            .field(
                "⏳ Duration",
                format!("```\n{}h\n```", duration_hours),
                true,
            )
            .field("📅 Expires", format!("<t:{}:R>", expires_unix), true)
            .footer(footer(&self.ingame_name, purse));

        self.send_embed(embed).await;
    }
}

// ── Boot ────────────────────────────────────────────────────────────

/// Spawn the Discord bot. Returns a `DiscordNotifier` that any task can
/// use to push embeds. Returns `None` if the token is empty or the client
/// fails to build.
pub async fn start_discord_bot(
    token: String,
    script_running: Arc<AtomicBool>,
    bot_client: BotClient,
    command_queue: CommandQueue,
    event_log: WebEventLog,
    ingame_name: String,
    allowed_channel_id: Option<u64>,
) -> Option<DiscordNotifier> {
    if token.is_empty() {
        warn!("Discord bot token is empty — skipping Discord bot startup");
        return None;
    }

    let intents = GatewayIntents::GUILD_MESSAGES | GatewayIntents::MESSAGE_CONTENT;

    let mut client = match Client::builder(&token, intents)
        .event_handler(Handler)
        .await
    {
        Ok(c) => c,
        Err(e) => {
            error!("Failed to create Discord client: {}", e);
            return None;
        }
    };

    // Store shared state
    {
        let mut data = client.data.write().await;
        data.insert::<ScriptRunning>(script_running);
        data.insert::<BotClientKey>(bot_client);
        data.insert::<CommandQueueKey>(command_queue);
        data.insert::<EventLogKey>(event_log);
        data.insert::<IngameNameKey>(ingame_name.clone());
        data.insert::<AllowedChannelKey>(allowed_channel_id);
    }

    // Build the notifier from the client's HTTP handle
    let notifier = allowed_channel_id.map(|ch| DiscordNotifier {
        http: client.http.clone(),
        channel_id: ChannelId::new(ch),
        ingame_name,
    });

    // Run the gateway in a background task (runs forever)
    tokio::spawn(async move {
        if let Err(e) = client.start().await {
            error!("Discord bot error: {}", e);
        }
    });

    // Give the gateway a moment to connect before returning
    tokio::time::sleep(std::time::Duration::from_secs(2)).await;

    notifier
}
