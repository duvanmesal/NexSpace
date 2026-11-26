use crate::err;
use std::ffi::c_void;
use tauri::{window::Monitor, AppHandle, PhysicalPosition, PhysicalSize, WebviewWindow};

#[derive(Clone, Copy, Debug, PartialEq)]
pub struct Rect {
    pub x: f64,
    pub y: f64,
    pub width: f64,
    pub height: f64,
}

impl Rect {
    pub fn new(x: f64, y: f64, width: f64, height: f64) -> Self {
        Self {
            x,
            y,
            width,
            height,
        }
    }

    pub fn fit(self, work: Self) -> Self {
        let width = self.width.clamp(1.0, work.width.max(1.0));
        let height = self.height.clamp(1.0, work.height.max(1.0));
        Self::new(
            self.x.clamp(work.x, work.x + work.width.max(1.0) - width),
            self.y.clamp(work.y, work.y + work.height.max(1.0) - height),
            width,
            height,
        )
    }

    pub fn position(self) -> PhysicalPosition<i32> {
        PhysicalPosition::new(self.x.round() as i32, self.y.round() as i32)
    }

    pub fn size(self) -> PhysicalSize<u32> {
        PhysicalSize::new(self.width.round() as u32, self.height.round() as u32)
    }
}

pub fn area(monitor: &Monitor) -> Rect {
    let work = monitor.work_area();
    Rect::new(
        work.position.x.into(),
        work.position.y.into(),
        work.size.width.into(),
        work.size.height.into(),
    )
}

pub fn primary(app: &AppHandle) -> Result<Monitor, String> {
    app.primary_monitor()
        .map_err(err)?
        .ok_or_else(|| "monitor-unavailable".into())
}

pub fn named(app: &AppHandle, key: &str) -> Result<Monitor, String> {
    if key != "primary" && !key.is_empty() {
        if let Some(monitor) = app
            .available_monitors()
            .map_err(err)?
            .into_iter()
            .find(|m| m.name().is_some_and(|name| name == key))
        {
            return Ok(monitor);
        }
    }
    primary(app)
}

pub fn frame(window: &WebviewWindow) -> Result<Rect, String> {
    let p = window.outer_position().map_err(err)?;
    let s = window.inner_size().map_err(err)?;
    Ok(Rect::new(
        p.x.into(),
        p.y.into(),
        s.width.into(),
        s.height.into(),
    ))
}

pub fn place(window: &WebviewWindow, rect: Rect) -> Result<(), String> {
    if window.outer_position().map_err(err)? != rect.position() {
        window.set_position(rect.position()).map_err(err)?;
    }
    if window.inner_size().map_err(err)? != rect.size() {
        window.set_size(rect.size()).map_err(err)?;
    }
    Ok(())
}

pub async fn reveal_displaced(window: &WebviewWindow, rect: Rect) -> Result<(), String> {
    let (send, receive) = tokio::sync::oneshot::channel();
    let surface = window.clone();
    window
        .run_on_main_thread(move || {
            let result = surface
                .hwnd()
                .map_err(err)
                .and_then(|handle| show_without_activation(handle.0.cast(), rect));
            let _ = send.send(result);
        })
        .map_err(err)?;
    receive.await.map_err(err)?
}

fn show_without_activation(hwnd: *mut c_void, rect: Rect) -> Result<(), String> {
    const SW_SHOWNOACTIVATE: i32 = 4;
    const SWP_NOACTIVATE: u32 = 0x0010;
    const SWP_SHOWWINDOW: u32 = 0x0040;
    const SWP_NOOWNERZORDER: u32 = 0x0200;
    let position = rect.position();
    let size = rect.size();
    unsafe {
        if IsIconic(hwnd) != 0 {
            ShowWindow(hwnd, SW_SHOWNOACTIVATE);
        }
        // HWND_TOP raises this window in its normal z-order without activating it.
        if SetWindowPos(hwnd, std::ptr::null_mut(), position.x, position.y, size.width as i32,
                        size.height as i32, SWP_NOACTIVATE | SWP_SHOWWINDOW | SWP_NOOWNERZORDER) == 0 {
            return Err(std::io::Error::last_os_error().to_string());
        }
        InvalidateRect(hwnd, std::ptr::null(), 0);
        if IsIconic(hwnd) != 0 || IsWindowVisible(hwnd) == 0 {
            return Err("displaced-window-not-visible".into());
        }
    }
    Ok(())
}

