import React, { useState, useEffect, useLayoutEffect, useRef } from "react";
import { invoke } from "@tauri-apps/api/core";
import {
  Folder,
  FileText,
  ChevronDown,
  Ellipsis,
  Grip,
  Settings2,
  Gamepad2,
  LayoutGrid,
  X,
  Plus,
} from "lucide-react";

type Row = {
  id: string;
  name: string;
  key: string;
  kind: string;
  status: string;
};
type Page = {
  multipleDisplays?: boolean;
  vertical: boolean;
  name: string;
  rows: Row[];
  art: Record<string, string | null>;
  total: number;
  list: boolean;
  collapsed: boolean;
  locked: boolean;
};
type Group = { id: string; name: string; shown: boolean };
type Category = {
  id: string;
  name: string;
  glyph: string;
  containerId: string | null;
};
type Manifest = {
  profile: string;
  profiles: Group[];
  groups: Group[];
  categories: Category[];
  vertical: boolean;
  selected: string | null;
  hotkey: boolean;
};
type Fields = {
  name?: string;
  offset?: number;
  target?: string;
  item?: string;
  link?: string | null;
  flag?: boolean;
};
type Form = {
  title: string;
  value?: string;
  confirm: string;
  apply: (value: string) => Promise<void>;
};
type PanelMenu =
  | { kind: "options"; trigger: HTMLButtonElement }
  | { kind: "entry"; trigger: HTMLButtonElement; row: Row };
const label = new URLSearchParams(location.search).get("floating") || "dock";
const [role, profile, target] = label.split("_");
const panel = role === "group" || role === "category";
const native = (op: string, extra: object = {}) =>
  invoke("surface", { op, ...extra });
const request = <T,>(op: string, fields: object) =>
  invoke<T>("library", { request: { op, ...fields } });
