#![windows_subsystem = "windows"]

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
}
#[derive(Deserialize)]
#[serde(tag = "kind", rename_all = "lowercase", deny_unknown_fields)]
enum Action {
    Ready,
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
        "index.html?role={role}&group={group}&exercise={}",
        u8::from(exercise)
    );
    let title = format!("NexSpace B · {role}");
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
        .map_err(err)?;
    Ok(())
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
            let size = LogicalSize::new(width.clamp(220, 880), height.clamp(150, 680));
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
        Action::Open { id } if id < 18 && (role == "popup" || index == Some(id as usize / 6)) => {
            let path = state.root.join(format!("reference-{id}.txt"));
            open::that_detached(path).map_err(err)?;
        }
        Action::Popover { group } if role == "dock" && GROUPS.contains(&group.as_str()) => {
            if let Some(p) = app.get_webview_window("popup") {
                p.destroy().map_err(err)?;
            }
            show(app, "popup", &group)?;
        }
        Action::Dismiss if role == "dock" || role == "popup" => {
            if let Some(p) = app.get_webview_window("popup") {
                p.destroy().map_err(err)?;
            }
        }
        Action::Recover => {
            for (name, w) in app.webview_windows() {
                if name == "popup" {
                    w.destroy().map_err(err)?;
                    continue;
                }
                let (x, y, _, _) = layout(app, &name)?;
                w.set_position(LogicalPosition::new(x, y)).map_err(err)?;
                w.show().map_err(err)?;
            }
        }
        Action::Close if role == "dock" || std::env::args().any(|s| s == "--measure") => {
            app.exit(0)
        }
        _ => return Err("Unsupported action for this window".into()),
    }
    Ok(None)
}
fn main() {
    tauri::Builder::default()
        .invoke_handler(tauri::generate_handler![dispatch])
        .setup(|app| {
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
            Ok(())
        })
        .run(tauri::generate_context!())
        .expect("NexSpace lab failed");
}
