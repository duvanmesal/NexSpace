use crate::{
    core::Core,
    err,
    placement::{self, place, Rect},
};
use serde::{Deserialize, Serialize};
use serde_json::{json, Value};
use std::{
    collections::HashMap,
    path::PathBuf,
    sync::{
        atomic::{AtomicU64, Ordering},
        Mutex,
    },
    time::Duration,
};
use tauri::{
    AppHandle, DragDropEvent, Manager, WebviewUrl, WebviewWindow, WebviewWindowBuilder, WindowEvent,
};

thread_local! {static HOTKEY:std::cell::RefCell<Option<global_hotkey::GlobalHotKeyManager>>=const{std::cell::RefCell::new(None)};}
#[derive(Default)]
struct Runtime {
    transition: tokio::sync::Mutex<()>,
    root: PathBuf,
    webview_data: PathBuf,
    active: Mutex<Option<String>>,
    hotkey: Mutex<bool>,
    serial: AtomicU64,
    gestures: Mutex<HashMap<String, Gesture>>,
}
#[derive(Serialize, Deserialize, Default)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
struct Action {
    op: String,
    profile: Option<String>,
    target: Option<String>,
    item: Option<String>,
    name: Option<String>,
    link: Option<String>,
    flag: Option<bool>,
    #[serde(default)]
    offset: i32,
    #[serde(default)]
    category: bool,
}
fn ident(value: &Value) -> Result<&str, String> {
    value
        .as_str()
        .filter(|v| v.len() == 36 && v.bytes().all(|b| b.is_ascii_hexdigit() || b == b'-'))
        .ok_or_else(|| "invalid-id".into())
}
fn group_label(profile: &str, id: &str) -> String {
    format!("group_{profile}_{id}")
}
fn scope(label: &str) -> Option<(&str, &str, &str)> {
    let mut bits = label.split('_');
    Some((bits.next()?, bits.next()?, bits.next()?))
}
#[derive(Clone, Copy)]
struct Gesture {
    start: Rect,
    cursor: (f64, f64),
    resize: bool,
    scale: f64,
}

fn centered(work: Rect, width: f64, height: f64) -> Rect {
    Rect::new(
        work.x + (work.width - width) / 2.,
        work.y + (work.height - height) / 2.,
        width,
        height,
    )
    .fit(work)
}

fn saved_frame(group: &Value, work: Rect, scale: f64) -> Rect {
    let p = &group["placement"];
    Rect::new(
        p["x"].as_f64().unwrap_or(work.x),
        p["y"].as_f64().unwrap_or(work.y),
        p["width"].as_f64().unwrap_or(386. * scale),
        if group["isCollapsed"] == true {
            52. * scale
        } else {
            p["height"].as_f64().unwrap_or(272. * scale)
        },
    )
}

