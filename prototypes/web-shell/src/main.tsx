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
import { invoke } from "@tauri-apps/api/core";
import "./style.css";
interface Entry {
  id: string;
  name: string;
  exists: boolean;
  icon: string | null;
}
interface Snapshot {
  items: Entry[];
  isCollapsed: boolean;
}
type LibraryAction =
  | { op: "load" | "list" }
  | { op: "open"; item: string }
  | { op: "rename"; item: string; name: string }
  | { op: "reorder"; item: string; offset: number };
function request<T>(action: LibraryAction) {
  return invoke<T>("core_request", { request: action });
}
function describe(code: unknown) {
  const errors: Record<string, string> = {
    missing: "Ya no se encuentra el archivo.",
    "invalid-name": "Introduce un nombre válido.",
    "not-movable": "La referencia ya está en ese extremo.",
    cancelled: "Operación cancelada.",
    "unknown-reference": "La referencia ya no está disponible.",
  };
  return (
    errors[String(code)] ||
    "NexCore no pudo completar la operación. Vuelve a cargar Archivos."
  );
}

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
  const connected =
    query.get("pilot") === "1" && group === "files" && role !== "dock";
  const [items, setItems] = useState<Entry[]>([]);
  const [loading, setLoading] = useState(connected);
  const [selected, select] = useState("");
  const [name, setName] = useState("");
  async function refresh(initial = false) {
    setLoading(true);
    try {
      if (initial && role === "files") await send({ kind: "restore" });
      const data = await request<Snapshot>({ op: "load" });
      setItems(data.items);
      if (role === "files") collapse(data.isCollapsed);
      setMessage("");
      return true;
    } catch (e) {
      setMessage(describe(e));
      return false;
    } finally {
      setLoading(false);
    }
  }
  async function edit(action: LibraryAction) {
    try {
      await request(action);
      await refresh();
      select("");
    } catch (e) {
      setMessage(describe(e));
    }
  }
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
    const ready = () =>
      requestAnimationFrame(() =>
        requestAnimationFrame(() => run({ kind: "ready" })),
      );
    if (connected)
      void refresh(true).then((ok) => {
        if (ok) ready();
      });
    else ready();
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
    setMenu(false);
    void send({ kind: "collapse", collapsed: !collapsed })
      .then(() => collapse(!collapsed))
      .catch((e) => setMessage(describe(e)));
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
          if (handle.current && connected) run({ kind: "save" });
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
          {connected && (
            <label>
              Editar referencia{" "}
              <select
                value={selected}
                onChange={(e) => {
                  const item = items.find((i) => i.id === e.target.value);
                  if (item) {
                    select(item.id);
                    setName(item.name);
                    setMenu(false);
                  }
                }}
              >
                <option value="">Elegir…</option>
                {items.map((i) => (
                  <option key={i.id} value={i.id}>
                    {i.name}
                  </option>
                ))}
              </select>
            </label>
          )}
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
          {connected &&
            items.map((entry) => (
              <button
                key={entry.id}
                title={entry.name}
                onClick={() => void edit({ op: "open", item: entry.id })}
                onContextMenu={(e) => {
                  if (role === "files") {
                    e.preventDefault();
                    select(entry.id);
                    setName(entry.name);
                  }
                }}
              >
                {entry.icon ? (
                  <img className="art" src={entry.icon} alt="" />
                ) : (
                  <Art name="document" />
                )}
                <span>
                  {entry.name}
                  {!entry.exists && " · Sin archivo"}
                </span>
              </button>
            ))}
          {connected && loading && (
            <span role="status">Cargando Archivos…</span>
          )}
          {!connected &&
            data.names.map((name, i) => (
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
            if (drag.current && connected) run({ kind: "save" });
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
              void send({
                kind: "size",
                width: innerWidth + d[0],
                height: innerHeight + d[1],
              })
                .then(() => (connected ? send({ kind: "save" }) : undefined))
                .catch((e) => setMessage(describe(e)));
            }
          }}
        >
          <Grip size={13} />
        </button>
      )}
      {selected && (
        <aside aria-label="Editar referencia">
          <form
            onSubmit={(e) => {
              e.preventDefault();
              void edit({ op: "rename", item: selected, name });
            }}
          >
            <label>
              Nombre{" "}
              <input
                value={name}
                maxLength={260}
                onChange={(e) => setName(e.target.value)}
                autoFocus
              />
            </label>
            <button type="submit">Guardar</button>
          </form>
          <button
            onClick={() =>
              void edit({ op: "reorder", item: selected, offset: -1 })
            }
          >
            Mover antes
          </button>
          <button
            onClick={() =>
              void edit({ op: "reorder", item: selected, offset: 1 })
            }
          >
            Mover después
          </button>
          <button onClick={() => select("")}>Cerrar</button>
        </aside>
      )}
      {connected && loading && role === "files" && (
        <button className="cancel" onClick={() => void invoke("cancel_core")}>
          Cancelar carga
        </button>
      )}
      {message && (
        <output role="alert">
          {message}{" "}
          {connected && (
            <button onClick={() => void refresh(true)}>Recargar</button>
          )}
        </output>
      )}
    </main>
  );
}
createRoot(document.getElementById("root")!).render(<Shell />);
