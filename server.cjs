var __create = Object.create;
var __defProp = Object.defineProperty;
var __getOwnPropDesc = Object.getOwnPropertyDescriptor;
var __getOwnPropNames = Object.getOwnPropertyNames;
var __getProtoOf = Object.getPrototypeOf;
var __hasOwnProp = Object.prototype.hasOwnProperty;
var __copyProps = (to, from, except, desc) => {
  if (from && typeof from === "object" || typeof from === "function") {
    for (let key of __getOwnPropNames(from))
      if (!__hasOwnProp.call(to, key) && key !== except)
        __defProp(to, key, { get: () => from[key], enumerable: !(desc = __getOwnPropDesc(from, key)) || desc.enumerable });
  }
  return to;
};
var __toESM = (mod, isNodeMode, target) => (target = mod != null ? __create(__getProtoOf(mod)) : {}, __copyProps(
  // If the importer is in node compatibility mode or this is not an ESM
  // file that has been converted to a CommonJS file using a Babel-
  // compatible transform (i.e. "__esModule" has not been set), then set
  // "default" to the CommonJS "module.exports" for node compatibility.
  isNodeMode || !mod || !mod.__esModule ? __defProp(target, "default", { value: mod, enumerable: true }) : target,
  mod
));

// server.ts
var import_express = __toESM(require("express"), 1);
var import_path = __toESM(require("path"), 1);
var import_dotenv = __toESM(require("dotenv"), 1);
var import_fs = __toESM(require("fs"), 1);
var import_os = __toESM(require("os"), 1);
var import_node_edge_tts = require("node-edge-tts");
import_dotenv.default.config();
function appRootDir() {
  try {
    if (require("node:sea").isSea()) return import_path.default.dirname(process.execPath);
  } catch (e) {
  }
  return process.cwd();
}
function loadVoiceMap() {
  const voiceMapPath = import_path.default.join(appRootDir(), "voice_map.json");
  try {
    if (import_fs.default.existsSync(voiceMapPath)) {
      return JSON.parse(import_fs.default.readFileSync(voiceMapPath, "utf-8"));
    }
  } catch (e) {
  }
  const embedded = global.LISTENING_EMBEDDED_ASSETS;
  if (embedded && embedded.voiceMap) return embedded.voiceMap;
  throw new Error("找不到 voice_map.json");
}
async function startServer() {
  const app = (0, import_express.default)();
  const PORT = Number(process.env.PORT) || 3e3;
  app.use(import_express.default.json({ limit: "10mb" }));
  app.get("/api/health", (req, res) => {
    res.json({ status: "ok", time: (/* @__PURE__ */ new Date()).toISOString() });
  });
  app.get("/api/voices", (req, res) => {
    try {
      const data = loadVoiceMap();
      const groups = {};
      for (const [name, id] of Object.entries(data)) {
        const lang = id.split("-").slice(0, 2).join("-");
        if (!groups[lang]) groups[lang] = [];
        groups[lang].push({ name, id });
      }
      res.json({ voices: data, groups });
    } catch (err) {
      res.status(500).json({ error: "voice_map_load_failed", message: err.message });
    }
  });
  const dataFilePath = import_path.default.join(appRootDir(), "data.json");
  const ensureDataFile = () => {
    if (!import_fs.default.existsSync(dataFilePath)) {
      import_fs.default.writeFileSync(dataFilePath, JSON.stringify({ archives: [], config: null, current_archive_id: null }, null, 2));
    }
  };
  ensureDataFile();
  app.get("/api/data", (_req, res) => {
    try {
      ensureDataFile();
      const raw = import_fs.default.readFileSync(dataFilePath, "utf-8");
      const data = JSON.parse(raw);
      res.json({ success: true, data });
    } catch (err) {
      res.status(500).json({ success: false, error: err.message });
    }
  });
  app.post("/api/data", (req, res) => {
    try {
      ensureDataFile();
      const { archives, config, current_archive_id } = req.body;
      const existing = JSON.parse(import_fs.default.readFileSync(dataFilePath, "utf-8"));
      const updated = {
        archives: archives !== void 0 ? archives : existing.archives,
        config: config !== void 0 ? config : existing.config,
        current_archive_id: current_archive_id !== void 0 ? current_archive_id : existing.current_archive_id
      };
      const tmp = dataFilePath + ".tmp";
      import_fs.default.writeFileSync(tmp, JSON.stringify(updated, null, 2));
      import_fs.default.renameSync(tmp, dataFilePath);
      res.json({ success: true });
    } catch (err) {
      res.status(500).json({ success: false, error: err.message });
    }
  });
  app.post("/api/tts", async (req, res) => {
    const { text, voiceName, role, speed } = req.body;
    if (!text) {
      return res.status(400).json({ error: "text is required" });
    }
    const tempFilePath = import_path.default.join(import_os.default.tmpdir(), `edge_tts_${Date.now()}_${Math.random().toString(36).substring(2, 9)}.mp3`);
    try {
      console.log(`[TTS] Requesting high-fidelity Edge TTS for text: "${text.substring(0, 30)}..." [BrowserVoice: ${voiceName || "none"}, Role: ${role || "default"}, Speed: ${speed || 1}]`);
      let voice = "en-US-JennyNeural";
      let lang = "en-US";
      const hasChinese = /[\u4e00-\u9fa5]/.test(text);
      let voiceMap = {};
      try {
        voiceMap = loadVoiceMap();
      } catch (e) {
      }
      if (voiceName && voiceMap[voiceName]) {
        voice = voiceMap[voiceName];
        lang = voice.split("-").slice(0, 2).join("-");
        console.log(`[TTS] Voice map matched: "${voiceName}" \u2192 ${voice}`);
      } else if (voiceName) {
        const nameLower = voiceName.toLowerCase();
        if (hasChinese || nameLower.includes("zh-") || nameLower.includes("chinese") || nameLower.includes("china") || nameLower.includes("mainland") || nameLower.includes("hong kong") || nameLower.includes("taiwan")) {
          if (nameLower.includes("hk") || nameLower.includes("hong kong")) {
            lang = "zh-HK";
          } else if (nameLower.includes("tw") || nameLower.includes("taiwan")) {
            lang = "zh-TW";
          } else {
            lang = "zh-CN";
          }
        } else if (nameLower.includes("en-au") || nameLower.includes("australia")) {
          lang = "en-AU";
        } else if (nameLower.includes("en-gb") || nameLower.includes("united kingdom") || nameLower.includes("great britain") || nameLower.includes("british")) {
          lang = "en-GB";
        } else if (nameLower.includes("en-ca") || nameLower.includes("canada")) {
          lang = "en-CA";
        } else if (nameLower.includes("en-in") || nameLower.includes("india")) {
          lang = "en-IN";
        } else {
          lang = "en-US";
        }
        const namesMap = {
          andrew: "Andrew",
          natasha: "Natasha",
          xiaoxiao: "Xiaoxiao",
          yunxi: "Yunxi",
          yunjian: "Yunjian",
          yunyang: "Yunyang",
          huihui: "Xiaoxiao",
          // Legacy offline SAPI5 female -> Xiaoxiao Neural
          kangkang: "Yunxi",
          // Legacy offline SAPI5 male -> Yunxi Neural
          yaoyao: "Xiaoxiao",
          jenny: "Jenny",
          guy: "Guy",
          aria: "Aria",
          brian: "Brian",
          david: "Brian",
          // Windows default offline male -> Brian Neural
          zira: "Jenny",
          // Windows default offline female -> Jenny Neural
          hazel: "Sonia",
          // Windows UK default offline female -> Sonia Neural
          elena: "Elena",
          sonia: "Sonia",
          ryan: "Ryan",
          libby: "Libby",
          abby: "Abby",
          emma: "Emma",
          michelle: "Michelle",
          eric: "Eric",
          jacob: "Jacob",
          christopher: "Christopher",
          steffan: "Steffan",
          roger: "Roger",
          william: "William",
          liam: "Liam",
          clara: "Clara",
          oliver: "Oliver"
        };
        let matchedName = "";
        for (const key of Object.keys(namesMap)) {
          if (nameLower.includes(key)) {
            matchedName = namesMap[key];
            break;
          }
        }
        if (matchedName) {
          voice = `${lang}-${matchedName}Neural`;
        } else {
          if (lang === "zh-CN") {
            voice = role === "male" || nameLower.includes("male") ? "zh-CN-YunxiNeural" : "zh-CN-XiaoxiaoNeural";
          } else if (lang === "zh-HK") {
            voice = role === "male" || nameLower.includes("male") ? "zh-HK-WanLungNeural" : "zh-HK-HiuMaanNeural";
          } else if (lang === "zh-TW") {
            voice = role === "male" || nameLower.includes("male") ? "zh-TW-YunJheNeural" : "zh-TW-HsiaoChenNeural";
          } else if (lang === "en-AU") {
            voice = role === "male" || nameLower.includes("male") ? "en-AU-WilliamNeural" : "en-AU-NatashaNeural";
          } else if (lang === "en-GB") {
            voice = role === "male" || nameLower.includes("male") ? "en-GB-RyanNeural" : "en-GB-SoniaNeural";
          } else if (lang === "en-CA") {
            voice = role === "male" || nameLower.includes("male") ? "en-CA-LiamNeural" : "en-CA-ClaraNeural";
          } else if (lang === "en-IN") {
            voice = role === "male" || nameLower.includes("male") ? "en-IN-PrabhatNeural" : "en-IN-NeerjaNeural";
          } else {
            voice = role === "male" || nameLower.includes("male") ? "en-US-BrianNeural" : "en-US-JennyNeural";
          }
        }
      } else {
        if (hasChinese) {
          voice = "zh-CN-XiaoxiaoNeural";
          lang = "zh-CN";
        } else {
          if (role === "male") {
            voice = "en-US-BrianNeural";
            lang = "en-US";
          } else if (role === "female") {
            voice = "en-US-JennyNeural";
            lang = "en-US";
          } else {
            voice = "en-US-JennyNeural";
            lang = "en-US";
          }
        }
      }
      console.log(`[TTS] Selected Edge Neural Voice ID: ${voice} for requested voice: ${voiceName || "none"}`);
      let rate = "default";
      if (speed && typeof speed === "number" && speed !== 1) {
        const pct = Math.round((speed - 1) * 100);
        rate = pct >= 0 ? `+${pct}%` : `${pct}%`;
      }
      const tts = new import_node_edge_tts.EdgeTTS({
        voice,
        lang,
        rate
      });
      await tts.ttsPromise(text, tempFilePath);
      if (!import_fs.default.existsSync(tempFilePath)) {
        throw new Error("Synthesizer succeeded but no audio file was written");
      }
      const audioBuffer = import_fs.default.readFileSync(tempFilePath);
      const base64Audio = audioBuffer.toString("base64");
      import_fs.default.unlink(tempFilePath, (err) => {
        if (err) console.error("[TTS Cleanup Warning]:", err);
      });
      res.json({ audio: base64Audio });
    } catch (error) {
      console.error("[TTS Error]:", error);
      if (import_fs.default.existsSync(tempFilePath)) {
        try {
          import_fs.default.unlinkSync(tempFilePath);
        } catch (e) {
        }
      }
      res.status(500).json({
        error: "TTS_FAILED",
        message: error.message || "Failed to generate high-fidelity speech audio stream."
      });
    }
  });
  if (process.env.NODE_ENV !== "production") {
    const vite = await require("vite").createServer({
      server: { middlewareMode: true },
      appType: "spa"
    });
    app.use(vite.middlewares);
  } else {
    const embedded = global.LISTENING_EMBEDDED_ASSETS;
    if (embedded && embedded.files) {
      app.use((req, res, next) => {
        const file = embedded.files[req.path];
        if (!file) return next();
        res.type(file.type).send(file.body);
      });
      app.get("*", (req, res) => {
        const home = embedded.files["/index.html"];
        if (!home) return res.status(404).end();
        res.type("html").send(home.body);
      });
    } else {
      const distPath = import_path.default.join(appRootDir(), "dist");
      app.use(import_express.default.static(distPath));
      app.get("*", (req, res) => {
        res.sendFile(import_path.default.join(distPath, "index.html"));
      });
    }
  }
  app.listen(PORT, "0.0.0.0", () => {
    console.log(`[Server] Listening on http://localhost:${PORT} in ${process.env.NODE_ENV || "development"} mode`);
    try {
      if (require("node:sea").isSea()) openBrowser(`http://localhost:${PORT}`);
    } catch (e) {
    }
  });
}
function openBrowser(url) {
  const { spawn } = require("child_process");
  if (process.platform === "win32") {
    spawn("cmd", ["/c", "start", "", url], { detached: true, stdio: "ignore" }).unref();
    return;
  }
  const command = process.platform === "darwin" ? "open" : "xdg-open";
  spawn(command, [url], { detached: true, stdio: "ignore" }).unref();
}
startServer().catch((err) => {
  console.error("[Server Crash]:", err);
});
/**
 * @license
 * SPDX-License-Identifier: Apache-2.0
 */