fn window(app: &AppHandle, label: &str, title: &str, r: Rect) -> Result<WebviewWindow, String> {
    let view = WebviewWindowBuilder::new(
        app,
        label,
        WebviewUrl::App(format!("index.html?floating={label}").into()),
    )
    .title(title)
    .decorations(false)
    .transparent(true)
    .shadow(false)
    .resizable(false)
    .visible(false)
    .background_color(tauri::window::Color(0, 0, 0, 0))
    .data_directory(app.state::<Runtime>().webview_data.clone())
    .on_navigation(|u| {
        u.host_str() == Some("tauri.localhost")
            && matches!(u.scheme(), "https" | "http")
            && u.path() == "/index.html"
    })
    .on_new_window(|_, _| tauri::webview::NewWindowResponse::Deny)
    .build()
    .map_err(|e| match e {
        tauri::Error::WindowLabelAlreadyExists(_) | tauri::Error::WebviewLabelAlreadyExists(_) => {
            "label-closing".into()
        }
        other => err(other),
    })?;
    if let Err(error) = place(&view, r).and_then(|_| view.show().map_err(err)) {
        let _ = view.destroy();
        return Err(error);
    }
    Ok(view)
}
async fn create(
    app: &AppHandle,
    label: &str,
    title: &str,
    r: Rect,
) -> Result<WebviewWindow, String> {
    for _ in 0..80 {
        match window(app, label, title, r) {
            Err(e) if e == "label-closing" => tokio::time::sleep(Duration::from_millis(25)).await,
            result => return result,
        }
    }
    Err("window-timeout".into())
}
async fn ipc(app: &AppHandle, msg: Value) -> Result<Value, String> {
    app.state::<Core>().exchange(msg).await
}
fn refresh(app: &AppHandle) {
    for w in app.webview_windows().values() {
        let _ = w.eval("window.dispatchEvent(new Event('library-changed'))");
    }
}
fn dismiss(app: &AppHandle) -> Result<(), String> {
    app.state::<Runtime>().active.lock().map_err(err)?.take();
    for (k, w) in app.webview_windows() {
        if k.starts_with("category_") {
            w.destroy().map_err(err)?;
        }
    }
    Ok(())
}
async fn sync(app: &AppHandle, recover: bool) -> Result<Value, String> {
    let state = app.state::<Runtime>();
    let _guard = state.transition.lock().await;
    let data = ipc(app, json!({"op":"manifest"})).await?;
    let profile = ident(&data["profile"])?;
    let groups = data["groups"].as_array().ok_or("invalid-library")?;
    let categories = data["categories"].as_array().ok_or("invalid-library")?;
    for (label, w) in app.webview_windows() {
        if let Some((kind, p, id)) = scope(&label) {
            let keep = p == profile
                && if kind == "group" {
                    groups.iter().any(|g| g["id"] == id && g["shown"] == true)
                } else {
                    categories.iter().any(|c| c["id"] == id)
                };
            if !keep {
                w.destroy().map_err(err)?;
                if kind == "category" {
                    state.active.lock().map_err(err)?.take();
                }
            }
        }
    }
    let dock_monitor = placement::named(app, data["dockMonitor"].as_str().unwrap_or_default())?;
    let work = placement::area(&dock_monitor);
    let scale = dock_monitor.scale_factor();
    let vertical = data["vertical"] == true;
    let length = (100. + categories.len() as f64 * 60.) * scale;
    let thickness = 70. * scale;
    let dock = if vertical {
        Rect::new(
            work.x + 12. * scale,
            work.y + (work.height - length) / 2.,
            thickness,
            length,
        )
    } else {
        Rect::new(
            work.x + (work.width - length) / 2.,
            work.y + work.height - 87. * scale,
            length,
            thickness,
        )
    }
    .fit(work);
    if let Some(w) = app.get_webview_window("dock") {
        place(&w, dock)?;
        if recover {
            w.unminimize().map_err(err)?;
            w.show().map_err(err)?;
        }
    }
    for g in groups.iter().filter(|g| g["shown"] == true) {
        let id = ident(&g["id"])?;
        let label = group_label(profile, id);
        let p = &g["placement"];
        let key = p["monitorKey"].as_str().unwrap_or_default();
        let monitor = placement::named(app, key)?;
        let work = placement::area(&monitor);
        let saved = saved_frame(g, work, monitor.scale_factor());
        let rect = saved.fit(work);
        if recover && (saved.x != rect.x || saved.y != rect.y) {
            let mut corrected = p.clone();
            corrected["x"] = json!(rect.position().x);
            corrected["y"] = json!(rect.position().y);
            corrected["monitorKey"] =
                json!(monitor.name().map(String::as_str).unwrap_or("primary"));
            ipc(app,json!({"op":"placement","profile":profile,"target":id,"placement":corrected,"flag":true})).await?;
        }
        if let Some(w) = app.get_webview_window(&label) {
            place(&w, rect)?;
            w.set_title(g["name"].as_str().unwrap_or("NexSpace"))
                .map_err(err)?;
            if recover {
                w.unminimize().map_err(err)?;
                w.show().map_err(err)?;
            }
        } else {
            create(app, &label, g["name"].as_str().unwrap_or("NexSpace"), rect).await?;
        }
    }
    refresh(app);
    Ok(data)
}
fn authorized(label: &str, r: &Action) -> bool {
    if label == "dock" || label == "organize" {
        return matches!(r.op.as_str(), "manifest")
            || label == "organize"
                && matches!(
                    r.op.as_str(),
                    "createGroup"
                        | "show"
                        | "createProfile"
                        | "renameProfile"
                        | "switchProfile"
                        | "createCategory"
                        | "editCategory"
                        | "deleteCategory"
                        | "moveCategory"
                        | "vertical"
                );
    }
    let Some((kind, p, id)) = scope(label) else {
        return false;
    };
    if r.profile.as_deref() != Some(p)
        || r.target.as_deref() != Some(id)
        || r.category != (kind == "category")
    {
        return false;
    }
    matches!(
        r.op.as_str(),
        "page" | "open" | "rename" | "remove" | "moveItem"
    ) || kind == "group"
        && matches!(
            r.op.as_str(),
            "renameGroup" | "deleteGroup" | "collapse" | "lock" | "listView" | "show"
        )
}
#[tauri::command]
async fn library(window: WebviewWindow, request: Action) -> Result<Value, String> {
    if !authorized(window.label(), &request) {
        return Err("invalid-window".into());
    }
    let app = window.app_handle();
    let op = request.op.clone();
    let mut message = serde_json::to_value(request).map_err(err)?;
    if op == "page" {
        let scale = window.scale_factor().unwrap_or(1.0);
        message["iconPixels"] = json!(if scale > 1.5 {
            96
        } else if scale > 1.0 {
            64
        } else {
            48
        });
    }
    let mut result = ipc(app, message).await?;
    if op == "page" && window.label().starts_with("group_") {
        result["multipleDisplays"] = json!(app.available_monitors().is_ok_and(|m| m.len() > 1));
    }
    if op == "manifest" {
        let mut v = result;
        v["selected"] = json!(*app.state::<Runtime>().active.lock().map_err(err)?);
        v["hotkey"] = json!(*app.state::<Runtime>().hotkey.lock().map_err(err)?);
        return Ok(v);
    }
    if op == "vertical" {
        dismiss(app)?;
    }
    if !matches!(op.as_str(), "open" | "page") {
        sync(app, false).await?;
    }
    Ok(result)
}
async fn add_paths(app: &AppHandle, label: &str, paths: Vec<PathBuf>) -> Result<(), String> {
    let (kind, p, id) = scope(label).ok_or("invalid-window")?;
    let mut failures = 0;
    let mut last = String::new();
    for path in paths {
        let result = ipc(
            app,
            json!({"op":"add","profile":p,"target":id,"category":kind=="category","path":path}),
        )
        .await;
        trace_drop(
            app,
            "nexcore",
            json!({"window":label,"ok":result.is_ok(),"error":result.as_ref().err()}),
        );
        if let Err(e) = result {
            failures += 1;
            last = e;
        }
    }
    refresh(app);
    if failures > 0 {
        Err(format!("{failures} · {last}"))
    } else {
        Ok(())
    }
}
fn trace_drop(app: &AppHandle, stage: &str, detail: Value) {
    use std::io::Write;
    if std::env::var_os("NEXSPACE_FLOATING_DROP_TRACE").is_none() {
        return;
    }
    let root = &app.state::<Runtime>().root;
    if let Ok(mut log) = std::fs::OpenOptions::new()
        .create(true)
        .append(true)
        .open(root.join("drop.jsonl"))
    {
        let stamp = std::time::SystemTime::now()
            .duration_since(std::time::UNIX_EPOCH)
            .unwrap_or_default();
        let _ = writeln!(
            log,
            "{}",
            json!({"ms":stamp.as_millis(),"stage":stage,"detail":detail})
        );
    }
}

