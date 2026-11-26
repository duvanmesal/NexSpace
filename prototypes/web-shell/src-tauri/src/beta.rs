use std::{fs, io, path::PathBuf};
use tauri::{AppHandle, Manager};

pub const ENABLED: bool = cfg!(feature = "windows-beta");

pub struct BetaPaths {
    pub data: PathBuf,
    pub smoke: bool,
    _lock: fs::File,
}

impl BetaPaths {
    pub fn open(app: &AppHandle) -> Result<Option<Self>, Box<dyn std::error::Error>> {
        let mut args = std::env::args_os().skip(1);
        let smoke = args.next().is_some_and(|value| value == "--beta-smoke");
        let data = if smoke {
            let path = PathBuf::from(args.next().ok_or("missing-beta-test-directory")?);
            let expected_parent = fs::canonicalize(std::env::temp_dir())?;
            let path = fs::canonicalize(path)?;
            let name = path.file_name().and_then(|name| name.to_str()).unwrap_or_default();
            if path.parent() != Some(expected_parent.as_path()) || !name.starts_with("NexSpace-Phase4-beta-") {
                return Err("invalid-beta-test-directory".into());
            }
            path
        } else {
            app.path().app_local_data_dir()?
        };
        fs::create_dir_all(&data)?;
        let lock = fs::OpenOptions::new().read(true).write(true).create(true).truncate(false)
            .open(data.join("instance.lock"))?;
        match lock.try_lock() {
            Ok(()) => (),
            Err(std::fs::TryLockError::WouldBlock) => return Ok(None),
            Err(std::fs::TryLockError::Error(error)) => return Err(error.into()),
        }
        fs::create_dir_all(data.join("logs"))?;
        Ok(Some(Self { data, smoke, _lock: lock }))
    }

    pub fn stop_requested(&self) -> bool {
        self.smoke && self.data.join("smoke-stop").is_file()
    }

    pub fn report(&self, data: &serde_json::Value) -> io::Result<()> {
        let pending = self.data.join("smoke-ready.tmp");
        fs::write(&pending, data.to_string())?;
        fs::rename(pending, self.data.join("smoke-ready.json"))
    }
}
