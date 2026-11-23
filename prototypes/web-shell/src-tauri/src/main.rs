#![windows_subsystem = "windows"]

mod core;
mod stress;
mod validation;
use core::{cancel_core, core_request, Core, Request};
use serde::Deserialize;
use std::{fs, path::PathBuf, sync::Mutex};
use tauri::window::Color;
use tauri::{
    AppHandle, LogicalPosition, LogicalSize, Manager, WebviewUrl, WebviewWindow,
    WebviewWindowBuilder,
};

const GROUPS: [&str; 3] = ["files", "games", "apps"];
struct Session {
    root: PathBuf,
    expanded: Mutex<[f64; 3]>,
    popover: tokio::sync::Mutex<()>,
}
#[derive(Deserialize)]
#[serde(tag = "kind", rename_all = "lowercase", deny_unknown_fields)]
enum Action {
    Ready,
    Restore,
    Save,
    Move { x: i32, y: i32 },
    Size { width: u32, height: u32 },
    Collapse { collapsed: bool },
    Open { id: u8 },
    Popover { group: String },
    Dismiss,
    Recover,
    Close,
}
fn layout(app: &AppHandle, role: &str) -> Result<(f64, f64, f64, f64), String> {
    let monitor = app
        .primary_monitor()
        .map_err(err)?
        .ok_or("monitor unavailable")?;
    let area = monitor.work_area();
    let scale = monitor.scale_factor();
    let x = area.position.x as f64 / scale;
    let y = area.position.y as f64 / scale;
    let w = area.size.width as f64 / scale;
    let h = area.size.height as f64 / scale;
    Ok(match role {
        "dock" => (x + (w - 530.) / 2., y + h - 100., 530., 76.),
        "apps" => (x + w - 410., y + 24., 386., 272.),
        "games" => (x + 24., y + 314., 386., 272.),
        _ => (x + 24., y + 24., 386., 272.),
    })
}
fn err(e: impl std::fmt::Display) -> String {
    e.to_string()
}
fn show(app: &AppHandle, role: &str, group: &str) -> Result<(), String> {
    let (mut x, mut y, mut w, mut h) = layout(app, role)?;
    if role == "popup" {
        let (dock_x, dock_y, _, _) = layout(app, "dock")?;
        let index = GROUPS
            .iter()
            .position(|g| *g == group)
            .ok_or("unknown group")?;
        w = 228.;
        h = 266.;
        let monitor = app
            .primary_monitor()
            .map_err(err)?
            .ok_or("monitor unavailable")?;
        let area = monitor.work_area();
        let left = area.position.x as f64 / monitor.scale_factor();
        let right = left + area.size.width as f64 / monitor.scale_factor();
        x = (dock_x + 119. + 66. * index as f64 - w / 2.).clamp(left, (right - w).max(left));
        y = (dock_y - h - 2.).max(area.position.y as f64 / monitor.scale_factor());
    }
    let exercise = std::env::args().any(|s| s == "--exercise");
    let url = format!(
        "index.html?role={role}&group={group}&pilot=1&exercise={}",
        u8::from(exercise)
    );
    let title = format!("NexSpace CorePilot · {role}");
    let state = app.state::<Session>();
    WebviewWindowBuilder::new(app, role, WebviewUrl::App(url.into()))
        .title(title)
        .decorations(false)
        .transparent(true)
        .shadow(false)
        .background_color(Color(0, 0, 0, 0))
        .resizable(false)
        .data_directory(state.root.join("webview"))
        .position(x, y)
        .inner_size(w, h)
        .on_new_window(|_, _| tauri::webview::NewWindowResponse::Deny)
        .on_navigation(|url| {
            url.host_str() == Some("tauri.localhost")
                && matches!(url.scheme(), "http" | "https")
                && url.path() == "/index.html"
        })
        .build()
        .map_err(|e| match e {
            tauri::Error::WindowLabelAlreadyExists(_)
            | tauri::Error::WebviewLabelAlreadyExists(_) => "label-closing".to_owned(),
            other => err(other),
        })?;
    Ok(())
}
// Native destruction is asynchronous. The window and WebView labels retire separately.
async fn reopen(app: &AppHandle, role: &str, group: &str) -> Result<(), String> {
    let deadline = std::time::Instant::now() + std::time::Duration::from_secs(2);
    loop {
        match show(app, role, group) {
            Err(e) if e == "label-closing" && std::time::Instant::now() < deadline => {
                tokio::time::sleep(std::time::Duration::from_millis(25)).await;
            }
            result => return result,
        }
    }
}
#[link(name = "dwmapi")]
extern "system" {
    fn DwmGetColorizationColor(color: *mut u32, opaque: *mut i32) -> i32;
}