fn native_drop(win: &tauri::Window, event: &DragDropEvent) {
    let app = win.app_handle();
    let label = win.label();
    let accepted = scope(label).is_some_and(|(kind, _, _)| matches!(kind, "group" | "category"));
    let scale = win.scale_factor().unwrap_or(1.0);
    if let DragDropEvent::Enter { paths, position } | DragDropEvent::Drop { paths, position } =
        event
    {
        trace_drop(
            app,
            if matches!(event, DragDropEvent::Enter { .. }) {
                "enter"
            } else {
                "drop"
            },
            json!({"window":label,"accepted":accepted,"count":paths.len(),"physical":[position.x,position.y],
                "logical":[position.x/scale,position.y/scale],"scale":scale}),
        );
    }
    if !accepted {
        return;
    }
    let Some(view) = app.get_webview_window(label) else {
        return;
    };
    let active = match event {
        DragDropEvent::Enter { paths, .. } => !paths.is_empty(),
        DragDropEvent::Leave | DragDropEvent::Drop { .. } => false,
        _ => return,
    };
    let _ = view.eval(format!(
        "document.documentElement.classList.toggle('receiving-drop',{active})"
    ));
    if let DragDropEvent::Drop { paths, .. } = event {
        let app = app.clone();
        let label = label.to_owned();
        let paths = paths.clone();
        tauri::async_runtime::spawn(async move {
            let result = add_paths(&app, &label, paths).await;
            trace_drop(&app, "persisted", json!({"window":label,"result":result}));
            if let (Some(w), Err(e)) = (app.get_webview_window(&label), result) {
                let _ = w.eval(format!(
                    "window.dispatchEvent(new CustomEvent('drop-error',{{detail:{}}}))",
                    json!(e)
                ));
            }
        });
    }
}

