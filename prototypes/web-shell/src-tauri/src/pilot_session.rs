use crate::err;
use serde_json::{json, Value};
use std::{fs, io, path::PathBuf};

pub struct PilotSession {
    pub id: String,
    pub evidence: PathBuf,
    _lock: fs::File,
}

impl PilotSession {
    pub fn tray_tracing() -> bool {
        std::env::var_os("NEXSPACE_FLOATING_TRAY_TRACE").is_some()
    }

    pub fn open() -> io::Result<Option<Self>> {
        let Ok(id) = std::env::var("NEXSPACE_PILOT_SESSION_ID") else {
            return Ok(None);
        };
        let invalid = || io::Error::new(io::ErrorKind::InvalidInput, "invalid pilot session");
        if id.len() != 32 || !id.bytes().all(|b| matches!(b, b'0'..=b'9' | b'a'..=b'f')) {
            return Err(invalid());
        }
        let root = std::env::temp_dir().join(format!("NexSpace-Phase4-pilot-{id}"));
        let provided = std::env::var_os("NEXSPACE_FLOATING_TEST_DIR").ok_or_else(invalid)?;
        if fs::canonicalize(provided)? != fs::canonicalize(&root)? {
            return Err(invalid());
        }
        let evidence = root.join("evidence");
        let lock = fs::OpenOptions::new()
            .read(true)
            .write(true)
            .create(true)
            .truncate(false)
            .open(evidence.join("host.lock"))?;
        lock.try_lock()
            .map_err(|error| io::Error::other(error.to_string()))?;
        Ok(Some(Self {
            id,
            evidence,
            _lock: lock,
        }))
    }

    pub fn report(
        &self,
        diagnostics: &Value,
        profile: &Value,
        groups: &Value,
    ) -> Result<(), String> {
        if diagnostics["session"] != self.id || diagnostics["profile"] != *profile {
            return Err("session-mismatch".into());
        }
        let executable = std::env::current_exe().map_err(err)?;
        let temporary = self.evidence.join("ready.tmp");
        let ready = self.evidence.join("ready.json");
        let value = json!({
            "session": self.id,
            "build": option_env!("NEXSPACE_BUILD_ID").unwrap_or("unversioned"),
            "executable": executable,
            "hostPid": std::process::id(),
            "corePid": diagnostics["corePid"],
            "database": diagnostics["database"],
            "profile": profile,
            "profileName": diagnostics["profileName"],
            "groups": groups,
            "tracing": std::env::var_os("NEXSPACE_FLOATING_DROP_TRACE").is_some(),
            "trayTracing": Self::tray_tracing()
        });
        fs::write(&temporary, value.to_string()).map_err(err)?;
        fs::rename(&temporary, &ready).map_err(err)
    }

    pub fn stop_requested(&self) -> bool {
        fs::read_to_string(self.evidence.join("stop.json"))
            .ok()
            .and_then(|raw| serde_json::from_str::<Value>(&raw).ok())
            .is_some_and(|value| value["session"] == self.id)
    }
}