#[tauri::command]
async fn dispatch(window: WebviewWindow, request: Action) -> Result<Option<String>, String> {
    let app = window.app_handle();
    let state = app.state::<Session>();
    let role = window.label();
    let index = GROUPS.iter().position(|g| *g == role);
    match request {
        Action::Ready => {
            use std::io::Write;
            fs::OpenOptions::new()
                .create(true)
                .append(true)
                .open(state.root.join(format!("{role}.ready")))
                .map_err(err)?
                .write_all(b".")
                .map_err(err)?;
            let (mut color, mut opaque) = (0u32, 0i32);
            if unsafe { DwmGetColorizationColor(&mut color, &mut opaque) } == 0 {
                return Ok(Some(format!("#{:06x}", color & 0xffffff)));
            }
        }
        Action::Save if role == "files" => {
            save_window(&window).await?;
        }
        Action::Restore if role == "files" => {
            let data = app.state::<Core>().call(Request::Load).await?;
            let p = &data["placement"];
            let m = app
                .primary_monitor()
                .map_err(err)?
                .ok_or("monitor unavailable")?;
            let area = m.work_area();
            let scale = m.scale_factor();
            let left = area.position.x as f64 / scale;
            let top = area.position.y as f64 / scale;
            let width = (p["width"].as_f64().ok_or("invalid-placement")?).clamp(320., 880.);
            let height = (p["height"].as_f64().ok_or("invalid-placement")?).clamp(220., 680.);
            state.expanded.lock().map_err(err)?[0] = height;
            let actual = if data["isCollapsed"] == true {
                52.
            } else {
                height
            };
            window
                .set_size(LogicalSize::new(width, actual))
                .map_err(err)?;
            let x = p["x"].as_f64().unwrap_or(left).clamp(
                left,
                (left + area.size.width as f64 / scale - width).max(left),
            );
            let y = p["y"].as_f64().unwrap_or(top).clamp(
                top,
                (top + area.size.height as f64 / scale - actual).max(top),
            );
            window
                .set_position(LogicalPosition::new(x, y))
                .map_err(err)?;
        }
        Action::Move { x, y } if index.is_some() => {
            let m = app
                .primary_monitor()
                .map_err(err)?
                .ok_or("monitor unavailable")?;
            let area = m.work_area();
            let scale = m.scale_factor();
            let size = window.outer_size().map_err(err)?;
            let limit_x =
                (area.position.x + area.size.width as i32 - size.width as i32).max(area.position.x);
            let limit_y = (area.position.y + area.size.height as i32 - size.height as i32)
                .max(area.position.y);
            window
                .set_position(tauri::PhysicalPosition::new(
                    ((x as f64 * scale) as i32).clamp(area.position.x, limit_x),
                    ((y as f64 * scale) as i32).clamp(area.position.y, limit_y),
                ))
                .map_err(err)?;
        }
        Action::Size { width, height } if index.is_some() => {
            let size = LogicalSize::new(
                width.clamp(if role == "files" { 320 } else { 220 }, 880),
                height.clamp(if role == "files" { 220 } else { 150 }, 680),
            );
            window.set_size(size).map_err(err)?;
            if let Some(m) = window.current_monitor().map_err(err)? {
                let scale = m.scale_factor();
                let area = m.work_area();
                let pos = window.outer_position().map_err(err)?;
                let x = pos.x.clamp(
                    area.position.x,
                    (area.position.x + area.size.width as i32 - (size.width as f64 * scale) as i32)
                        .max(area.position.x),
                );
                let y = pos.y.clamp(
                    area.position.y,
                    (area.position.y + area.size.height as i32
                        - (size.height as f64 * scale) as i32)
                        .max(area.position.y),
                );
                window
                    .set_position(tauri::PhysicalPosition::new(x, y))
                    .map_err(err)?;
            }
        }
        Action::Collapse { collapsed } if index.is_some() => {
            let size = window
                .inner_size()
                .map_err(err)?
                .to_logical::<f64>(window.scale_factor().map_err(err)?);
            if role == "files" {
                app.state::<Core>()
                    .call(Request::Collapse { collapsed })
                    .await?;
            }
            let mut heights = state.expanded.lock().map_err(err)?;
            let slot = &mut heights[index.unwrap()];
            if collapsed && size.height > 60. {
                *slot = size.height;
            }
            window
                .set_size(LogicalSize::new(
                    size.width,
                    if collapsed { 52. } else { *slot },
                ))
                .map_err(err)?;
        }
        Action::Open { id }
            if (6..18).contains(&id)
                && (index == Some(id as usize / 6)
                    || role == "popup"
                        && window
                            .url()
                            .map_err(err)?
                            .query_pairs()
                            .any(|(k, v)| k == "group" && v == GROUPS[id as usize / 6])) =>
        {
            let path = state.root.join(format!("reference-{id}.txt"));
            open::that_detached(path).map_err(err)?;
        }
        Action::Popover { group } if role == "dock" && GROUPS.contains(&group.as_str()) => {
            let _transition = state.popover.lock().await;
            if let Some(p) = app.get_webview_window("popup") {
                p.destroy().map_err(err)?;
            }
            reopen(app, "popup", &group).await?;
        }
        Action::Dismiss if role == "dock" || role == "popup" => {
            let _transition = state.popover.lock().await;
            if let Some(p) = app.get_webview_window("popup") {
                p.destroy().map_err(err)?;
            }
        }
        Action::Recover => {
            recover(app)?;
            if let Some(files) = app.get_webview_window("files") {
                save_window(&files).await?;
            }
        }
        Action::Close if role == "dock" || std::env::args().any(|s| s == "--measure") => {
            app.exit(0)
        }
        _ => return Err("Unsupported action for this window".into()),
    }
    Ok(None)
}
async fn save_window(window: &WebviewWindow) -> Result<(), String> {
    let scale = window.scale_factor().map_err(err)?;
    let pos = window
        .outer_position()
        .map_err(err)?
        .to_logical::<i32>(scale);
    let size = window.inner_size().map_err(err)?.to_logical::<u32>(scale);
    let expanded = if size.height < 100 {
        window.state::<Session>().expanded.lock().map_err(err)?[0] as u32
    } else {
        size.height
    };
    window
        .state::<Core>()
        .call(Request::Placement {
            x: pos.x,
            y: pos.y,
            width: size.width.clamp(320, 880),
            height: expanded.clamp(220, 680),
        })
        .await?;
    Ok(())
}
fn recover(app: &AppHandle) -> Result<(), String> {
    for (name, w) in app.webview_windows() {
        if name == "popup" {
            w.destroy().map_err(err)?;
            continue;
        }
        let (x, y, _, _) = layout(app, &name)?;
        w.set_position(LogicalPosition::new(x, y)).map_err(err)?;
        w.unminimize().map_err(err)?;
        w.show().map_err(err)?;
    }
    if app.get_webview_window("files").is_none() {
        show(app, "files", "files")?;
    }
    if let Some(w) = app.get_webview_window("files") {
        w.set_focus().map_err(err)?;
    }
    Ok(())
}
fn main() {
    tauri::Builder::default()
        .invoke_handler(tauri::generate_handler![
            dispatch,
            core_request,
            cancel_core
        ])
        .manage(Core::default())
        .setup(|app| {
            use tauri::{
                menu::{Menu, MenuItem},
                tray::TrayIconBuilder,
            };
            let restore = MenuItem::with_id(
                app,
                "recover",
                "Recuperar ventanas del piloto",
                true,
                None::<&str>,
            )?;
            let quit = MenuItem::with_id(app, "quit", "Cerrar piloto", true, None::<&str>)?;
            let menu = Menu::with_items(app, &[&restore, &quit])?;
            let mut tray = TrayIconBuilder::new()
                .tooltip("NexSpace · piloto NexCore")
                .menu(&menu);
            if let Some(icon) = app.default_window_icon() {
                tray = tray.icon(icon.clone());
            }
            tray.on_menu_event(|app, event| match event.id().as_ref() {
                "recover" => {
                    let _ = recover(app);
                    if let Some(w) = app.get_webview_window("files") {
                        tauri::async_runtime::spawn(async move {
                            let _ = save_window(&w).await;
                        });
                    }
                }
                "quit" => app.exit(0),
                _ => {}
            })
            .build(app)?;
            let root = std::env::temp_dir().join(format!(
                "NexSpace-WebLab-B-{}-{}",
                std::process::id(),
                std::time::SystemTime::now()
                    .duration_since(std::time::UNIX_EPOCH)?
                    .as_nanos()
            ));
            fs::create_dir(&root)?;
            for id in 0..18 {
                fs::write(
                    root.join(format!("reference-{id}.txt")),
                    format!("NexSpace · Ficha de demostración {id}"),
                )?;
            }
            app.manage(Session {
                root,
                expanded: Mutex::new([272.; 3]),
                popover: Default::default(),
            });
            for role in ["files", "games", "apps", "dock"] {
                if std::env::args().any(|s| s == "--one") && role != "files" {
                    continue;
                }
                if std::env::args().any(|s| s == "--three") && role == "dock" {
                    continue;
                }
                show(app.handle(), role, "files")?;
            }
            if std::env::args().any(|s| s == "--validate") {
                let app = app.handle().clone();
                tauri::async_runtime::spawn(async move {
                    let result = validation::validate(&app).await;
                    let _ = fs::write(
                        app.state::<Session>().root.join("validation.result"),
                        format!("{:?}", result),
                    );
                    app.exit(if result.is_ok() { 0 } else { 1 });
                });
            }
            if std::env::args().any(|s| s == "--faults") {
                let app = app.handle().clone();
                tauri::async_runtime::spawn(async move {
                    let path = app.state::<Session>().root.join("files.ready");
                    for _ in 0..200 {
                        if path.exists() {
                            break;
                        }
                        tokio::time::sleep(std::time::Duration::from_millis(50)).await;
                    }
                    let tested = app.state::<Core>().verify_disconnect().await;
                    let _ = fs::write(
                        app.state::<Session>().root.join("reconnect.result"),
                        format!("{:?}", tested),
                    );
                    app.exit(if tested.is_ok() { 0 } else { 1 });
                });
            }
            if std::env::args().any(|s| s == "--stress") {
                let handle = app.handle().clone();
                tauri::async_runtime::spawn(async move {
                    let result = stress::run(handle.clone()).await;
                    if let Err(e) = result {
                        let _ = fs::write(handle.state::<Session>().root.join("stress.error"), e);
                        handle.exit(1)
                    } else {
                        handle.exit(0)
                    }
                });
            }
            Ok(())
        })
        .run(tauri::generate_context!())
        .expect("NexSpace lab failed");
}
