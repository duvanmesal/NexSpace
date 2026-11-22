import { invoke, isTauri } from "@tauri-apps/api/core";

export type Group = "files" | "games" | "apps";
export type Action =
  | { kind: "open"; id: number }
  | { kind: "popover"; group: Group }
  | { kind: "collapse"; collapsed: boolean }
  | { kind: "size"; width: number; height: number }
  | { kind: "move"; x: number; y: number }
  | { kind: "ready" | "recover" | "close" | "dismiss" };

interface WebBridge {
  postMessage(request: Action): void;
  addEventListener(type: "message", listener: (e: MessageEvent) => void): void;
}
declare global {
  interface Window {
    chrome?: { webview?: WebBridge };
  }
}
export async function send(request: Action) {
  if (isTauri()) {
    const accent = await invoke<string | null>("dispatch", { request });
    if (request.kind === "ready" && accent && /^#[0-9a-f]{6}$/i.test(accent))
      document.documentElement.style.setProperty("--accent", accent);
  } else if (window.chrome?.webview) window.chrome.webview.postMessage(request);
  else throw Error("Abre el prototipo desde su anfitrión Windows.");
}