#[tauri::command]
async fn pick(window: WebviewWindow, folder: bool) -> Result<(), String> {
    if scope(window.label()).is_none() {
        return Err("invalid-window".into());
    }
    let owner = window.clone();
    let paths = tauri::async_runtime::spawn_blocking(move || {
        let dlg = rfd::FileDialog::new()
            .set_parent(&owner)
            .set_title("Añadir referencias");
        if folder {
            dlg.pick_folder().map(|p| vec![p])
        } else {
            dlg.pick_files()
        }
    })
    .await
    .map_err(err)?;
    if let Some(paths) = paths {
        add_paths(window.app_handle(), window.label(), paths).await?;
    }
    Ok(())
}
#[tauri::command]
async fn frame(
    window: WebviewWindow,
    phase: String,
    resize: bool,
    dx: f64,
    dy: f64,
) -> Result<(), String> {
    let (kind, p, id) = scope(window.label()).ok_or("invalid-window")?;
    if kind != "group" {
        return Err("invalid-window".into());
    }
    let app = window.app_handle();
    let state = app.state::<Runtime>();
    let label = window.label().to_owned();
    if phase == "begin" {
        let start = placement::frame(&window)?;
        let cursor = placement::cursor()?;
        let scale = window.scale_factor().map_err(err)?;
        state.gestures.lock().map_err(err)?.insert(
            label,
            Gesture {
                start,
                cursor,
                resize,
                scale,
            },
        );
        return Ok(());
    }

    let (desired, monitor) = if phase == "next-monitor" {
        let current = window
            .current_monitor()
            .map_err(err)?
            .unwrap_or(placement::primary(app)?);
        let monitors = app.available_monitors().map_err(err)?;
        let next = monitors
            .iter()
            .find(|m| m.name() != current.name())
            .cloned()
            .ok_or("second-monitor-unavailable")?;
        let rect = placement::frame(&window)?;
        let ratio = next.scale_factor() / current.scale_factor();
        (
            centered(
                placement::area(&next),
                rect.width * ratio,
                rect.height * ratio,
            ),
            next,
        )
    } else if phase == "key" {
        let scale = window.scale_factor().map_err(err)?;
        let mut rect = placement::frame(&window)?;
        rect.width += dx.clamp(-48., 48.) * scale;
        rect.height += dy.clamp(-48., 48.) * scale;
        let monitor = window
            .current_monitor()
            .map_err(err)?
            .unwrap_or(placement::primary(app)?);
        (rect, monitor)
    } else {
        let Some(gesture) = ({
            let mut gestures = state.gestures.lock().map_err(err)?;
            if phase == "end" {
                gestures.remove(&label)
            } else {
                gestures.get(&label).copied()
            }
        }) else {
            return Ok(());
        };
        let cursor = placement::cursor()?;
        let mut rect = gesture.start;
        let (x, y) = (cursor.0 - gesture.cursor.0, cursor.1 - gesture.cursor.1);
        let monitor = if gesture.resize {
            rect.width += x;
            rect.height += y;
            window.current_monitor().map_err(err)?
        } else {
            rect.x += x;
            rect.y += y;
            app.monitor_from_point(cursor.0, cursor.1).map_err(err)?
        }
        .unwrap_or(placement::primary(app)?);
        if !gesture.resize {
            let ratio = monitor.scale_factor() / gesture.scale;
            // Keep the point held in the header under the physical cursor when DPI changes.
            rect.x = cursor.0 - (gesture.cursor.0 - gesture.start.x) * ratio;
            rect.y = cursor.1 - (gesture.cursor.1 - gesture.start.y) * ratio;
            rect.width *= ratio;
            rect.height *= ratio;
        }
        (rect, monitor)
    };
    let scale = monitor.scale_factor();
    let mut rect = desired;
    rect.width = rect.width.max(320. * scale);
    rect.height = if resize {
        rect.height.max(220. * scale)
    } else {
        rect.height
    };
    let rect = rect.fit(placement::area(&monitor));
    if phase == "update" {
        return place(&window, rect);
    }

    let data = ipc(app, json!({"op":"manifest"})).await?;
    let g = data["groups"]
        .as_array()
        .and_then(|groups| groups.iter().find(|g| g["id"] == id))
        .ok_or("not-found")?;
    let expanded_height = if g["isCollapsed"] == true {
        g["placement"]["height"].clone()
    } else {
        json!(rect.size().height)
    };
    ipc(app,json!({"op":"placement","profile":p,"target":id,"placement":{
        "monitorKey":monitor.name().map(String::as_str).unwrap_or("primary"),
        "x":rect.position().x,"y":rect.position().y,"width":rect.size().width,"height":expanded_height
    }})).await?;
    sync(app, false).await?;
    Ok(())
}
#[tauri::command]
async fn surface(
    window: WebviewWindow,
    op: String,
    profile: Option<String>,
    category: Option<String>,
    anchor: Option<f64>,
) -> Result<(), String> {
    let app = window.app_handle();
    let state = app.state::<Runtime>();
    match op.as_str() {
        "organize" if window.label() == "dock" => {
            if let Some(w) = app.get_webview_window("organize") {
                w.set_focus().map_err(err)?;
            } else {
                let monitor = window
                    .current_monitor()
                    .map_err(err)?
                    .unwrap_or(placement::primary(app)?);
                let scale = monitor.scale_factor();
                create(
                    app,
                    "organize",
                    "Organizar NexSpace",
                    centered(placement::area(&monitor), 450. * scale, 420. * scale),
                )
                .await?;
            }
        }
        "category" if window.label() == "dock" => {
            let _guard = state.transition.lock().await;
            let data = ipc(app, json!({"op":"manifest"})).await?;
            let p = ident(&data["profile"])?;
            if profile.as_deref() != Some(p) {
                return Err("stale-profile".into());
            }
            let id = category.ok_or("missing-category")?;
            let cat = data["categories"]
                .as_array()
                .and_then(|a| a.iter().find(|c| c["id"] == id))
                .ok_or("not-found")?;
            let selected = state.active.lock().map_err(err)?.as_deref() == Some(&id);
            dismiss(app)?;
            if !selected {
                let pos = window.inner_position().map_err(err)?;
                let monitor = window
                    .current_monitor()
                    .map_err(err)?
                    .unwrap_or(placement::primary(app)?);
                let scale = monitor.scale_factor();
                let a = anchor.unwrap_or(36.);
                let (x, y) = if data["vertical"] == true {
                    (
                        f64::from(pos.x) + 74. * scale,
                        f64::from(pos.y) + (a - 135.) * scale,
                    )
                } else {
                    (
                        f64::from(pos.x) + (a - 128.) * scale,
                        f64::from(pos.y) - 276. * scale,
                    )
                };
                create(
                    app,
                    &format!("category_{p}_{id}"),
                    cat["name"].as_str().unwrap_or("Categoría"),
                    Rect::new(x, y, 256. * scale, 272. * scale).fit(placement::area(&monitor)),
                )
                .await?;
                *state.active.lock().map_err(err)? = Some(id);
            }
            refresh(app);
        }
        "dismiss" if window.label().starts_with("category_") => {
            let category = scope(window.label()).map(|(_, _, id)| id);
            dismiss(app)?;
            refresh(app);
            if let (Some(dock), Some(id)) = (app.get_webview_window("dock"), category) {
                let _ = dock.set_focus();
                let selector = format!("[data-category='{id}']");
                let _ = dock.eval(format!(
                    "document.querySelector({})?.focus()",
                    json!(selector)
                ));
            }
        }
        "dismiss" if window.label() == "organize" => window.destroy().map_err(err)?,
        "recover" => {
            sync(app, true).await?;
        }
        "quit" if window.label() == "organize" || window.label() == "dock" => app.exit(0),
        "ready" if !std::env::args().any(|arg| arg.starts_with("--floating-cycles=")) => (),
        "ready" => {
            let n = state.serial.fetch_add(1, Ordering::Relaxed);
            std::fs::write(
                state.root.join(format!("{}.ready", window.label())),
                n.to_string(),
            )
            .map_err(err)?;
        }
        _ => return Err("unsupported-operation".into()),
    }
    Ok(())
}
// Always surface the dock first, including when the IPC child is unavailable.
fn recover(app: &AppHandle) {
    if let Some(w) = app.get_webview_window("dock") {
        if let Ok(monitor) = placement::primary(app) {
            let work = placement::area(&monitor);
            let scale = monitor.scale_factor();
            if !w.is_visible().unwrap_or(false) || w.current_monitor().ok().flatten().is_none() {
                let _ = place(
                    &w,
                    Rect::new(
                        work.x + work.width / 2. - 160. * scale,
                        work.y + work.height - 87. * scale,
                        320. * scale,
                        70. * scale,
                    )
                    .fit(work),
                );
            }
        }
        let _ = w.unminimize();
        let _ = w.show();
        let _ = w.set_focus();
    }
    let app = app.clone();
    tauri::async_runtime::spawn(async move {
        if sync(&app, true).await.is_err() {
            // Reading the manifest again is safe after the old pipe has been discarded.
            let _ = sync(&app, true).await;
        }
    });
}
async fn watch_displays(app: AppHandle) {
    let mut previous = None;
    loop {
        if let Ok(monitors) = app.available_monitors() {
            let mut layout: Vec<_> = monitors
                .iter()
                .map(|monitor| {
                    (
                        monitor.name().cloned(),
                        placement::area(monitor),
                        monitor.scale_factor(),
                    )
                })
                .collect();
            layout.sort_by(|left, right| left.0.cmp(&right.0));
            if !layout.is_empty() && previous.as_ref().is_some_and(|last| last != &layout) {
                let _ = dismiss(&app);
                let _ = sync(&app, false).await;
            }
            if !layout.is_empty() {
                previous = Some(layout);
            }
        }
        tokio::time::sleep(Duration::from_secs(3)).await;
    }
}

