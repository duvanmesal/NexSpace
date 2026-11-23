use crate::{
    core::{Core, Request},
    dispatch, recover, reopen, Action, Session,
};
use std::{
    fs,
    path::Path,
    time::{Duration, Instant},
};
use tauri::{AppHandle, Manager};
use tokio::time::sleep;
async fn wait(path: &Path, threshold: u64) -> Result<(), String> {
    let deadline = Instant::now() + Duration::from_secs(20);
    while fs::metadata(path).map(|v| v.len()).unwrap_or(0) <= threshold {
        if Instant::now() > deadline {
            return Err("validation-timeout".into());
        }
        sleep(Duration::from_millis(40)).await;
    }
    Ok(())
}
pub async fn validate(app: &AppHandle) -> Result<(), String> {
    let root = app.state::<Session>().root.clone();
    let ready = root.join("files.ready");
    wait(&ready, 0).await?;
    let core = app.state::<Core>();
    let original = core.call(Request::Load).await?;
    let placement = &original["placement"];
    let result = async {
        let w = app.get_webview_window("files").ok_or("missing-files")?;
        dispatch(w.clone(), Action::Collapse { collapsed: false }).await?;
        dispatch(
            w.clone(),
            Action::Size {
                width: 427,
                height: 305,
            },
        )
        .await?;
        dispatch(w.clone(), Action::Move { x: 120, y: 84 }).await?;
        dispatch(w.clone(), Action::Save).await?;
        dispatch(w.clone(), Action::Collapse { collapsed: true }).await?;
        let loaded = fs::metadata(&ready).map_err(crate::err)?.len();
        w.destroy().map_err(crate::err)?;
        reopen(app, "files", "files").await?;
        wait(&ready, loaded).await?;
        let w = app.get_webview_window("files").ok_or("reopen")?;
        let size = w
            .inner_size()
            .map_err(crate::err)?
            .to_logical::<u32>(w.scale_factor().map_err(crate::err)?);
        let pos = w
            .outer_position()
            .map_err(crate::err)?
            .to_logical::<i32>(w.scale_factor().map_err(crate::err)?);
        if size.width != 427 || size.height != 52 || pos.x != 120 || pos.y != 84 {
            return Err("geometry-on-reload".into());
        }
        dispatch(w.clone(), Action::Collapse { collapsed: false }).await?;
        let height = w
            .inner_size()
            .map_err(crate::err)?
            .to_logical::<u32>(w.scale_factor().map_err(crate::err)?)
            .height;
        if height != 305 {
            return Err("expanded-height".into());
        }
        for win in app.webview_windows().values() {
            win.hide().map_err(crate::err)?
        }
        recover(app)?;
        if !w.is_visible().map_err(crate::err)? {
            return Err("recovery-hidden".into());
        }
        fs::write(root.join("geometry.ok"), "visible").map_err(crate::err)?;
        wait(&root.join("db.locked"), 0).await?;
        if core.call(Request::Load).await.err().as_deref() != Some("timeout") {
            return Err("expected-timeout".into());
        }
        let cancel = async {
            sleep(Duration::from_millis(400)).await;
            core.cancel()
        };
        let (reply, _) = tokio::join!(core.call(Request::Load), cancel);
        if reply.err().as_deref() != Some("cancelled") {
            return Err("expected-cancellation".into());
        }
        fs::write(root.join("ipc.ok"), "timeout/cancel").map_err(crate::err)?;
        wait(&root.join("db.released"), 0).await?;
        core.verify_disconnect().await
    }
    .await;
    // La prueba usa únicamente la copia aislada. Restaurar también ante un fallo.
    core.call(Request::Placement {
        x: placement["x"].as_i64().ok_or("invalid-x")? as i32,
        y: placement["y"].as_i64().ok_or("invalid-y")? as i32,
        width: placement["width"].as_u64().ok_or("invalid-width")? as u32,
        height: placement["height"].as_u64().ok_or("invalid-height")? as u32,
    })
    .await?;
    core.call(Request::Collapse {
        collapsed: original["isCollapsed"].as_bool().ok_or("invalid-state")?,
    })
    .await?;
    result
}
