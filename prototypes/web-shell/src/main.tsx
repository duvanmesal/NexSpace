import React, { useState, useEffect, useRef } from "react";
import { createRoot } from "react-dom/client";
import {
  ChevronDown,
  Gamepad2,
  LayoutGrid,
  Folder,
  Rows3,
  SunMoon,
  Power,
  Grip,
  Ellipsis,
  Pin,
} from "lucide-react";
import { send, type Group, type Action } from "./host";
import "./style.css";

const library = {
  files: {
    label: "Archivos",
    names: [
      "Documentos",
      "Proyectos",
      "Archivo",
      "Imágenes",
      "Música",
      "Notas",
    ],
    art: ["document", "folder", "folder", "picture", "music", "notes"],
  },
  games: {
    label: "Juegos",
    names: ["Órbita", "Bosque", "Circuito", "Arcade", "Horizonte", "Ajedrez"],
    art: ["orbit", "forest", "race", "arcade", "horizon", "chess"],
  },
  apps: {
    label: "Aplicaciones",
    names: [
      "Cámara",
      "Calendario",
      "Correo",
      "Calculadora",
      "Dibujo",
      "Libros",
    ],
    art: ["camera", "calendar", "mail", "calc", "paint", "books"],
  },
};
const groups = Object.keys(library) as Group[];
const query = new URLSearchParams(location.search);
const role = query.get("role") || "files";
const group =
  groups.find((g) => g === (role === "popup" ? query.get("group") : role)) ||
  "files";