pub fn run() {
    tauri::Builder::default()
        .manage(Core::floating())
        .invoke_handler(tauri::generate_handler![library, pick, frame, surface])
        .on_window_event(|win, event| {
            if matches!(event, WindowEvent::Destroyed) {
                if let Ok(mut work) = win.state::<Runtime>().gestures.lock() {
                    work.remove(win.label());
                }
            }
            if let WindowEvent::DragDrop(drop) = event {
                native_drop(win, drop);
            }
            if let tauri::WindowEvent::CloseRequested { api, .. } = event {
                let label = win.label().to_owned();
                if label == "dock" {
                    api.prevent_close();
                    let _ = win.hide();
                } else if let Some((kind, profile, id)) = scope(&label) {
                    api.prevent_close();
                    let a = win.app_handle().clone();
                    if kind == "category" {
                        let _ = dismiss(&a);
                        refresh(&a);
                    } else {
                        let request =
                            json!({"op":"show","profile":profile,"target":id,"flag":false});
                        tauri::async_runtime::spawn(async move {
                            if ipc(&a, request).await.is_ok() {
                                let _ = sync(&a, false).await;
                            }
                        });
                    }
                }
            }
        })
        .setup(|app| {
            use global_hotkey::{
                hotkey::{Code, HotKey, Modifiers},
                GlobalHotKeyEvent, GlobalHotKeyManager, HotKeyState,
            };
            use tauri::{
                menu::{Menu, MenuItem},
                tray::TrayIconBuilder,
            };
            let root = std::env::temp_dir().join(format!(
                "NexSpace-Floating-{}-{}",
                std::process::id(),
                std::time::SystemTime::now()
                    .duration_since(std::time::UNIX_EPOCH)?
                    .as_nanos()
            ));
            std::fs::create_dir(&root)?;
            let diagnostic = std::env::args().any(|a| {
                a.starts_with("--floating-cycles=")
                    || matches!(a.as_str(), "--verify-floating-ipc" | "--floating-lifecycle")
            });
            let webview_data = if diagnostic {
                root.join("webview")
            } else {
                std::env::current_exe()?
                    .parent()
                    .ok_or("missing-app-directory")?
                    .join("pilot-data/webview")
            };
            app.manage(Runtime {
                webview_data,
                root,
                ..Default::default()
            });
            let restore =
                MenuItem::with_id(app, "restore", "Recuperar ventanas", true, None::<&str>)?;
            let quit = MenuItem::with_id(
                app,
                "quit",
                "Cerrar NexSpace experimental",
                true,
                None::<&str>,
            )?;
            let menu = Menu::with_items(app, &[&restore, &quit])?;
            let mut tray = TrayIconBuilder::new()
                .menu(&menu)
                .tooltip("NexSpace experimental");
            if let Some(icon) = app.default_window_icon() {
                tray = tray.icon(icon.clone())
            }
            tray.on_menu_event(|a, e| {
                if e.id().as_ref() == "restore" {
                    recover(a)
                } else if e.id().as_ref() == "quit" {
                    a.exit(0)
                }
            })
            .build(app)?;
            let hot = HotKey::new(
                Some(Modifiers::CONTROL | Modifiers::SHIFT | Modifiers::ALT),
                Code::Home,
            );
            if let Ok(m) = GlobalHotKeyManager::new() {
                if m.register(hot).is_ok() {
                    let a = app.handle().clone();
                    GlobalHotKeyEvent::set_event_handler(Some(move |e: GlobalHotKeyEvent| {
                        if e.id == hot.id() && e.state == HotKeyState::Pressed {
                            recover(&a)
                        }
                    }));
                    HOTKEY.with(|slot| *slot.borrow_mut() = Some(m));
                    *app.state::<Runtime>().hotkey.lock().unwrap() = true;
                }
            }
            let monitor = placement::primary(app.handle())?;
            let work = placement::area(&monitor);
            let scale = monitor.scale_factor();
            if let Some(count) = std::env::args()
                .find_map(|s| {
                    s.strip_prefix("--floating-cycles=")
                        .and_then(|v| v.parse::<u32>().ok())
                })
                .filter(|n| matches!(n, 100 | 300))
            {
                window(
                    app.handle(),
                    "organize",
                    "NexSpace · ensayo",
                    centered(work, 450. * scale, 420. * scale),
                )?;
                let a = app.handle().clone();
                tauri::async_runtime::spawn(async move {
                    let result = cycles(&a, count).await;
                    let _ = std::fs::write(
                        a.state::<Runtime>().root.join("cycles.result"),
                        format!("{:?}", result),
                    );
                    a.exit(if result.is_ok() { 0 } else { 1 });
                });
                return Ok(());
            }
            window(
                app.handle(),
                "dock",
                "NexDock",
                Rect::new(
                    work.x + work.width / 2. - 160. * scale,
                    work.y + work.height - 87. * scale,
                    320. * scale,
                    70. * scale,
                )
                .fit(work),
            )?;
            let a = app.handle().clone();
            tauri::async_runtime::spawn(async move {
                if std::env::args().any(|s| s == "--verify-floating-ipc") {
                    let result = a.state::<Core>().verify_disconnect().await;
                    let _ = std::fs::write(
                        a.state::<Runtime>().root.join("ipc.result"),
                        format!("{result:?}"),
                    );
                    a.exit(if result.is_ok() { 0 } else { 1 });
                    return;
                }
                if ipc(&a, json!({"op":"startup"})).await.is_ok() {
                    let _ = sync(&a, false).await;
                }
                if std::env::args().any(|s| s == "--floating-lifecycle") {
                    let result = lifecycle(&a).await;
                    let _ = std::fs::write(
                        a.state::<Runtime>().root.join("lifecycle.result"),
                        format!("{result:?}"),
                    );
                    a.exit(if result.is_ok() { 0 } else { 1 });
                    return;
                }
                watch_displays(a).await;
            });
            Ok(())
        })
        .run(tauri::generate_context!())
        .expect("NexSpace floating host");
}