pub fn foreground_handle() -> usize {
    unsafe { GetForegroundWindow() as usize }
}

pub fn observe_native(hwnd: usize) -> Option<([i32; 4], bool, bool)> {
    let hwnd = hwnd as *mut c_void;
    let mut bounds = [0i32; 4];
    unsafe {
        (GetWindowRect(hwnd, bounds.as_mut_ptr()) != 0)
            .then(|| (bounds, IsWindowVisible(hwnd) != 0, IsIconic(hwnd) != 0))
    }
}

pub fn cursor() -> Result<(f64, f64), String> {
    let mut point = [0i32; 2];
    // The native cursor and monitor rectangles share the virtual desktop's physical coordinate space.
    if unsafe { GetPhysicalCursorPos(point.as_mut_ptr()) } == 0 {
        return Err("cursor-unavailable".into());
    }
    Ok((point[0].into(), point[1].into()))
}

#[link(name = "user32")]
extern "system" {
    fn GetPhysicalCursorPos(point: *mut i32) -> i32;
    fn GetForegroundWindow() -> *mut c_void;
    fn IsIconic(hwnd: *mut c_void) -> i32;
    fn IsWindowVisible(hwnd: *mut c_void) -> i32;
    fn GetWindowRect(hwnd: *mut c_void, rect: *mut i32) -> i32;
    fn InvalidateRect(hwnd: *mut c_void, rect: *const i32, erase: i32) -> i32;
    fn ShowWindow(hwnd: *mut c_void, cmd: i32) -> i32;
    fn SetWindowPos(hwnd: *mut c_void, after: *mut c_void, x: i32, y: i32, width: i32, height: i32, flags: u32) -> i32;
}

#[cfg(test)]
mod tests {
    use super::*;

    #[link(name = "user32")]
    extern "system" {
        fn CreateWindowExW(ex_style: u32, class: *const u16, name: *const u16, style: u32,
                          x: i32, y: i32, width: i32, height: i32, parent: *mut c_void,
                          menu: *mut c_void, instance: *mut c_void, param: *mut c_void) -> *mut c_void;
        fn DestroyWindow(hwnd: *mut c_void) -> i32;
    }

    struct TestWindow(*mut c_void);

    impl Drop for TestWindow {
        fn drop(&mut self) {
            unsafe { DestroyWindow(self.0) };
        }
    }

    #[test]
    fn reveals_a_displaced_win32_window_without_activating_it() {
        let null = std::ptr::null_mut();
        let class: Vec<u16> = "STATIC\0".encode_utf16().collect();
        let hwnd = unsafe {
            CreateWindowExW(0, class.as_ptr(), std::ptr::null(), 0x80000000,
                           20, 20, 150, 100, null, null, null, null)
        };
        assert!(!hwnd.is_null());
        let _window = TestWindow(hwnd);
        let before = foreground_handle();
        let destination = Rect::new(60., 70., 180., 110.);
        assert_eq!(observe_native(hwnd as usize).unwrap().1, false);
        show_without_activation(hwnd, destination).unwrap();
        assert_eq!(observe_native(hwnd as usize), Some(([60, 70, 240, 180], true, false)));
        assert_eq!(foreground_handle(), before);

        unsafe { ShowWindow(hwnd, 7) }; // SW_SHOWMINNOACTIVE, on this test window only.
        assert!(observe_native(hwnd as usize).unwrap().2);
        let before_restore = foreground_handle();
        show_without_activation(hwnd, destination).unwrap();
        assert_eq!(observe_native(hwnd as usize), Some(([60, 70, 240, 180], true, false)));
        assert_eq!(foreground_handle(), before_restore);
    }

    #[test]
    fn fits_on_secondary_left_of_primary() {
        let work = Rect::new(-2560., 40., 2560., 1400.);
        assert_eq!(
            Rect::new(-2800., -50., 600., 400.).fit(work),
            Rect::new(-2560., 40., 600., 400.)
        );
    }

    #[test]
    fn recovers_after_monitor_loss_and_smaller_resolution() {
        let work = Rect::new(0., 0., 1280., 700.);
        assert_eq!(Rect::new(2000., -200., 1300., 1000.).fit(work), work);
    }
}
