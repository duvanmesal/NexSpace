use serde::{Deserialize, Serialize};
use serde_json::{json, Value};
use std::{
    process::Stdio,
    sync::atomic::{AtomicU64, Ordering},
    time::Duration,
};
use tauri::WebviewWindow;
use tokio::{
    io::{AsyncBufReadExt, AsyncReadExt, AsyncWriteExt, BufReader},
    process::{Child, ChildStdin, ChildStdout, Command},
    sync::{watch, Mutex},
    time::timeout,
};

#[derive(Serialize, Deserialize)]
#[serde(tag = "op", rename_all = "lowercase", deny_unknown_fields)]
pub enum Request {
    List,
    Load,
    Open {
        item: String,
    },
    Rename {
        item: String,
        name: String,
    },
    Reorder {
        item: String,
        offset: i8,
    },
    Placement {
        x: i32,
        y: i32,
        width: u32,
        height: u32,
    },
    Collapse {
        collapsed: bool,
    },
}
struct Pipe {
    child: Child,
    input: ChildStdin,
    output: BufReader<ChildStdout>,
}
impl Pipe {
    fn start() -> Result<Self, String> {
        let base = std::env::current_exe().map_err(|_| "core-path")?;
        let dir = base.parent().ok_or("core-path")?;
        let log = std::fs::OpenOptions::new()
            .create(true)
            .append(true)
            .open(dir.join("pilot-data/core.log"))
            .map_err(|_| "core-unprepared")?;
        let mut child = Command::new(dir.join("core/NexSpace.CorePilot.exe"))
            .creation_flags(0x08000000)
            .stdin(Stdio::piped())
            .stdout(Stdio::piped())
            .stderr(log)
            .kill_on_drop(true)
            .spawn()
            .map_err(|_| "core-start")?;
        Ok(Self {
            input: child.stdin.take().ok_or("core-input")?,
            output: BufReader::new(child.stdout.take().ok_or("core-output")?),
            child,
        })
    }
    async fn exchange(&mut self, id: u64, req: Request) -> Result<Value, String> {
        let mut msg = serde_json::to_value(req).map_err(|_| "invalid-request")?;
        msg["id"] = json!(id);
        msg["v"] = json!(1);
        let mut bytes = msg.to_string().into_bytes();
        if bytes.len() > 2048 {
            return Err("message-too-large".into());
        }
        bytes.push(b'\n');
        self.input
            .write_all(&bytes)
            .await
            .map_err(|_| "disconnected")?;
        let mut reply = Vec::new();
        (&mut self.output)
            .take(512 * 1024)
            .read_until(b'\n', &mut reply)
            .await
            .map_err(|_| "disconnected")?;
        if reply.last() != Some(&b'\n') {
            return Err("disconnected".into());
        }
        let body: Value = serde_json::from_slice(&reply).map_err(|_| "invalid-response")?;
        if body["v"] != 1 || body["id"] != id {
            return Err("correlation-failed".into());
        }
        Ok(body)
    }
}
pub struct Core {
    pipe: Mutex<Option<Pipe>>,
    next: AtomicU64,
    cancel: watch::Sender<u64>,
}
impl Default for Core {
    fn default() -> Self {
        Self {
            pipe: Mutex::new(None),
            next: AtomicU64::new(1),
            cancel: watch::channel(0).0,
        }
    }
}
impl Core {
    pub async fn call(&self, req: Request) -> Result<Value, String> {
        let mut stop = self.cancel.subscribe();
        let mut pipe = timeout(Duration::from_secs(2), self.pipe.lock())
            .await
            .map_err(|_| "core-busy")?;
        if stop.has_changed().unwrap_or(true) {
            return Err("cancelled".into());
        }
        if pipe.is_none() {
            *pipe = Some(Pipe::start()?);
        }
        let id = self.next.fetch_add(1, Ordering::Relaxed);
        let exchange = pipe.as_mut().unwrap().exchange(id, req);
        let result = tokio::select! {
            _=stop.changed()=> Err("cancelled".to_string()),
            r=timeout(Duration::from_secs(5),exchange)=>r.unwrap_or_else(|_|Err("timeout".into()))
        };
        match result {
            Ok(reply) => match reply["error"].as_str() {
                Some(code) => Err(code.into()),
                None => Ok(reply["data"].clone()),
            },
            Err(code) => {
                if let Some(mut old) = pipe.take() {
                    let _ = old.child.kill().await;
                }
                Err(code)
            }
        }
    }
    // Native-only acceptance exercise. React cannot request a process kill.
    pub async fn verify_disconnect(&self) -> Result<(), String> {
        let before = self.call(Request::Load).await?;
        {
            let mut owner = self.pipe.lock().await;
            owner
                .as_mut()
                .ok_or("missing-sidecar")?
                .child
                .kill()
                .await
                .map_err(|_| "kill-failed")?;
        }
        if self.call(Request::Load).await.is_ok() {
            return Err("expected-disconnection".into());
        }
        let after = self.call(Request::Load).await?;
        if before != after {
            return Err("reconnect-data-mismatch".into());
        }
        Ok(())
    }
    pub fn cancel(&self) {
        self.cancel.send_modify(|v| *v = v.wrapping_add(1));
    }
}
#[tauri::command]
pub async fn core_request(
    window: WebviewWindow,
    core: tauri::State<'_, Core>,
    request: Request,
) -> Result<Value, String> {
    let files = window.label() == "files";
    let popup = window.label() == "popup"
        && window
            .url()
            .map_err(|_| "invalid-window")?
            .query_pairs()
            .any(|(k, v)| k == "group" && v == "files");
    if !(files || popup && matches!(request, Request::Load | Request::Open { .. })) {
        return Err("invalid-window".into());
    }
    if matches!(request, Request::Placement { .. }) {
        return Err("host-placement-only".into());
    }
    core.call(request).await
}
#[tauri::command]
pub fn cancel_core(window: WebviewWindow, core: tauri::State<'_, Core>) -> Result<(), String> {
    if window.label() != "files" {
        return Err("invalid-window".into());
    }
    core.cancel();
    Ok(())
}
