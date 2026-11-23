use crate::{reopen, Session};
use std::{
    fs,
    io::Write,
    path::Path,
    time::{Duration, Instant},
};
use tauri::{AppHandle, Manager};
use tokio::time::sleep;

fn count(path: &Path) -> u64 {
    fs::metadata(path).map(|m| m.len()).unwrap_or(0)
}
async fn ready(path: &Path, before: u64) -> Result<(), String> {
    let deadline = Instant::now() + Duration::from_secs(8);
    while count(path) <= before {
        if Instant::now() > deadline {
            return Err("ready-timeout".into());
        }
        sleep(Duration::from_millis(30)).await;
    }
    Ok(())
}
pub async fn run(app: AppHandle) -> Result<(), String> {
    let root = app.state::<Session>().root.clone();
    let mut log = fs::File::create(root.join("cycles.tsv")).map_err(crate::err)?;
    ready(&root.join("files.ready"), 0).await?;
    for role in ["files", "popup"] {
        for cycle in 1..=100 {
            if let Some(w) = app.get_webview_window(role) {
                w.destroy().map_err(crate::err)?;
            }
            let path = root.join(format!("{role}.ready"));
            let before = count(&path);
            reopen(&app, role, "files").await?;
            ready(&path, before).await?;
            if role == "popup" {
                app.get_webview_window(role)
                    .ok_or("popup missing")?
                    .destroy()
                    .map_err(crate::err)?;
            }
            writeln!(log, "{role}\t{cycle}").map_err(crate::err)?;
        }
    }
    sleep(Duration::from_secs(5)).await;
    Ok(())
}