async fn cycles(app: &AppHandle, count: u32) -> Result<(), String> {
    use std::io::Write;
    let data = ipc(app, json!({"op":"manifest"})).await?;
    let g = &data["groups"][0];
    let label = group_label(ident(&data["profile"])?, ident(&g["id"])?);
    let root = app.state::<Runtime>().root.clone();
    let mut log = std::fs::File::create(root.join("cycles.tsv")).map_err(err)?;
    let ready = root.join(format!("{label}.ready"));
    let monitor = placement::named(
        app,
        g["placement"]["monitorKey"].as_str().unwrap_or_default(),
    )?;
    let work = placement::area(&monitor);
    let rect = saved_frame(g, work, monitor.scale_factor()).fit(work);
    for i in 1..=count {
        let last = std::fs::read_to_string(&ready).ok();
        let w = create(app, &label, g["name"].as_str().unwrap_or("NexSpace"), rect).await?;
        let deadline = std::time::Instant::now() + Duration::from_secs(8);
        loop {
            if std::fs::read_to_string(&ready)
                .ok()
                .is_some_and(|v| Some(v) != last)
            {
                break;
            }
            if std::time::Instant::now() > deadline {
                return Err("render-timeout".into());
            }
            tokio::time::sleep(Duration::from_millis(20)).await;
        }
        tokio::time::sleep(Duration::from_millis(100)).await;
        w.destroy().map_err(err)?;
        tokio::time::sleep(Duration::from_millis(100)).await;
        writeln!(log, "group\t{i}").map_err(err)?;
    }
    std::fs::write(root.join("settling"), "1").map_err(err)?;
    let idle = std::env::args()
        .find_map(|arg| arg.strip_prefix("--floating-idle=")?.parse::<u64>().ok())
        .unwrap_or(10)
        .clamp(1, 180);
    tokio::time::sleep(Duration::from_secs(idle)).await;
    Ok(())
}

