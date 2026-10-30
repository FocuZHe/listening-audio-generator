const http = require("http");
const fs = require("fs");

const port = Number(process.env.PORT) || 3010;
const voices = Object.entries(require("../voice_map.json"));
const concurrency = 4;

function tts(name) {
  const body = JSON.stringify({
    text: "Hello.",
    voiceName: name,
    role: "narrator",
    speed: 1
  });
  return new Promise((resolve) => {
    const req = http.request({
      hostname: "127.0.0.1",
      port,
      path: "/api/tts",
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "Content-Length": Buffer.byteLength(body)
      },
      timeout: 25000
    }, (res) => {
      const chunks = [];
      res.on("data", (chunk) => chunks.push(chunk));
      res.on("end", () => {
        const raw = Buffer.concat(chunks).toString("utf8");
        let audioLen = 0;
        let message = "";
        try {
          const parsed = JSON.parse(raw);
          if (parsed.audio) audioLen = parsed.audio.length;
          message = parsed.message || parsed.error || "";
        } catch (error) {
          message = raw.slice(0, 180);
        }
        resolve({ name, status: res.statusCode, audioLen, message });
      });
    });
    req.on("timeout", () => {
      req.destroy();
      resolve({ name, status: 0, audioLen: 0, message: "timeout" });
    });
    req.on("error", (error) => {
      resolve({ name, status: 0, audioLen: 0, message: error.message });
    });
    req.write(body);
    req.end();
  });
}

async function once(name, id) {
  let result = await tts(name);
  if (result.status !== 200 || result.audioLen < 100) {
    result = await tts(name);
    result.retried = true;
  }
  result.id = id;
  return result;
}

async function main() {
  let cursor = 0;
  const results = [];
  async function worker() {
    while (cursor < voices.length) {
      const index = cursor;
      cursor += 1;
      const [name, id] = voices[index];
      const result = await once(name, id);
      results.push(result);
      const ok = result.status === 200 && result.audioLen >= 100;
      console.log(`${ok ? "OK" : "FAIL"} ${results.length}/${voices.length} ${id} status=${result.status} audio=${result.audioLen} ${result.message}`);
    }
  }
  await Promise.all(Array.from({ length: concurrency }, () => worker()));
  const failed = results.filter((item) => item.status !== 200 || item.audioLen < 100);
  const reportPath = require("path").join(__dirname, "..", "build");
  fs.mkdirSync(reportPath, { recursive: true });
  fs.writeFileSync(require("path").join(reportPath, "tts-voice-report.json"), JSON.stringify({
    total: results.length,
    ok: results.length - failed.length,
    failed
  }, null, 2));
  console.log(`DONE ok=${results.length - failed.length} failed=${failed.length}`);
  process.exit(failed.length ? 1 : 0);
}

main();
