use axum::{
    extract::State,
    extract::ws::{Message, WebSocket, WebSocketUpgrade},
    http::StatusCode,
    response::{Html, IntoResponse, Json},
};
use futures::stream::StreamExt;
use futures::SinkExt;
use serde::Deserialize;
use std::sync::Arc;
use std::sync::atomic::Ordering;
use tracing::{error, info};

use super::state::WebState;

/// Serve the single-page dashboard.
pub async fn index() -> Html<&'static str> {
    Html(include_str!("static/index.html"))
}

/// `GET /api/status` — core bot telemetry for the dashboard header cards.
pub async fn status(State(state): State<Arc<WebState>>) -> Json<serde_json::Value> {
    let uptime_secs = state.start_time.elapsed().as_secs();
    let bot_state = state.bot_client.state();
    let purse = state.bot_client.get_purse();
    let scoreboard = state.bot_client.get_scoreboard_lines();
    let queue_depth = state.command_queue.len();

    Json(serde_json::json!({
        "state": format!("{:?}", bot_state),
        "allowsCommands": bot_state.allows_commands(),
        "purse": purse,
        "queueDepth": queue_depth,
        "scoreboard": scoreboard,
        "uptimeSecs": uptime_secs,
        "player": state.ingame_name,
        "ahFlips": state.config.enable_ah_flips,
        "bazaarFlips": state.config.enable_bazaar_flips,
        "running": state.script_running.load(Ordering::Relaxed),
    }))
}

/// `GET /api/inventory` — cached player inventory JSON.
pub async fn inventory(State(state): State<Arc<WebState>>) -> Json<serde_json::Value> {
    let inv = state
        .bot_client
        .get_cached_inventory_json()
        .unwrap_or_else(|| "null".to_string());
    // inv is already a JSON string from the bot — parse it so we don't double-encode.
    let parsed: serde_json::Value =
        serde_json::from_str(&inv).unwrap_or(serde_json::Value::Null);
    Json(serde_json::json!({ "inventory": parsed }))
}

/// `GET /api/events` — return the recent event log snapshot.
pub async fn events(State(state): State<Arc<WebState>>) -> Json<serde_json::Value> {
    let events = state.event_log.snapshot();
    Json(serde_json::json!({ "events": events }))
}

/// Body for `POST /api/command`.
#[derive(Deserialize)]
pub struct CommandBody {
    pub command: String,
}

/// `POST /api/command` — send a command (same logic as the console handler).
pub async fn send_command(
    State(state): State<Arc<WebState>>,
    Json(body): Json<CommandBody>,
) -> Result<Json<serde_json::Value>, StatusCode> {
    let input = body.command.trim().to_string();
    if input.is_empty() {
        return Ok(Json(serde_json::json!({ "ok": false, "error": "empty command" })));
    }

    let lowercase = input.to_lowercase();

    if lowercase.starts_with("/cofl") || lowercase.starts_with("/baf") {
        let parts: Vec<&str> = input.split_whitespace().collect();
        if parts.len() > 1 {
            let command = parts[1].to_string();
            let args = parts[2..].join(" ");
            let data_json =
                serde_json::to_string(&args).unwrap_or_else(|_| "\"\"".to_string());
            let message = serde_json::json!({
                "type": command,
                "data": data_json
            })
            .to_string();
            if let Err(e) = state.ws_client.send_message(&message).await {
                error!("Web GUI: failed to send /cofl command: {}", e);
                return Ok(Json(
                    serde_json::json!({ "ok": false, "error": format!("{}", e) }),
                ));
            }
            info!("Web GUI sent /cofl {} {}", command, args);
        }
    } else if input.starts_with('/') {
        state.command_queue.enqueue(
            crate::types::CommandType::SendChat {
                message: input.clone(),
            },
            crate::types::CommandPriority::High,
            false,
        );
        info!("Web GUI queued Minecraft command: {}", input);
    } else {
        // Non-slash → send as COFL chat
        let data_json =
            serde_json::to_string(&input).unwrap_or_else(|_| "\"\"".to_string());
        let message = serde_json::json!({
            "type": "chat",
            "data": data_json
        })
        .to_string();
        if let Err(e) = state.ws_client.send_message(&message).await {
            error!("Web GUI: failed to send chat: {}", e);
            return Ok(Json(
                serde_json::json!({ "ok": false, "error": format!("{}", e) }),
            ));
        }
    }

    state.event_log.push("command", format!("[Web] {}", input));
    Ok(Json(serde_json::json!({ "ok": true })))
}

