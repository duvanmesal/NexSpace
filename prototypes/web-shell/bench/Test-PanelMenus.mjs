// Runs the real React panel in a disposable Edge profile with a stubbed Tauri bridge.
import { execFile } from "node:child_process";
import { access, mkdtemp, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join, relative } from "node:path";
import { fileURLToPath } from "node:url";
import { promisify } from "node:util";
import { createServer } from "vite";

const run = promisify(execFile);
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
    for (const role of ["group", "category"]) {
      const profile = await mkdtemp(join(base, "NexSpace-panel-menus-"));
      try {
        const { stdout } = await run(edge, [
          "--headless=new", "--no-first-run", "--disable-extensions",
          `--user-data-dir=${profile}`, "--window-size=550,460",
          "--dump-dom", "--virtual-time-budget=5000",
          `http://127.0.0.1:${port}/bench/panel-menus.html?floating=${role}_profile_panel`,
        ], { timeout: 35_000, windowsHide: true });
        const result = stdout.match(/<pre id="result" data-status="([^"]+)">([^<]*)<\/pre>/);
        if (result?.[1] !== "pass") throw new Error(`${role}: ${result?.[2] || "sin resultado del navegador"}`);
        console.log(`PASS ${role} · ${result[2]}`);
      } finally {
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