const categoryIcon = { files: Folder, games: Gamepad2, apps: LayoutGrid };
function Art({ name }: { name: string }) {
  return (
    <img
      className="art"
      src={"./art/" + name + ".png"}
      alt=""
      draggable="false"
    />
  );
}
function Mark({ group: g }: { group: Group }) {
  const Icon = categoryIcon[g];
  return g === "files" ? <Art name="folder" /> : <Icon aria-hidden="true" />;
}
function Shell() {
  const [collapsed, collapse] = useState(false),
    [menu, setMenu] = useState(false);
  const handle = useRef<{ x: number; y: number } | null>(null);
  const drag = useRef<{ x: number; y: number; w: number; h: number } | null>(
    null,
  );
  const [list, setList] = useState(localStorage.getItem("view") === "list");
  const [message, setMessage] = useState(""),
    [active, setActive] = useState("");
  const run = (cmd: Action) => {
    void send(cmd).catch(() => setMessage("No se pudo completar la acción."));
  };
  useEffect(() => {
    const theme = () => {
      document.documentElement.dataset.theme =
        localStorage.getItem("theme") || "dark";
    };
    const update = (e: StorageEvent) => {
      theme();
      if (e.key === "view") setList(e.newValue === "list");
      if (e.key === "active") setActive(e.newValue || "");
    };
    theme();
    window.addEventListener("storage", update);
    const onMessage = (e: MessageEvent) => {
      if (/^#[0-9a-f]{6}$/i.test(e.data?.accent || ""))
        document.documentElement.style.setProperty("--accent", e.data.accent);
      if (e.data?.error) setMessage("No se pudo abrir la referencia.");
    };
    window.chrome?.webview?.addEventListener("message", onMessage);
    const keyboard = (e: KeyboardEvent) => {
      if (e.ctrlKey && e.shiftKey && e.key === "Home") {
        e.preventDefault();
        run({ kind: "recover" });
      }
      if (e.key === "Escape") {
        setMenu(false);
        if (role === "popup") run({ kind: "dismiss" });
      }
    };
    window.addEventListener("keydown", keyboard);
    if (role === "popup") {
      localStorage.setItem("active", group);
      window.addEventListener(
        "pagehide",
        () => localStorage.removeItem("active"),
        { once: true },
      );
    }
    requestAnimationFrame(() =>
      requestAnimationFrame(() => run({ kind: "ready" })),
    );
    const timers: number[] = [];
    if (query.get("exercise") === "1") {
      const at = (ms: number, fn: () => void) =>
        timers.push(window.setTimeout(fn, ms));
      if (role === "games")
        at(7500, () => {
          collapse(true);
          run({ kind: "collapse", collapsed: true });
        });
      if (role === "apps")
        at(7500, () => run({ kind: "size", width: 434, height: 308 }));
      if (role === "dock") {
        at(8000, () => {
          setActive("files");
          run({ kind: "popover", group: "files" });
        });
        at(14000, changeTheme);
        at(16500, changeTheme);
        for (let i = 0; i < 8; i++) {
          at(18000 + 2200 * i, () => run({ kind: "dismiss" }));
          at(19100 + 2200 * i, () =>
            run({ kind: "popover", group: groups[i % 3] }),
          );
        }
        at(36000, () => {
          setActive("");
          run({ kind: "dismiss" });
        });
      }
    }
    return () => {
      timers.forEach(clearTimeout);
      window.removeEventListener("storage", update);
      window.removeEventListener("keydown", keyboard);
    };
  }, []);
  const changeTheme = () => {
    const v =
      document.documentElement.dataset.theme === "dark" ? "light" : "dark";
    localStorage.setItem("theme", v);
    document.documentElement.dataset.theme = v;
  };
  const open = (i: number) =>
    run({ kind: "open", id: groups.indexOf(group) * 6 + i });
  const tools = (
    label: string,
    icon: React.ReactNode,
    fn: () => void,
    selected = false,
  ) => (
    <button
      type="button"
      key={label}
      className={selected ? "selected" : ""}
      aria-label={label}
      title={label}
      onClick={fn}
    >
      {icon}
    </button>
  );
  const toggle = () => {
    collapse(!collapsed);
    setMenu(false);
    run({ kind: "collapse", collapsed: !collapsed });
  };
  if (role === "dock")
    return (
      <nav
        className="dock glass flex items-center justify-center"
        aria-label="NexDock"
      >
        {tools("Recuperar ventanas", <LayoutGrid className="home-mark" />, () =>
          run({ kind: "recover" }),
        )}
        {groups.map((g) =>
          tools(
            library[g].label,
            <Mark group={g} />,
            () => {
              setActive(active === g ? "" : g);
              run(
                active === g
                  ? { kind: "dismiss" }
                  : { kind: "popover", group: g },
              );
            },
            active === g,
          ),
        )}
        <i />
        {tools(
          list ? "Usar cuadrícula" : "Usar lista",
          <Rows3 />,
          () => {
            localStorage.setItem("view", list ? "grid" : "list");
            setList(!list);
          },
          list,
        )}
        {tools("Alternar tema", <SunMoon />, changeTheme)}
        {tools("Salir de la prueba", <Power />, () => run({ kind: "close" }))}
      </nav>
    );
  const data = library[group],
    popup = role === "popup";
  return (
    <main
      className={
        (popup ? "popup " : "panel ") +
        (collapsed ? "collapsed " : "") +
        "glass"
      }
    >
      <header
        onPointerDown={(e) => {
          if (
            !popup &&
            e.button === 0 &&
            !(e.target as Element).closest("button")
          ) {
            e.currentTarget.setPointerCapture(e.pointerId);
            handle.current = { x: e.clientX, y: e.clientY };
          }
        }}
        onPointerMove={(e) => {
          if (handle.current)
            run({
              kind: "move",
              x: Math.round(e.screenX - handle.current.x),
              y: Math.round(e.screenY - handle.current.y),
            });
        }}
        onLostPointerCapture={() => {
          handle.current = null;
        }}
      >
        <Mark group={group} />
        <h1>{data.label}</h1>
        {popup ? (
          tools("Cerrar panel", <Pin />, () => run({ kind: "dismiss" }))
        ) : (
          <>
            {tools("Opciones", <Ellipsis />, () => setMenu(!menu))}
            {tools(
              collapsed ? "Expandir" : "Contraer",
              <ChevronDown className={collapsed ? "turned" : ""} />,
              toggle,
            )}
          </>
        )}
      </header>
      {menu && (
        <aside>
          {tools("Recuperar ventanas", <>Restablecer posición</>, () => {
            run({ kind: "recover" });
            setMenu(false);
          })}
        </aside>
      )}
      {!collapsed && (
        <section
          aria-label={data.label}
          className={popup || list ? "rows" : "tiles"}
        >
          {data.names.map((name, i) => (
            <button
              key={name}
              title={"Abrir ficha de " + name}
              onClick={() => open(i)}
            >
              <Art name={data.art[i]} />
              <span>{name}</span>
            </button>
          ))}
        </section>
      )}
      {!popup && !collapsed && (
        <button
          className="grip"
          aria-label="Redimensionar"
          onPointerDown={(e) => {
            e.currentTarget.setPointerCapture(e.pointerId);
            drag.current = {
              x: e.screenX,
              y: e.screenY,
              w: innerWidth,
              h: innerHeight,
            };
          }}
          onPointerMove={(e) => {
            const d = drag.current;
            if (d)
              run({
                kind: "size",
                width: Math.round(d.w + e.screenX - d.x),
                height: Math.round(d.h + e.screenY - d.y),
              });
          }}
          onLostPointerCapture={() => {
            drag.current = null;
          }}
          onKeyDown={(e) => {
            const deltas: Record<string, [number, number]> = {
              ArrowRight: [12, 0],
              ArrowLeft: [-12, 0],
              ArrowDown: [0, 12],
              ArrowUp: [0, -12],
            };
            const d = deltas[e.key];
            if (d) {
              e.preventDefault();
              run({
                kind: "size",
                width: innerWidth + d[0],
                height: innerHeight + d[1],
              });
            }
          }}
        >
          <Grip size={13} />
        </button>
      )}
      {message && <output role="alert">{message}</output>}
    </main>
  );
}
createRoot(document.getElementById("root")!).render(<Shell />);
