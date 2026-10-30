const { spawnSync } = require("child_process");
const fs = require("fs");
const path = require("path");

const root = path.join(__dirname, "..");
const buildDir = path.join(root, "build");
const releaseDir = path.join(root, "release");
const exeName = "听力音频生成器.exe";

function run(command, args) {
  const result = spawnSync(command, args, {
    cwd: root,
    stdio: "inherit"
  });
  if (result.status !== 0) {
    throw new Error(`${path.basename(command)} 失败（退出码 ${result.status}）`);
  }
}

function contentType(filePath) {
  if (filePath.endsWith(".css")) return "css";
  if (filePath.endsWith(".js")) return "js";
  return "html";
}

function writeEmbeddedAssets() {
  const indexHtml = fs.readFileSync(path.join(root, "dist", "index.html"), "utf8");
  const files = {
    "/": { type: "html", body: indexHtml },
    "/index.html": { type: "html", body: indexHtml }
  };
  const assetDir = path.join(root, "dist", "assets");
  for (const name of fs.readdirSync(assetDir)) {
    const full = path.join(assetDir, name);
    files["/assets/" + name] = {
      type: contentType(name),
      body: fs.readFileSync(full, "utf8")
    };
  }
  const voiceMap = JSON.parse(fs.readFileSync(path.join(root, "voice_map.json"), "utf8"));
  fs.mkdirSync(buildDir, { recursive: true });
  const output = "module.exports = " + JSON.stringify({ voiceMap, files }) + ";\n";
  fs.writeFileSync(path.join(buildDir, "embedded-assets.cjs"), output);
}

function bundle() {
  const esbuild = path.join(root, "node_modules", "esbuild", "bin", "esbuild");
  run(process.execPath, [
    esbuild,
    "scripts/exe-main.cjs",
    "--bundle",
    "--platform=node",
    "--format=cjs",
    "--outfile=build/bundle.cjs",
    "--external:vite",
    "--external:bufferutil",
    "--external:utf-8-validate"
  ]);
}

function makeSeaBlob() {
  const configPath = path.join(buildDir, "sea-config.json");
  fs.writeFileSync(configPath, JSON.stringify({
    main: path.join(buildDir, "bundle.cjs"),
    output: path.join(buildDir, "sea-prep.blob"),
    disableExperimentalSEAWarning: true
  }, null, 2));
  run(process.execPath, ["--experimental-sea-config", configPath]);
}

function injectExe() {
  fs.mkdirSync(releaseDir, { recursive: true });
  const exePath = path.join(releaseDir, exeName);
  fs.copyFileSync(process.execPath, exePath);
  const postject = path.join(root, "node_modules", "postject", "dist", "cli.js");
  run(process.execPath, [
    postject,
    exePath,
    "NODE_SEA_BLOB",
    path.join(buildDir, "sea-prep.blob"),
    "--sentinel-fuse",
    "NODE_SEA_FUSE_fce680ab2cc467b6e072b8b5df1996b2"
  ]);
  setIcon(exePath, path.join(root, "icons", "browser.ico"));
  return exePath;
}

function setIcon(exePath, icoPath) {
  const ResEdit = require("resedit");
  const exe = ResEdit.NtExecutable.from(fs.readFileSync(exePath), { ignoreCert: true });
  const res = ResEdit.NtExecutableResource.from(exe);
  const iconFile = ResEdit.Data.IconFile.from(fs.readFileSync(icoPath));
  ResEdit.Resource.IconGroupEntry.replaceIconsForResource(
    res.entries,
    1,
    1033,
    iconFile.icons.map((item) => item.data)
  );
  res.outputResource(exe);
  fs.writeFileSync(exePath, Buffer.from(exe.generate()));
}

writeEmbeddedAssets();
bundle();
makeSeaBlob();
const exePath = injectExe();
console.log("已生成 " + exePath);
