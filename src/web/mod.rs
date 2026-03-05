pub mod routes;
pub mod state;

pub use state::{WebEventLog, WebState};

use axum::{
    routing::{get, post, put},
    Router,
};
use std::sync::Arc;
use tower_http::cors::CorsLayer;
use tracing::info;

/// Start the Axum web server on the configured port.
///
/// This function runs forever (or until the runtime shuts down).
pub async fn start_web_server(state: Arc<WebState>, port: u16) {
    let app = Router::new()
        .route("/", get(routes::index))
        .route("/api/status", get(routes::status))
        .route("/api/inventory", get(routes::inventory))
        .route("/api/events", get(routes::events))
        .route("/api/flips", get(routes::flips))
        .route("/api/stats", get(routes::stats))
        .route("/api/command", post(routes::send_command))
        .route("/api/toggle", post(routes::toggle_running))
        .route("/api/config", get(routes::get_config))
        .route("/api/config", put(routes::update_config))
        .route("/api/configs", get(routes::list_named_configs))
        .route("/api/configs", post(routes::save_named_config))
        .route("/api/configs/load", post(routes::load_named_config))
        .route("/api/configs/delete", post(routes::delete_named_config))
        .route("/ws", get(routes::ws_handler))
        .layer(CorsLayer::permissive())
        .with_state(state);

    let addr = format!("0.0.0.0:{}", port);
    let listener = match tokio::net::TcpListener::bind(&addr).await {
        Ok(l) => l,
        Err(e) => {
            tracing::error!("Failed to bind web GUI port {}: {}", port, e);
            return;
        }
    };

    info!("Web GUI running at http://localhost:{}", port);
    if let Err(e) = axum::serve(listener, app).await {
        tracing::error!("Web server error: {}", e);
    }
}
