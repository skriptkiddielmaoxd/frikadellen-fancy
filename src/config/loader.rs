use super::types::Config;
use anyhow::{Context, Result};
use std::fs;
use std::path::PathBuf;
use tracing::info;

pub struct ConfigLoader {
    config_path: PathBuf,
}

impl ConfigLoader {
    pub fn new() -> Self {
        let config_path = Self::get_config_path();
        Self { config_path }
    }

    fn get_config_path() -> PathBuf {
        // Prefer a per-user config location (e.g. %APPDATA% on Windows).
        // Storing config in the executable directory causes permission errors
        // when the app is installed to Program Files. Fall back to the
        // current directory if a platform config dir cannot be determined.
        if let Some(mut cfg_dir) = dirs::config_dir() {
            cfg_dir.push("Frikadellen BAF");
            return cfg_dir.join("config.toml");
        }

        // Fallback: use executable directory so local dev runs behave the same
        match std::env::current_exe() {
            Ok(exe_path) => exe_path
                .parent()
                .map(|p| p.join("config.toml"))
                .unwrap_or_else(|| PathBuf::from("config.toml")),
            Err(_) => PathBuf::from("config.toml"),
        }
    }

    pub fn load(&self) -> Result<Config> {
        if !self.config_path.exists() {
            info!(
                "Config file not found, creating default config at {:?}",
                self.config_path
            );
            let config = Config::default();
            self.save(&config)?;
            return Ok(config);
        }

        let contents =
            fs::read_to_string(&self.config_path).context("Failed to read config file")?;

        let config = Self::parse_config(&contents)?;

        // Merge any missing fields from defaults into the loaded config
        // (matches TypeScript initConfigHelper: "add new default values to existing config
        //  if new property was added in newer version").
        // Currently there are no programmatic merges to perform here —
        // main.rs prompts the user for any missing interactive fields (e.g. webhook_url).

        info!("Loaded configuration from {:?}", self.config_path);
        Ok(config)
    }

    fn parse_config(contents: &str) -> Result<Config> {
        let value: toml::Value = toml::from_str(contents).context("Failed to parse config file")?;

        value
            .try_into()
            .context("Failed to deserialize config file")
    }

    pub fn save(&self, config: &Config) -> Result<()> {
        // Ensure parent directory exists
        if let Some(parent) = self.config_path.parent() {
            fs::create_dir_all(parent).context("Failed to create config directory")?;
        }

        let toml_string = toml::to_string_pretty(config).context("Failed to serialize config")?;

        fs::write(&self.config_path, toml_string).context("Failed to write config file")?;

        info!("Saved configuration to {:?}", self.config_path);
        Ok(())
    }

    /// List named configs saved under the `configs/` subdirectory next to the main config.
    pub fn list_named_configs(&self) -> Result<Vec<String>> {
        let configs_dir = self
            .config_path
            .parent()
            .map(|p| p.join("configs"))
            .unwrap_or_else(|| PathBuf::from("configs"));

        if !configs_dir.exists() {
            return Ok(Vec::new());
        }

        let mut names = Vec::new();
        for entry in fs::read_dir(&configs_dir).context("Failed to read configs dir")? {
            let entry = entry.context("Failed to read config entry")?;
            let path = entry.path();
            if path.is_file() {
                if let Some(ext) = path.extension() {
                    if ext == "toml" {
                        if let Some(stem) = path.file_stem().and_then(|s| s.to_str()) {
                            names.push(stem.to_string());
                        }
                    }
                }
            }
        }

        Ok(names)
    }

    /// Save the provided config as a named config (configs/<name>.toml).
    pub fn save_named_config(&self, name: &str, config: &Config) -> Result<()> {
        let mut configs_dir = self
            .config_path
            .parent()
            .map(|p| p.join("configs"))
            .unwrap_or_else(|| PathBuf::from("configs"));
        fs::create_dir_all(&configs_dir).context("Failed to create configs directory")?;
        configs_dir.push(format!("{}.toml", name));
        let toml_string = toml::to_string_pretty(config).context("Failed to serialize config")?;
        fs::write(&configs_dir, toml_string).context("Failed to write named config file")?;
        Ok(())
    }

    /// Load a named config from configs/<name>.toml and return it.
    pub fn load_named_config(&self, name: &str) -> Result<Config> {
        let mut configs_dir = self
            .config_path
            .parent()
            .map(|p| p.join("configs"))
            .unwrap_or_else(|| PathBuf::from("configs"));
        configs_dir.push(format!("{}.toml", name));
        let contents =
            fs::read_to_string(&configs_dir).context("Failed to read named config file")?;
        Self::parse_config(&contents)
    }

    /// Delete a named config file if it exists.
    pub fn delete_named_config(&self, name: &str) -> Result<()> {
        let mut configs_dir = self
            .config_path
            .parent()
            .map(|p| p.join("configs"))
            .unwrap_or_else(|| PathBuf::from("configs"));
        configs_dir.push(format!("{}.toml", name));
        if configs_dir.exists() {
            fs::remove_file(&configs_dir).context("Failed to delete named config file")?;
        }
        Ok(())
    }

    pub fn update_property<F>(&self, mut updater: F) -> Result<()>
    where
        F: FnMut(&mut Config),
    {
        let mut config = self.load()?;
        updater(&mut config);
        self.save(&config)?;
        Ok(())
    }
}

#[cfg(test)]
mod tests {
    use super::ConfigLoader;

    #[test]
    fn parse_config_does_not_map_confirm_skip_to_fastbuy() {
        let config =
            ConfigLoader::parse_config("confirm_skip = true").expect("config should parse");
        assert!(!config.fastbuy_enabled());

        let config = ConfigLoader::parse_config("fastbuy = true\nconfirm_skip = false")
            .expect("config should parse");
        assert!(config.fastbuy_enabled());

        let config = ConfigLoader::parse_config("fastbuy = false\nconfirm_skip = true")
            .expect("config should parse");
        assert!(!config.fastbuy_enabled());
    }
}

impl Default for ConfigLoader {
    fn default() -> Self {
        Self::new()
    }
}