const messages: Record<string, string> = {
  missing: "El recurso ya no está en su ubicación.",
  "unknown-reference": "La referencia ya no está disponible.",
  "stale-profile": "Cambió el perfil. Recarga esta ventana.",
  disconnected: "Se perdió la conexión con NexCore.",
  timeout: "NexCore tardó demasiado en responder.",
  "duplicate-reference": "Esta referencia ya está en el grupo.",
  "invalid-workspace": "Comprueba el nombre, vínculo u orden de la categoría.",
};
function Button({
  name,
  action,
  children,
  selected = false,
  expanded,
}: {
  name: string;
  action: React.MouseEventHandler<HTMLButtonElement>;
  children: React.ReactNode;
  selected?: boolean;
  expanded?: boolean;
}) {
  return (
    <button
      type="button"
      className={selected ? "selected" : ""}
      title={name}
      aria-label={name}
      aria-expanded={expanded}
      onClick={action}
    >
      {children}
    </button>
  );
}
function Mark({ name }: { name: string }) {
  return /juego/i.test(name) ? (
    <Gamepad2 />
  ) : /aplic/i.test(name) ? (
    <LayoutGrid />
  ) : (
    <Folder />
  );
}
export function Floating() {
  const [data, setData] = useState<Manifest>();
  const [page, setPage] = useState<Page>();
  const [error, setError] = useState("");
  const [menu, setMenu] = useState<PanelMenu>();
  const menuSurface = useRef<HTMLElement>(null);
  const entry = menu?.kind === "entry" ? menu.row : undefined;
  const [offset, setOffset] = useState(0);
  const [form, setForm] = useState<Form>();
  const [value, setValue] = useState("");
  const [tab, setTab] = useState("Contenedores");
  const [link, setLink] = useState("");
  const [pending, setPending] = useState(false);
  const seq = useRef(0);
  const ready = useRef(false);
  const [retry, setRetry] = useState(0);
  const active = panel ? profile : data?.profile;
  function fail(e: unknown) {
    setError(
      messages[String(e)] ||
        "No se pudo completar la operación (" + String(e) + ").",
    );
  }
  function run(fn: () => Promise<unknown>) {
    void fn().catch(fail);
  }
  async function load() {
    const token = ++seq.current;
    try {
      if (panel) {
        const result = await request<Page>("page", {
          profile,
          target,
          category: role === "category",
          offset,
        });
        if (token !== seq.current) return;
        if (offset > 0 && !result.rows.length) {
          setOffset(Math.max(0, offset - 24));
          return;
        }
        setPage(result);
      } else {
        const result = await request<Manifest>("manifest", {});
        if (token !== seq.current) return;
        setData(result);
      }
      setError("");
      if (!ready.current) {
        ready.current = true;
        await native("ready");
      }
    } catch (e) {
      if (token === seq.current) fail(e);
    }
  }
  useEffect(() => {
    void load();
    const change = () => {
      void load();
    };
    const dropped = (e: Event) => fail((e as CustomEvent).detail);
    window.addEventListener("library-changed", change);
    window.addEventListener("drop-error", dropped);
    return () => {
      seq.current++;
      window.removeEventListener("library-changed", change);
      window.removeEventListener("drop-error", dropped);
    };
  }, [offset, retry]);
  async function act(op: string, fields: Fields = {}) {
    await request(op, {
      profile: active,
      target,
      category: role === "category",
      ...fields,
    });
    setError("");
  }
  function ask(f: Form) {
    setValue(f.value || "");
    setForm(f);
  }
  function rename(title: string, current: string, op: string, fields: Fields) {
    ask({
      title,
      value: current,
      confirm: "Guardar",
      apply: (name) => act(op, { ...fields, name }),
    });
  }
  function remove(title: string, op: string, fields: Fields) {
    ask({ title, confirm: "Quitar", apply: () => act(op, fields) });
  }
  function closeMenuAndRestoreFocus() {
    setMenu(undefined);
    if (menu?.trigger.isConnected) menu.trigger.focus({ preventScroll: true });
  }
  useLayoutEffect(() => {
    const surface = menuSurface.current;
    if (!menu || !surface) return;
    const dismissOutside = (event: Event) => {
      const target = event.target;
      if (target instanceof Node && !surface.contains(target) && !menu.trigger.contains(target)) {
        setMenu(undefined);
      }
    };
    const dismiss = () => setMenu(undefined);
    // Observe the initiating input without consuming the click or the new focus.
    document.addEventListener("pointerdown", dismissOutside, true);
    document.addEventListener("focusin", dismissOutside);
    window.addEventListener("blur", dismiss);
    window.addEventListener("panel-focus-lost", dismiss);
    surface.querySelector("button")?.focus({ preventScroll: true });
    return () => {
      document.removeEventListener("pointerdown", dismissOutside, true);
      document.removeEventListener("focusin", dismissOutside);
      window.removeEventListener("blur", dismiss);
      window.removeEventListener("panel-focus-lost", dismiss);
    };
  }, [menu]);
  function escape() {
    if (form) setForm(undefined);
    else if (menu) closeMenuAndRestoreFocus();
    else if (role === "category" || role === "organize")
      run(() => native("dismiss"));
  }
  useEffect(() => {
    const key = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        e.preventDefault();
        escape();
      }
    };
    window.addEventListener("keydown", key);
    return () => window.removeEventListener("keydown", key);
  }, [form, menu]);
  const drag = useRef<number | null>(null);
  const queue = useRef(Promise.resolve<unknown>(null));
  const updating = useRef(false);
  function gesture(resize: boolean) {
    function send(phase: string) {
      queue.current = queue.current
        .then(() => invoke("frame", { phase, resize, dx: 0, dy: 0 }))
        .catch(fail);
    }
    return {
      onPointerDown: (e: React.PointerEvent<HTMLElement>) => {
        if (role !== "group" || !page || (!resize && page.locked) || e.button !== 0 ||
            (!resize && (e.target as Element).closest("button"))) return;
        e.currentTarget.setPointerCapture(e.pointerId);
        drag.current = e.pointerId;
        send("begin");
      },
      onPointerMove: (e: React.PointerEvent<HTMLElement>) => {
        if (drag.current !== e.pointerId || updating.current) return;
        updating.current = true;
        send("update");
        void queue.current.finally(() => { updating.current = false; });
      },
      onLostPointerCapture: (e: React.PointerEvent<HTMLElement>) => {
        if (drag.current !== e.pointerId) return;
        drag.current = null;
        send("end");
      },
    };
  }
  const picker = (folder: boolean) =>
    run(() => invoke("pick", { folder }));
  const report = error ? (
    <output role="alert">
      {error}{" "}
      <button
        onClick={() =>
          run(async () => {
            if (role === "dock") await native("recover");
            setRetry((i) => i + 1);
          })
        }
      >
        Reintentar
      </button>
    </output>
  ) : null;
  const dialog = form && (
    <form
      className="editor"
      role="dialog"
      aria-label={form.title}
      onSubmit={(e) => {
        e.preventDefault();
        if (pending) return;
        setPending(true);
        run(async () => {
          try {
            await form.apply(value);
            setForm(undefined);
          } finally {
            setPending(false);
          }
        });
      }}
    >
      <strong>{form.title}</strong>
      {form.value !== undefined && (
        <input
          autoFocus
          aria-label={form.title}
          value={value}
          maxLength={260}
          onChange={(e) => setValue(e.target.value)}
        />
      )}
      <div>
        <button type="button" onClick={() => setForm(undefined)}>
          Cancelar
        </button>
        <button disabled={pending} type="submit">
          {form.confirm}
        </button>
      </div>
    </form>
  );
  if (role === "dock")
    return (
      <nav
        className={
          "glass dock floating-dock" + (data?.vertical ? " vertical" : "")
        }
        aria-label="NexDock"
      >
        <Button
          name="Organizar espacio y perfiles"
          action={() => run(() => native("organize"))}
        >
          <Settings2 />
        </Button>
        <i />
        <div className="categories">
          {data?.categories.map((c) => (
            <button
              key={c.id}
              data-category={c.id}
              title={c.name}
              aria-label={c.name}
              aria-pressed={c.id === data.selected}
              className={c.id === data.selected ? "selected" : ""}
              onClick={(e) => {
                const r = e.currentTarget.getBoundingClientRect();
                run(() =>
                  native("category", {
                    profile: data.profile,
                    category: c.id,
                    anchor: data.vertical
                      ? r.y + r.height / 2
                      : r.x + r.width / 2,
                  }),
                );
              }}
            >
              <span className="glyph">{c.glyph}</span>
            </button>
          ))}
        </div>
        {report}
      </nav>
    );
  if (role === "organize")
    return (
      <main className="glass organizer">
        <header>
          <Settings2 />
          <h1>Espacio de trabajo</h1>
          <Button name="Cerrar" action={() => run(() => native("dismiss"))}>
            <X />
          </Button>
        </header>
        <nav>
          {["Contenedores", "Categorías", "Perfiles"].map((t) => (
            <Button
              key={t}
              name={t}
              selected={tab === t}
              action={() => setTab(t)}
            >
              {t}
            </Button>
          ))}
        </nav>
        <section>
          {!data && !error && <p role="status">Cargando biblioteca…</p>}
          {data &&
            (tab === "Contenedores" ? (
              <>
                <button
                  onClick={() =>
                    rename("Crear contenedor", "", "createGroup", {})
                  }
                >
                  ＋ Nuevo contenedor
                </button>
                {data.groups.map((g) => (
                  <div className="setting" key={g.id}>
                    <span>{g.name}</span>
                    <button
                      onClick={() =>
                        run(() => act("show", { target: g.id, flag: !g.shown }))
                      }
                    >
                      {g.shown ? "Ocultar" : "Mostrar"}
                    </button>
                  </div>
                ))}
              </>
            ) : tab === "Categorías" ? (
              <>
                <label>
                  Vincular a{" "}
                  <select
                    value={link}
                    onChange={(e) => setLink(e.target.value)}
                  >
                    <option value="">Categoría con referencias propias</option>
                    {data.groups.map((g) => (
                      <option key={g.id} value={g.id}>
                        {g.name}
                      </option>
                    ))}
                  </select>
                </label>
                <button
                  onClick={() =>
                    rename("Crear categoría", "", "createCategory", {
                      link: link || null,
                    })
                  }
                >
                  ＋ Nueva categoría
                </button>
                {data.categories.map((c, i) => (
                  <div className="setting" key={c.id}>
                    <span>{c.name}</span>
                    <button
                      onClick={() =>
                        rename("Renombrar categoría", c.name, "editCategory", {
                          target: c.id,
                          link: c.containerId,
                        })
                      }
                    >
                      Renombrar
                    </button>
                    <select
                      aria-label={"Contenedor de " + c.name}
                      value={c.containerId || ""}
                      onChange={(e) => {
                        const next = e.target.value || null;
                        ask({
                          title: "¿Cambiar el origen de " + c.name + "?",
                          confirm: "Aplicar",
                          apply: () =>
                            act("editCategory", {
                              target: c.id,
                              name: c.name,
                              link: next,
                            }),
                        });
                      }}
                    >
                      <option value="">Referencias propias</option>
                      {data.groups.map((g) => (
                        <option key={g.id} value={g.id}>
                          {g.name}
                        </option>
                      ))}
                    </select>
                    <button
                      disabled={!i}
                      aria-label={"Subir " + c.name}
                      onClick={() =>
                        run(() =>
                          act("moveCategory", { target: c.id, offset: -1 }),
                        )
                      }
                    >
                      ↑
                    </button>
                    <button
                      disabled={i + 1 === data.categories.length}
                      aria-label={"Bajar " + c.name}
                      onClick={() =>
                        run(() =>
                          act("moveCategory", { target: c.id, offset: 1 }),
                        )
                      }
                    >
                      ↓
                    </button>
                    <button
                      onClick={() =>
                        remove(
                          "¿Quitar la categoría " + c.name + "?",
                          "deleteCategory",
                          { target: c.id },
                        )
                      }
                    >
                      Quitar
                    </button>
                  </div>
                ))}
                <button
                  onClick={() =>
                    run(() => act("vertical", { flag: !data.vertical }))
                  }
                >
                  Usar dock {data.vertical ? "horizontal" : "vertical"}
                </button>
              </>
            ) : (
              <>
                <button
                  onClick={() =>
                    rename("Crear perfil", "", "createProfile", {})
                  }
                >
                  ＋ Nuevo perfil
                </button>
                <button
                  onClick={() =>
                    rename("Duplicar perfil activo", "", "createProfile", {
                      flag: true,
                    })
                  }
                >
                  Duplicar activo
                </button>
                {data.profiles.map((p) => (
                  <div className="setting" key={p.id}>
                    <span>{p.name}</span>
                    <button
                      onClick={() =>
                        rename("Renombrar perfil", p.name, "renameProfile", {
                          target: p.id,
                        })
                      }
                    >
                      Renombrar
                    </button>
                    <button
                      disabled={data.profile === p.id}
                      onClick={() =>
                        run(() => act("switchProfile", { target: p.id }))
                      }
                    >
                      {data.profile === p.id ? "Activo" : "Activar"}
                    </button>
                  </div>
                ))}
              </>
            ))}
        </section>
        <footer>
          <button onClick={() => run(() => native("recover"))}>
            Recuperar ventanas
          </button>
          <button onClick={() => run(() => native("quit"))}>Salir</button>
        </footer>
        {dialog}
        {report}
      </main>
    );
  const popup = role === "category";
  return (
    <main
      className={
        "glass " +
        (popup ? (page?.vertical ? "popup side-popup" : "popup") : "panel") +
        (page?.collapsed && !popup ? " collapsed" : "")
      }
    >
      <header {...gesture(false)}>
        <Mark name={page?.name || ""} />
        <h1>{page?.name || "Cargando…"}</h1>
        {(popup || !page?.collapsed) && (
          <Button
            name="Opciones"
            expanded={menu?.kind === "options"}
            action={(event) => {
              const trigger = event.currentTarget;
              setMenu((current) => current?.kind === "options" ? undefined : { kind: "options", trigger });
            }}
          >
            <Ellipsis />
          </Button>
        )}
        {popup ? (
          <Button
            name="Cerrar categoría"
            action={() => run(() => native("dismiss"))}
          >
            <X />
          </Button>
        ) : (
          <Button
            name={page?.collapsed ? "Expandir" : "Contraer"}
            action={() =>
              run(() => act("collapse", { flag: !page?.collapsed }))
            }
          >
            <ChevronDown className={page?.collapsed ? "turned" : ""} />
          </Button>
        )}
      </header>
      {(popup || !page?.collapsed) && (
        <section className={popup || page?.list ? "rows" : "tiles"}>
          {page?.rows.map((row) => (
            <button
              key={row.id}
              title={row.name}
              onClick={() => run(() => act("open", { item: row.id }))}
              onContextMenu={(e) => {
                e.preventDefault();
                setMenu({ kind: "entry", row, trigger: e.currentTarget });
              }}
              onKeyDown={(e) => {
                if (
                  e.key === "ContextMenu" ||
                  (e.shiftKey && e.key === "F10")
                ) {
                  e.preventDefault();
                  setMenu({ kind: "entry", row, trigger: e.currentTarget });
                }
              }}
            >
              {page.art[row.key] ? (
                <img
                  alt=""
                  className="art"
                  src={"data:image/png;base64," + page.art[row.key]}
                />
              ) : row.kind === "Folder" ? (
                <Folder className="art" />
              ) : (
                <FileText className="art" />
              )}
              <span>
                {row.name}
                {row.status !== "available" && " · No disponible"}
              </span>
            </button>
          ))}
          {page && page.total === 0 && (
            <button onClick={() => picker(false)}>
              <Plus /> Añadir primera referencia
            </button>
          )}
        </section>
      )}
      {page && page.total > 24 && (
        <div className="paging">
          <button disabled={!offset} onClick={() => setOffset(offset - 24)}>
            Anterior
          </button>
          <button
            disabled={offset + 24 >= page.total}
            onClick={() => setOffset(offset + 24)}
          >
            Siguiente
          </button>
        </div>
      )}
      {menu && (
        <aside
          className="choices"
          ref={menuSurface}
          aria-label={entry ? "Acciones de " + entry.name : "Opciones del panel"}
          onClickCapture={(event) => {
            if (event.target instanceof Element && event.target.closest("button")) {
              closeMenuAndRestoreFocus();
            }
          }}
        >
          {entry ? (
            <>
              <strong>{entry.name}</strong>
              <button
                onClick={() =>
                  rename("Nombre de referencia", entry.name, "rename", {
                    item: entry.id,
                  })
                }
              >
                Renombrar
              </button>
              {[-1, 1].map((o) => (
                <button
                  key={o}
                  onClick={() =>
                    run(() => act("moveItem", { item: entry.id, offset: o }))
                  }
                >
                  {o < 0 ? "Mover antes" : "Mover después"}
                </button>
              ))}
              <button
                onClick={() =>
                  remove(
                    "¿Quitar la referencia? El original se conserva.",
                    "remove",
                    { item: entry.id },
                  )
                }
              >
                Quitar referencia
              </button>
            </>
          ) : (
            <>
              <button onClick={() => picker(false)}>
                Añadir archivos o accesos
              </button>
              <button onClick={() => picker(true)}>Añadir carpeta</button>
              {!popup && (
                <>
                  {page?.multipleDisplays && !page.locked && (
                    <button onClick={() => run(() =>
                      invoke("frame", { phase: "next-monitor", resize: false, dx: 0, dy: 0 })
                    )}>Mover a otra pantalla</button>
                  )}
                  <button
                    onClick={() =>
                      run(() => act("listView", { flag: !page?.list }))
                    }
                  >
                    {page?.list ? "Mostrar cuadrícula" : "Mostrar lista"}
                  </button>
                  <button
                    onClick={() =>
                      run(() => act("lock", { flag: !page?.locked }))
                    }
                  >
                    {page?.locked
                      ? "Desbloquear posición"
                      : "Bloquear posición"}
                  </button>
                  <button
                    onClick={() =>
                      rename(
                        "Renombrar contenedor",
                        page?.name || "",
                        "renameGroup",
                        {},
                      )
                    }
                  >
                    Renombrar contenedor
                  </button>
                  <button
                    onClick={() =>
                      remove(
                        "¿Quitar " + page?.name + " y sus referencias?",
                        "deleteGroup",
                        {},
                      )
                    }
                  >
                    Quitar contenedor
                  </button>
                </>
              )}
            </>
          )}
        </aside>
      )}
      {!popup && !page?.collapsed && (
        <button
          className="grip"
          aria-label="Redimensionar"
          {...gesture(true)}
          onKeyDown={(e) => {
            const d: Record<string, number[]> = {
              ArrowLeft: [-12, 0],
              ArrowRight: [12, 0],
              ArrowUp: [0, -12],
              ArrowDown: [0, 12],
            };
            if (d[e.key]) {
              e.preventDefault();
              run(() =>
                invoke("frame", {
                  phase: "key",
                  resize: true,
                  dx: d[e.key][0],
                  dy: d[e.key][1],
                }),
              );
            }
          }}
        >
          <Grip size={14} />
        </button>
      )}
      {dialog}
      {report}
    </main>
  );
}
