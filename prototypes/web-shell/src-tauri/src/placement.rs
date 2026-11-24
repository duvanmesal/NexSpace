use crate::err;
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
}

#[cfg(test)]
mod tests {
    use super::*;

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