/// `POST /api/toggle` — start or stop the script.
pub async fn toggle_running(
    State(state): State<Arc<WebState>>,
) -> Json<serde_json::Value> {
    let was_running = state.script_running.load(Ordering::Relaxed);
    let now_running = !was_running;
    state.script_running.store(now_running, Ordering::Relaxed);
    let label = if now_running { "started" } else { "stopped" };
    info!("Script {} via Web GUI", label);
    state.event_log.push("system", format!("Script {} via Web GUI", label));
    Json(serde_json::json!({ "running": now_running }))
}

/// `GET /api/config` — return the current config as JSON.
pub async fn get_config(
    State(state): State<Arc<WebState>>,
) -> Json<serde_json::Value> {
    match state.config_loader.load() {
        Ok(cfg) => {
            // Serialize the Config struct to a serde_json::Value
            let val = serde_json::to_value(&cfg).unwrap_or(serde_json::Value::Null);
            Json(val)
        }
        Err(e) => {
            error!("Failed to load config: {}", e);
            Json(serde_json::json!({ "error": format!("{}", e) }))
        }
    }
}

/// `GET /api/configs` — list saved named configs
pub async fn list_named_configs(State(state): State<Arc<WebState>>) -> Json<serde_json::Value> {
    match state.config_loader.list_named_configs() {
        Ok(list) => Json(serde_json::json!({ "configs": list })),
        Err(e) => {
            error!("Failed to list named configs: {}", e);
            Json(serde_json::json!({ "configs": [], "error": format!("{}", e) }))
        }
    }
}

#[derive(Deserialize)]
pub struct NamedConfigBody {
    pub name: String,
    #[serde(default)]
    pub config: Option<serde_json::Value>,
}

/// `POST /api/configs` — save current config under given name
pub async fn save_named_config(
    State(state): State<Arc<WebState>>,
    Json(body): Json<NamedConfigBody>,
) -> Json<serde_json::Value> {
    // If the UI provided a config payload, use that to save; otherwise fall back to loading disk.
    let cfg_result: Result<crate::config::Config, anyhow::Error> = if let Some(val) = &body.config {
        serde_json::from_value(val.clone()).map_err(|e| anyhow::anyhow!(e.to_string()))
    } else {
        state.config_loader.load().map_err(|e| e.into())
    };

    match cfg_result {
        Ok(cfg) => match state.config_loader.save_named_config(&body.name, &cfg) {
            Ok(()) => {
                info!("Saved named config: {}", body.name);
                Json(serde_json::json!({ "ok": true }))
            }
            Err(e) => {
                error!("Failed to save named config: {}", e);
                Json(serde_json::json!({ "ok": false, "error": format!("{}", e) }))
            }
        },
        Err(e) => {
            error!("Failed to obtain config for saving: {}", e);
            Json(serde_json::json!({ "ok": false, "error": format!("{}", e) }))
        }
    }
}

/// `POST /api/configs/load` — load a named config and overwrite current config
pub async fn load_named_config(
    State(state): State<Arc<WebState>>,
    Json(body): Json<NamedConfigBody>,
) -> Json<serde_json::Value> {
    match state.config_loader.load_named_config(&body.name) {
        Ok(cfg) => {
            if let Err(e) = state.config_loader.save(&cfg) {
                error!("Failed to apply named config {}: {}", body.name, e);
                return Json(serde_json::json!({ "ok": false, "error": format!("{}", e) }));
            }
            info!("Loaded named config: {}", body.name);
            state.event_log.push("system", format!("Loaded named config: {}", body.name));
            // Return the applied config to the caller so UI can update immediately.
            let val = serde_json::to_value(&cfg).unwrap_or(serde_json::Value::Null);
            Json(serde_json::json!({ "ok": true, "config": val }))
        }
        Err(e) => {
            error!("Failed to load named config {}: {}", body.name, e);
            Json(serde_json::json!({ "ok": false, "error": format!("{}", e) }))
        }
    }
}

