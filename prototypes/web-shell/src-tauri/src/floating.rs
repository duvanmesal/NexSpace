use crate::{core::Core, err};
use serde::{Deserialize, Serialize};
use serde_json::{json, Value};
use std::{
    path::PathBuf,
    sync::{
        atomic::{AtomicU64, Ordering},
        Mutex,
    },
    time::Duration,
};
use tauri::{
    AppHandle, DragDropEvent, LogicalPosition, LogicalSize, Manager, WebviewEvent, WebviewUrl,
    WebviewWindow, WebviewWindowBuilder,
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
fn area(app: &AppHandle) -> Result<(f64, f64, f64, f64), String> {
    let m = app
        .primary_monitor()
        .map_err(err)?
        .ok_or("monitor-unavailable")?;
    let a = m.work_area();
    let s = m.scale_factor();
    Ok((
        a.position.x as f64 / s,
        a.position.y as f64 / s,
        a.size.width as f64 / s,
        a.size.height as f64 / s,
    ))
}
fn bounds(app: &AppHandle, x: f64, y: f64, w: f64, h: f64) -> Result<(f64, f64, f64, f64), String> {
    let (l, t, aw, ah) = area(app)?;
    let w = w.min(aw);
    let h = h.min(ah);
    Ok((x.clamp(l, l + aw - w), y.clamp(t, t + ah - h), w, h))
}
fn place(w: &WebviewWindow, r: (f64, f64, f64, f64)) -> Result<(), String> {
    w.set_size(LogicalSize::new(r.2, r.3)).map_err(err)?;
    w.set_position(LogicalPosition::new(r.0, r.1)).map_err(err)
}
fn window(
    app: &AppHandle,
    label: &str,
    title: &str,
    r: (f64, f64, f64, f64),
) -> Result<WebviewWindow, String> {
    WebviewWindowBuilder::new(
        app,
        label,
        WebviewUrl::App(format!("index.html?floating={label}").into()),
    )
    .title(title)
    .decorations(false)
    .transparent(true)
    .shadow(false)
    .resizable(false)
    .position(r.0, r.1)
    .inner_size(r.2, r.3)
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
    })
}
async fn create(
    app: &AppHandle,
    label: &str,
    title: &str,
    r: (f64, f64, f64, f64),
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
    let (l, t, aw, ah) = area(app)?;
    let vertical = data["vertical"] == true;
    let length = (100. + categories.len() as f64 * 60.)
        .clamp(148., if vertical { ah.max(148.) } else { aw.max(148.) });
    let dock = if vertical {
        (l + 12., t + (ah - length) / 2., 70., length)
    } else {
        (l + (aw - length) / 2., t + ah - 87., length, 70.)
    };
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
        let h = if g["isCollapsed"] == true {
            52.
        } else {
            p["height"].as_f64().unwrap_or(272.)
        };
        let rect = bounds(
            app,
            p["x"].as_f64().unwrap_or(l),
            p["y"].as_f64().unwrap_or(t),
            p["width"].as_f64().unwrap_or(386.),
            h,
        )?;
        if recover && (p["x"].as_f64() != Some(rect.0) || p["y"].as_f64() != Some(rect.1)) {
            let mut corrected = p.clone();
            corrected["x"] = json!(rect.0.round() as i32);
            corrected["y"] = json!(rect.1.round() as i32);
            corrected["monitorKey"] = json!("primary");
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
    let result = ipc(app, serde_json::to_value(request).map_err(err)?).await?;
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
        if let Err(e) = ipc(
            app,
            json!({"op":"add","profile":p,"target":id,"category":kind=="category","path":path}),
        )
        .await
        {
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
    x: f64,
    y: f64,
    width: f64,
    height: f64,
    save: bool,
) -> Result<(), String> {
    let (kind, p, id) = scope(window.label()).ok_or("invalid-window")?;
    if kind != "group" {
        return Err("invalid-window".into());
    }
    let app = window.app_handle();
    let rect = bounds(app, x, y, width.max(320.), height.max(52.))?;
    if save {
        let manifest = ipc(app, json!({"op":"manifest"})).await?;
        let g = manifest["groups"]
            .as_array()
            .and_then(|groups| groups.iter().find(|g| g["id"] == id))
            .ok_or("not-found")?;
        let h = if g["isCollapsed"] == true {
            g["placement"]["height"].clone()
        } else {
            json!(rect.3.round() as i32)
        };
        ipc(app,json!({"op":"placement","profile":p,"target":id,"placement":{"monitorKey":"primary","x":rect.0.round() as i32,"y":rect.1.round() as i32,"width":rect.2.round() as i32,"height":h}})).await?;
        sync(app, false).await?;
    } else {
        place(&window, rect)?;
    }
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
                let (l, t, aw, ah) = area(app)?;
                create(
                    app,
                    "organize",
                    "Organizar NexSpace",
                    bounds(app, l + (aw - 450.) / 2., t + (ah - 420.) / 2., 450., 420.)?,
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
                let pos = window
                    .outer_position()
                    .map_err(err)?
                    .to_logical::<f64>(window.scale_factor().map_err(err)?);
                let a = anchor.unwrap_or(36.);
                let (x, y) = if data["vertical"] == true {
                    (pos.x + 74., pos.y + a - 135.)
                } else {
                    (pos.x + a - 128., pos.y - 276.)
                };
                create(
                    app,
                    &format!("category_{p}_{id}"),
                    cat["name"].as_str().unwrap_or("Categoría"),
                    bounds(app, x, y, 256., 272.)?,
                )
                .await?;
                *state.active.lock().map_err(err)? = Some(id);
            }
            refresh(app);
        }
        "dismiss" if window.label().starts_with("category_") => {
            dismiss(app)?;
            refresh(app);
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
        if let Ok((l, t, width, height)) = area(app) {
            let _ = place(&w, (l + (width - 320.) / 2., t + height - 87., 320., 70.));
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
pub fn run() {
    tauri::Builder::default()
        .manage(Core::floating())
        .invoke_handler(tauri::generate_handler![library, pick, frame, surface])
        .on_window_event(|win, event| {
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
        .on_webview_event(|view, event| {
            if let WebviewEvent::DragDrop(DragDropEvent::Drop { paths, .. }) = event {
                let label = view.label().to_owned();
                if scope(&label).is_none() {
                    return;
                }
                let app = view.app_handle().clone();
                let paths = paths.clone();
                tauri::async_runtime::spawn(async move {
                    if let Err(e) = add_paths(&app, &label, paths).await {
                        if let Some(w) = app.get_webview_window(&label) {
                            let _ = w.eval(format!(
                                "window.dispatchEvent(new CustomEvent('drop-error',{{detail:{}}}))",
                                json!(e)
                            ));
                        }
                    }
                });
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
            let diagnostic = std::env::args()
                .any(|a| a.starts_with("--floating-cycles=") || a == "--verify-floating-ipc");
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
            let (l, t, aw, ah) = area(app.handle())?;
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
                    (l + (aw - 450.) / 2., t + (ah - 420.) / 2., 450., 420.),
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
                (l + (aw - 320.) / 2., t + ah - 87., 320., 70.),
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
    let p = &g["placement"];
    let rect = bounds(
        app,
        p["x"].as_f64().unwrap_or(24.),
        p["y"].as_f64().unwrap_or(24.),
        p["width"].as_f64().unwrap_or(386.),
        p["height"].as_f64().unwrap_or(272.),
    )?;
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
    tokio::time::sleep(Duration::from_secs(10)).await;
    Ok(())
}