async fn lifecycle(app: &AppHandle) -> Result<(), String> {
    if std::env::var_os("NEXSPACE_FLOATING_TEST_DIR").is_none() {
        return Err("isolated-library-required".into());
    }
    let initial = ipc(app, json!({"op":"manifest"})).await?;
    let profile = ident(&initial["profile"])?;
    let category = ident(&initial["categories"][0]["id"])?;
    let alternate = ipc(
        app,
        json!({"op":"createProfile","profile":profile,"name":"Prueba de ciclo"}),
    )
    .await?;
    let alternate = ident(&alternate)?;
    let dock = app.get_webview_window("dock").ok_or("dock-missing")?;
    for _ in 0..20 {
        surface(
            dock.clone(),
            "category".into(),
            Some(profile.into()),
            Some(category.into()),
            Some(118.),
        )
        .await?;
        if app
            .get_webview_window(&format!("category_{profile}_{category}"))
            .is_none()
        {
            return Err("popover-missing".into());
        }
        ipc(
            app,
            json!({"op":"switchProfile","profile":profile,"target":alternate}),
        )
        .await?;
        sync(app, false).await?;
        if app.webview_windows().keys().any(|key| scope(key).is_some()) {
            return Err("old-profile-window".into());
        }
        ipc(
            app,
            json!({"op":"switchProfile","profile":alternate,"target":profile}),
        )
        .await?;
        sync(app, false).await?;
    }
    app.state::<Core>().verify_disconnect().await?;
    dock.hide().map_err(err)?;
    recover(app);
    if !dock.is_visible().map_err(err)? {
        return Err("dock-not-restored".into());
    }
    Ok(())
}
