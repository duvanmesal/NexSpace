import React, { useState, useEffect, useRef } from "react";
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
}: {
  name: string;
  action: () => void;
  children: React.ReactNode;
  selected?: boolean;
}) {
  return (
    <button
      type="button"
      className={selected ? "selected" : ""}
      title={name}
      aria-label={name}
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
  const [menu, setMenu] = useState(false);
  const [entry, setEntry] = useState<Row>();
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
    setMenu(false);
    setEntry(undefined);
    setError("");
  }
  function ask(f: Form) {
    setValue(f.value || "");
    setForm(f);
    setMenu(false);
    setEntry(undefined);
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
  function escape() {
    if (form) setForm(undefined);
    else if (menu || entry) {
      setMenu(false);
      setEntry(undefined);
    } else if (role === "category" || role === "organize")
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
  }, [form, menu, entry]);
  const drag = useRef<{
    x: number;
    y: number;
    l: number;
    t: number;
    w: number;
    h: number;
    resize: boolean;
  } | null>(null);
  const queue = useRef(Promise.resolve<unknown>(null));
  function gesture(resize: boolean) {
    return {
      onPointerDown: (e: React.PointerEvent<HTMLElement>) => {
        if (
          role !== "group" ||
          (!resize && page?.locked) ||
          e.button !== 0 ||
          (!resize && (e.target as Element).closest("button"))
        )
          return;
        e.currentTarget.setPointerCapture(e.pointerId);
        drag.current = {
          x: e.screenX,
          y: e.screenY,
          l: screenX,
          t: screenY,
          w: innerWidth,
          h: innerHeight,
          resize,
        };
      },
      onPointerMove: (e: React.PointerEvent<HTMLElement>) => update(e, false),
      onPointerUp: (e: React.PointerEvent<HTMLElement>) => update(e, true),
      onPointerCancel: (e: React.PointerEvent<HTMLElement>) => update(e, true),
    };
  }
  function update(e: React.PointerEvent, save: boolean) {
    const d = drag.current;
    if (!d) return;
    const dx = e.screenX - d.x,
      dy = e.screenY - d.y;
    const args = {
      x: d.l + (d.resize ? 0 : dx),
      y: d.t + (d.resize ? 0 : dy),
      width: d.w + (d.resize ? dx : 0),
      height: d.h + (d.resize ? dy : 0),
      save,
    };
    if (save) drag.current = null;
    queue.current = queue.current.then(() => invoke("frame", args)).catch(fail);
  }
  const picker = (folder: boolean) =>
    run(async () => {
      setMenu(false);
      await invoke("pick", { folder });
    });
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
            action={() => {
              setMenu(!menu);
              setEntry(undefined);
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
                setEntry(row);
                setMenu(false);
              }}
              onKeyDown={(e) => {
                if (
                  e.key === "ContextMenu" ||
                  (e.shiftKey && e.key === "F10")
                ) {
                  e.preventDefault();
                  setEntry(row);
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
      {(menu || entry) && (
        <aside className="choices">
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
                  x: screenX,
                  y: screenY,
                  width: innerWidth + d[e.key][0],
                  height: innerHeight + d[e.key][1],
                  save: true,
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
