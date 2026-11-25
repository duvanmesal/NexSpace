import React, { act } from "react";
import { createRoot } from "react-dom/client";
import { Floating } from "../src/Floating";
import "../src/style.css";

const result = document.querySelector<HTMLPreElement>("#result")!;
const root = createRoot(document.querySelector("#root")!);
const popup = location.search.includes("floating=category_");
const calls: string[] = [];
const checks: string[] = [];
let completeMove: (() => void) | undefined;
let collapsed = false;
let list = false;

Object.assign(window, {
  IS_REACT_ACT_ENVIRONMENT: true,
  __TAURI_INTERNALS__: {
    async invoke(command: string, fields: {
      op?: string;
      request?: { op: string; item?: string; flag?: boolean };
    }) {
      if (command === "surface") {
        calls.push("surface:" + fields.op);
        return;
      }
      const request = fields.request!;
      if (request.op === "page") {
        return {
          name: "Archivos", art: {}, collapsed, list, total: 2,
          rows: ["Notas", "Proyecto"].map((name) =>
            ({ id: name, name, key: "", kind: "File", status: "available" })),
        };
      }
      calls.push(request.op + ":" + (request.item || ""));
      if (request.op === "moveItem") {
        await new Promise<void>((resolve) => { completeMove = resolve; });
      }
      if (request.op === "collapse" || request.op === "listView") {
        if (request.op === "collapse") collapsed = request.flag!;
        else list = request.flag!;
        window.dispatchEvent(new Event("library-changed"));
      }
    },
  },
});

function require(condition: unknown, description: string): asserts condition {
  if (!condition) throw new Error(description);
}

function button(name: string) {
  const element = Array.from(document.querySelectorAll<HTMLButtonElement>("button"))
    .find((button) => (button.getAttribute("aria-label") || button.textContent?.trim()) === name);
  require(element, "Falta el botón " + name);
  return element;
}

const menu = () => document.querySelector(".choices");

async function tap(target: HTMLElement, right = false) {
  const button = right ? 2 : 0;
  await act(async () => {
    const init = { bubbles: true, cancelable: true, button };
    require(target.dispatchEvent(new PointerEvent("pointerdown", { ...init, pointerType: "mouse" })),
            "El menú consumió el pointerdown exterior.");
    target.dispatchEvent(new MouseEvent("mousedown", init));
    if (right) {
      const event = new MouseEvent("contextmenu", init);
      target.dispatchEvent(event);
      require(event.defaultPrevented, "Se permitió el menú contextual del navegador.");
    } else {
      target.focus();
      target.click();
    }
  });
}

async function key(target: EventTarget, key: string, shiftKey = false) {
  await act(async () => {
    target.dispatchEvent(new KeyboardEvent("keydown", { bubbles: true, cancelable: true, key, shiftKey }));
  });
}

async function caseOf(name: string, step: () => Promise<void>) {
  await step();
  checks.push(name);
}

async function test() {
  await act(async () => root.render(<Floating />));
  const options = button("Opciones");
  const notes = button("Notas");
  const project = button("Proyecto");

  await caseOf("opciones: abrir, Escape, volver a abrir y alternar", async () => {
    await tap(options);
    require(menu() && options.getAttribute("aria-expanded") === "true", "Opciones no abrió.");
    require(menu()?.contains(document.activeElement), "El foco no entró en Opciones.");
    await key(document.activeElement!, "Escape");
    require(!menu() && document.activeElement === options, "Escape no devolvió el foco al botón.");
    await tap(options);
    await tap(options);
    require(!menu() && options.getAttribute("aria-expanded") === "false", "El botón no alternó el menú.");
  });

  await caseOf("opciones: clic exterior llega al archivo", async () => {
    await tap(options);
    const rect = notes.getBoundingClientRect();
    const hit = document.elementFromPoint(rect.left + rect.width / 2, rect.top + rect.height / 2);
    require(hit && notes.contains(hit), "La superficie del menú intercepta el archivo exterior.");
    await tap(notes);
    require(!menu() && calls.at(-1) === "open:Notas" && document.activeElement === notes,
            "El clic exterior no cerró Opciones o perdió la acción del archivo.");
  });

  await caseOf("archivo: menú alterna con otros archivos y con opciones", async () => {
    await tap(project, true);
    require(menu()?.getAttribute("aria-label") === "Acciones de Proyecto", "Clic derecho no abrió el menú.");
    await tap(notes, true);
    require(menu()?.getAttribute("aria-label") === "Acciones de Notas", "El segundo archivo no sustituyó el menú.");
    await tap(options);
    require(menu()?.getAttribute("aria-label") === "Opciones del panel", "Opciones no sustituyó el menú de archivo.");
    await key(project, "F10", true);
    require(menu()?.getAttribute("aria-label") === "Acciones de Proyecto", "Shift+F10 no sustituyó Opciones.");
    await key(document.activeElement!, "Escape");
    require(!menu() && document.activeElement === project, "Escape no devolvió el foco al archivo.");
  });

  await caseOf("archivo: clic exterior ejecuta el archivo de destino", async () => {
    await tap(project, true);
    const before = calls.length;
    await tap(notes);
    require(!menu() && calls.length === before + 1 && calls.at(-1) === "open:Notas",
            "El menú de archivo consumió el clic exterior.");
  });

  await caseOf("archivo: selección cierra de inmediato y el IPC no cierra otro menú", async () => {
    await tap(notes, true);
    await tap(button("Mover después"));
    require(!menu() && calls.at(-1) === "moveItem:Notas" && completeMove, "Seleccionar no cerró la acción pendiente.");
    await tap(options);
    await act(async () => completeMove!());
    require(menu()?.getAttribute("aria-label") === "Opciones del panel", "La respuesta cerró el menú siguiente.");
    await key(window, "Escape");
  });

  await caseOf("pérdida de foco web, nativa y por teclado", async () => {
    await tap(options);
    await act(async () => window.dispatchEvent(new Event("blur")));
    require(!menu(), "El blur de la ventana no cerró Opciones.");
    await tap(notes, true);
    await act(async () => window.dispatchEvent(new Event("panel-focus-lost")));
    require(!menu(), "El aviso nativo no cerró el menú de archivo.");
    await tap(options);
    await act(async () => project.focus());
    require(!menu() && document.activeElement === project, "El foco exterior no se conservó.");
  });

  if (popup) {
    await caseOf("popover: Escape del menú y luego de la categoría", async () => {
      await tap(options);
      const before = calls.length;
      await key(document.activeElement!, "Escape");
      require(!menu() && calls.length === before, "Escape cerró toda la categoría antes que el menú.");
      await key(window, "Escape");
      require(calls.at(-1) === "surface:dismiss", "Escape ya no cierra la categoría.");
    });
  } else {
    await caseOf("contenedor: elegir una opción, contraer y expandir", async () => {
      await tap(options);
      await tap(button("Mostrar lista"));
      require(!menu() && calls.at(-1) === "listView:" && document.querySelector("section.rows"),
              "La selección no cerró Opciones o no cambió a lista.");
      await tap(options);
      await tap(button("Contraer"));
      require(!menu() && !document.querySelector("section"), "El contenedor no se contrajo tras el clic exterior.");
      await tap(button("Expandir"));
      await tap(button("Notas"));
      require(calls.at(-1) === "open:Notas", "El archivo perdió su interacción al expandir.");
    });
  }
  await act(async () => root.unmount());
}

try {
  await test();
  result.dataset.status = "pass";
  result.textContent = checks.join(" | ");
} catch (error) {
  result.dataset.status = "fail";
  result.textContent = String(error);
}
