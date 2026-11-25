// Runs the real React panel in a disposable Edge profile with a stubbed Tauri bridge.
import { spawn } from "node:child_process";
import { once } from "node:events";
import { access, mkdtemp, readFile, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join, relative } from "node:path";
import { fileURLToPath } from "node:url";
import { createServer } from "vite";
import { connect, until } from "./devtools.mjs";

const root = fileURLToPath(new URL("../", import.meta.url));

async function findEdge() {
  const folders = [process.env["ProgramFiles(x86)"], process.env.ProgramFiles, process.env.LOCALAPPDATA];
  const paths = [process.env.NEXSPACE_EDGE,
    ...folders.filter(Boolean).map((folder) => join(folder, "Microsoft", "Edge", "Application", "msedge.exe"))];
  for (const path of paths.filter(Boolean)) {
    try {
      await access(path);
      return path;
    } catch (error) {
      if (error.code !== "ENOENT") throw error;
    }
  }
}

async function test(edge) {
  const server = await createServer({
    root,
    configFile: false,
    server: { host: "127.0.0.1", port: 0 },
    optimizeDeps: { entries: ["bench/panel-menus.html"] },
  });
  const base = tmpdir();
  try {
    await server.listen();
    const { port } = server.httpServer.address();
    for (const role of ["group", "category", "dock"]) {
      const profile = await mkdtemp(join(base, "NexSpace-panel-menus-"));
      const browser = spawn(edge, [
        "--headless=new", "--no-first-run", "--disable-extensions", "--remote-debugging-port=0",
        `--user-data-dir=${profile}`, "--window-size=550,460",
        `http://127.0.0.1:${port}/bench/panel-menus.html?floating=${role}_profile_panel`,
      ], { windowsHide: true, stdio: "ignore" });
      const exited = once(browser, "exit");
      let page;
      try {
        const debugPort = await until(async () => {
          try {
            return parseInt(await readFile(join(profile, "DevToolsActivePort"), "utf-8"), 10);
          } catch (error) {
            if (error.code !== "ENOENT") throw error;
          }
        }, "Edge no abrió DevTools.");
        const target = await until(async () => {
          const targets = await (await fetch(`http://127.0.0.1:${debugPort}/json/list`)).json();
          return targets.find(({ url }) => url.includes("panel-menus.html"));
        }, "No se encontró la página de pruebas.");
        page = await connect(target.webSocketDebuggerUrl);
        const result = await until(async () => {
          const state = await page.read(`(() => {
            const result = document.getElementById("result");
            return { status: result?.dataset.status, text: result?.textContent, input: window.menuTestInput };
          })()`);
          if (state.input) {
            await page.click(state.input);
            await page.read("window.completeMenuTestInput()");
          }
          return state.status && state.status !== "pending" ? state : null;
        }, `${role}: sin resultado del navegador.`, 25_000);
        if (result.status !== "pass") throw new Error(`${role}: ${result.text}`);
        console.log(`PASS ${role} · ${result.text}`);
      } finally {
        if (page) {
          await page.send("Browser.close").catch(() => undefined);
          page.close();
        } else {
          browser.kill();
        }
        await exited;
        if (!/^NexSpace-panel-menus-[^/\\]+$/.test(relative(base, profile))) {
          throw new Error("El perfil Headless no pertenece al directorio temporal.");
        }
        await rm(profile, { recursive: true, force: true, maxRetries: 5, retryDelay: 200 });
      }
    }
  } finally {
    await server.close();
  }
}

const edge = await findEdge();
if (edge) {
  try {
    await test(edge);
  } catch (error) {
    console.error(error.message);
    process.exitCode = 1;
  }
} else {
  console.error("Se requiere Microsoft Edge o una ruta en NEXSPACE_EDGE.");
  process.exitCode = 2;
}