/// `POST /api/configs/delete` — delete a named config file
pub async fn delete_named_config(
    State(state): State<Arc<WebState>>,
    Json(body): Json<NamedConfigBody>,
) -> Json<serde_json::Value> {
    match state.config_loader.delete_named_config(&body.name) {
        Ok(()) => {
            info!("Deleted named config: {}", body.name);
            state.event_log.push("system", format!("Deleted named config: {}", body.name));
            Json(serde_json::json!({ "ok": true }))
        }
        Err(e) => {
            error!("Failed to delete named config {}: {}", body.name, e);
            Json(serde_json::json!({ "ok": false, "error": format!("{}", e) }))
        }
    }
}

/// Body for `PUT /api/config`.
#[derive(Deserialize)]
pub struct ConfigUpdateBody {
    #[serde(flatten)]
    pub fields: serde_json::Map<String, serde_json::Value>,
}

/// `PUT /api/config` — update config fields and persist to disk.
pub async fn update_config(
    State(state): State<Arc<WebState>>,
    Json(body): Json<ConfigUpdateBody>,
) -> Json<serde_json::Value> {
    let result = state.config_loader.update_property(|cfg| {
        // Merge each field from the request into the config
        if let Ok(mut val) = serde_json::to_value(&*cfg) {
            if let Some(obj) = val.as_object_mut() {
                for (k, v) in &body.fields {
                    obj.insert(k.clone(), v.clone());
                }
            }
            if let Ok(updated) = serde_json::from_value(val) {
                *cfg = updated;
            }
        }
    });
    match result {
        Ok(()) => {
            info!("Config updated via UI API");
            state.event_log.push("system", "Config updated via UI".to_string());
            Json(serde_json::json!({ "ok": true }))
        }
        Err(e) => {
            error!("Failed to update config: {}", e);
            Json(serde_json::json!({ "ok": false, "error": format!("{}", e) }))
        }
    }
}

/// `GET /ws` — WebSocket endpoint for real-time UI streaming.
pub async fn ws_handler(
    ws: WebSocketUpgrade,
    State(state): State<Arc<WebState>>,
) -> impl IntoResponse {
    ws.on_upgrade(move |socket| handle_ws_client(socket, state))
}

async fn handle_ws_client(socket: WebSocket, state: Arc<WebState>) {
    let (mut ws_tx, mut ws_rx) = socket.split();
    let mut broadcast_rx = state.ui_broadcast.subscribe();

    // Send initial status snapshot
    let uptime_secs = state.start_time.elapsed().as_secs();
    let bot_state = state.bot_client.state();
    let purse = state.bot_client.get_purse();
    let queue_depth = state.command_queue.len();
    let initial = serde_json::json!({
        "type": "status",
        "state": format!("{:?}", bot_state),
        "purse": purse,
        "queueDepth": queue_depth,
        "uptimeSecs": uptime_secs,
        "player": state.ingame_name,
        "running": state.script_running.load(Ordering::Relaxed),
        "ahFlips": state.config.enable_ah_flips,
        "bazaarFlips": state.config.enable_bazaar_flips,
    });
    let _ = ws_tx.send(Message::Text(initial.to_string())).await;

    // Forward broadcast messages to this client
    let send_task = tokio::spawn(async move {
        loop {
            match broadcast_rx.recv().await {
                Ok(msg) => {
                    if ws_tx.send(Message::Text(msg)).await.is_err() {
                        break; // client disconnected
                    }
                }
                Err(tokio::sync::broadcast::error::RecvError::Lagged(n)) => {
                    // Receiver fell behind during a burst (e.g. warp) — skip
                    // the dropped messages and keep streaming.
                    tracing::debug!("UI WS client lagged by {} messages, skipping", n);
                    continue;
                }
                Err(tokio::sync::broadcast::error::RecvError::Closed) => {
                    break; // broadcast channel shut down
                }
            }
        }
    });

    // Read from client (ping/pong, or close)
    let recv_task = tokio::spawn(async move {
        while let Some(Ok(_msg)) = ws_rx.next().await {
            // We don't process incoming WS messages from the UI currently
        }
    });

    // When either task finishes, abort the other
    tokio::select! {
        _ = send_task => {},
        _ = recv_task => {},
    }
}
