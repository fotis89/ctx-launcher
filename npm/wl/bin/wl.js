#!/usr/bin/env node
const { spawnSync } = require("node:child_process");
const { chmodSync } = require("node:fs");

const platformMap = {
  win32: "win",
  linux: "linux",
  darwin: "darwin",
};

const osKey = platformMap[process.platform];
const archKey = process.arch;
const pkg = osKey && `@ctx-launcher/wl-${osKey}-${archKey}`;
const exe = process.platform === "win32" ? "wl.exe" : "wl";

if (!pkg) {
  console.error(`@ctx-launcher/wl: unsupported platform ${process.platform}.`);
  process.exit(1);
}

let bin;
try {
  bin = require.resolve(`${pkg}/bin/${exe}`);
} catch {
  console.error(`@ctx-launcher/wl: no prebuilt binary for ${process.platform}-${archKey}.`);
  console.error(`Prebuilt platforms: win32-x64, linux-x64, darwin-arm64.`);
  console.error(`For other platforms, build from source: https://github.com/fotis89/ctx-launcher`);
  process.exit(1);
}

// npm tarballs served via GitHub Actions artifact download lose the +x bit.
// Chmod on Unix before spawning so the binary is runnable.
if (process.platform !== "win32") {
  try { chmodSync(bin, 0o755); } catch { /* best-effort — EPERM on shared installs is OK */ }
}

const result = spawnSync(bin, process.argv.slice(2), { stdio: "inherit" });
if (result.error) {
  console.error(result.error.message);
  process.exit(1);
}
process.exit(result.status ?? 1);
