const { spawn } = require("child_process");
const fs = require("fs");
const http = require("http");
const path = require("path");

const root = path.join(__dirname, "..");
const port = Number(process.env.PORT) || 3000;
const url = `http://localhost:${port}`;

function fail(message) {
  console.error("");
  console.error(message);
  console.error("");
  process.exit(1);
}

function run(command, args) {
  return new Promise((resolve, reject) => {
    const child = spawn(command, args, {
      cwd: root,
      stdio: "inherit",
      shell: process.platform === "win32",
      env: process.env
    });
    child.on("error", reject);
    child.on("exit", (code) => {
      if (code === 0) resolve();
      else reject(new Error(`${command} 未能完成（退出码 ${code}）`));
    });
  });
}

function openBrowser(target) {
  if (process.platform === "win32") {
    spawn("cmd", ["/c", "start", "", target], { detached: true, stdio: "ignore" }).unref();
    return;
  }
  const command = process.platform === "darwin" ? "open" : "xdg-open";
  spawn(command, [target], { detached: true, stdio: "ignore" }).unref();
}

function waitAndOpen() {
  const started = Date.now();
  const timer = setInterval(() => {
    const request = http.get(`${url}/api/health`, (response) => {
      response.resume();
      if (response.statusCode === 200) {
        clearInterval(timer);
        openBrowser(url);
      }
    });
    request.on("error", () => {});
    if (Date.now() - started > 60000) clearInterval(timer);
  }, 500);
}

async function main() {
  const major = Number(process.versions.node.split(".")[0]);
  if (major < 18) {
    fail(`需要 Node.js 18 或更高版本，当前是 ${process.version}。\n请到 https://nodejs.org/ 安装后再双击一次。`);
  }

  const installed = fs.existsSync(path.join(root, "node_modules", "express"));
  if (!installed) {
    console.log("第一次使用，正在安装依赖。这一步需要联网，请稍等...");
    await run("npm", ["install"]);
  }

  console.log("");
  console.log(`服务启动后会自动打开浏览器：${url}`);
  console.log("关闭这个窗口就会停止服务。");
  console.log("");
  waitAndOpen();
  await run("node", ["scripts/start.cjs"]);
}

main().catch((error) => {
  fail(error && error.message ? error.message : String(error));
});
