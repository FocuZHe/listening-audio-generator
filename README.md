# 听力音频生成器 · Listening Audio Generator

给英语试卷做听力音频的小工具：把题目按「归档 → 分卷 → 题」录进去，选好音色、语速、停顿和铃声，一键把**整卷**合成为一条连续的 WAV 音频，直接拿去放给学生听。

后端 Node.js（Express），语音合成走微软 Edge 的在线 Neural TTS（免费、无需 API Key），前端是 Vite 打包的单页应用。

> 适合中小学英语老师出单元卷、期中期末卷的听力部分。

---

## 功能

- **结构化录题**：归档（整套教材/学期）→ 分卷（大题，如「一、听录音选出单词」）→ 题目
  - `statement` 单句题：读一句，可重复 N 遍
  - `dialogue` 对话题：男女声交替朗读
- **每题自定义**：导入语（如 `Number 1.`）、重复遍数、句后停顿、对话后停顿、题后停顿
- **卷首/卷尾**：可设置开考提示语（`examHeader`）和结束语（`examFooter`）
- **提示铃声**：可开关
- **音色**：微软 Edge Neural TTS，中/英/日/韩/多语种上百个音色（见 `voice_map.json`），男女声分开配置
- **整卷导出**：前端逐题取音频 → 解码 → 按停顿拼接 → 输出单个连续 WAV
- **数据双保险**：浏览器 `localStorage` 为主，同时同步一份到服务器 `data.json`

---

## 快速开始

需要 **Node.js ≥ 18**（开发环境为 v22）。

```bash
git clone https://github.com/FocuZHe/listening-audio-generator.git
cd listening-audio-generator
npm install          # 会自动给 node-edge-tts 打上 Edge TTS v2 补丁
npm start            # Windows / macOS / Linux 均可，默认生产模式，端口 3000
```

然后打开 <http://localhost:3000> 。换端口时设置环境变量再启动，例如 `PORT=3080 npm start`（PowerShell：`$env:PORT=3080; npm start`）。

首次启动会自动生成空的 `data.json`，浏览器打开即用。

界面默认音色保持为：男声 `Microsoft WilliamMultilingual Online (Natural)`、女声 `Microsoft Natasha Online (Natural) - English (Australia)`、旁白 `Microsoft Xiaoxiao Online (Natural) - Chinese (Mainland)`。

---

## 目录结构

```
listening-audio-generator/
├── server.cjs                  # 服务入口（Express，含 /api/* 与静态托管）
├── dist/                       # 前端构建产物（Vite 打包，直接由 Express 托管）
│   ├── index.html
│   └── assets/index-*.js|css
├── voice_map.json              # 「浏览器音色名 → Edge Neural 音色 ID」映射表
├── scripts/
│   ├── start.cjs               # 跨平台启动：未设置 NODE_ENV 时使用 production
│   ├── repatch-edge-tts-v2.cjs # Edge TTS 403 幂等修复脚本（见下方说明）
│   └── edge-tts-v2/drm.js      # Sec-MS-GEC v2 令牌实现
├── deploy/
│   ├── listening-audio.service # systemd 示例
│   └── nginx.conf.example      # nginx 反代示例
├── package.json
└── LICENSE
```

---

## HTTP 接口

| 方法 | 路径 | 说明 |
| --- | --- | --- |
| GET | `/api/health` | 健康检查，返回 `{status:"ok", time}` |
| GET | `/api/voices` | 返回全部音色映射，以及按语言分组的列表 |
| GET | `/api/data` | 读取服务器端数据（`archives` / `config` / `current_archive_id`） |
| POST | `/api/data` | 覆盖写入上述三个字段（只传要改的字段，其余保留） |
| POST | `/api/tts` | 合成单条语音，body：`{text, voiceName, role, speed}`，返回 `{audio: <base64 MP3>}` |

`role` 可取 `male` / `female` / `narrator`；`speed` 为语速倍率（如 `0.9`）。
`voiceName` 传 `voice_map.json` 里的**中文描述名**（例如 `Microsoft Natasha Online (Natural) - English (Australia)`），服务端据此解析出真正的 Neural 音色 ID。

---

## 音色配置

`voice_map.json` 是一张扁平的映射表，键是界面上显示的浏览器音色名，值是 Edge TTS 的音色 ID：

```json
{
  "Microsoft WilliamMultilingual Online (Natural)": "en-AU-WilliamMultilingualNeural",
  "Microsoft Natasha Online (Natural) - English (Australia)": "en-AU-NatashaNeural",
  "Microsoft Xiaoxiao Online (Natural) - Chinese (Mainland)": "zh-CN-XiaoxiaoNeural"
}
```

想加音色：把 `"界面显示名": "xx-XX-XxxNeural"` 追加进去，重启服务即可。音色 ID 可在
`edge-tts --list-voices` 或微软 [Speech 音色列表](https://learn.microsoft.com/azure/ai-services/speech-service/language-support) 里查。

---

## ⚠️ Edge TTS 403：为什么装完还要打补丁

微软 2025 年起把 Edge Read-Aloud 的鉴权参数 `Sec-MS-GEC` 升级到了 **v2**：

- v1（旧）：`SHA256(roundedTicks + TRUSTED_CLIENT_TOKEN)`
- v2（新）：`<CS64 校验和16hex>-<SHA256 64hex>`，哈希输入前面多一行版权串，并要求
  `Sec-MS-GEC-Version=2-<chromium 版本>` 和 `ConnectionId`

而 npm 上的 `node-edge-tts@1.2.10 / 1.2.11` 仍在用 v1 → WebSocket 握手直接 **403**。
症状很有迷惑性：**页面能开、资源能加载、就是点「生成」永远失败**。

本仓库的处理方式：

```bash
npm install                       # postinstall 自动执行 --no-verify 打补丁
npm run check:tts                 # 体检：0=补丁完整，1=需要修
npm run patch:tts                 # 手动补丁 + 联网合成自检（会真合成一句中文验证）
```

脚本是**幂等**的，只改 `node_modules/node-edge-tts/dist/{drm.js,edge-tts.js}`，
原文件留 `*.bak-<时间戳>` 备份，不碰任何业务代码和用户数据。

> 注意：**每次 `npm install` / `npm ci` 都会把补丁冲掉**（所以有 postinstall 兜底）。
> 如果某天又出现 403，先跑 `npm run check:tts`，再跑 `npm run patch:tts`。
> 若微软再次改协议，只需要更新 `scripts/edge-tts-v2/drm.js`。

---

## 部署到服务器

```bash
sudo mkdir -p /opt/listening-audio
sudo cp -r ./* /opt/listening-audio/      # 或直接在该目录 npm install
cd /opt/listening-audio && sudo npm install --omit=dev
sudo cp deploy/listening-audio.service /etc/systemd/system/
sudo systemctl daemon-reload && sudo systemctl enable --now listening-audio
```

nginx 反代示例见 `deploy/nginx.conf.example`（**必须挂在根路径**，前端资源用的是绝对路径 `/assets/...`，
挂到子目录会 404）。语音合成是逐条请求微软服务，反代超时记得放宽到 300s。

---

## 数据与隐私

- 听力材料**不在仓库里**：`data.json` 已被 `.gitignore` 排除，它只存在于你自己的部署机器上
- 界面的主要数据源是浏览器 `localStorage`，服务器 `data.json` 是同步备份
- `.env`、`.env.local` 同样被忽略；本项目默认不需要任何 API Key
- 语音合成内容会发往微软 Edge 的公开朗读接口，不要用它处理敏感内容

---

## 已知限制

- **前端只提供构建产物**：`dist/` 是 Vite 打包后的文件，前端源码不在本仓库内，因此前端无法在此仓库直接二次开发（后端 `server.cjs` 是可读的 Node CJS，可直接改）。
- **`server.cjs` 是打包后的运行文件**，同样不是原始 TS 工程。
- **依赖微软在线服务**：断网无法合成；微软改协议时需重打补丁。
- 旧数据里可能还有 `engine: "gemini"` 字段，服务端没有 Gemini 接口，试听和导出都走 Edge TTS，不需要 API Key。
- 默认端口 `3000`。要用别的端口，设置环境变量 `PORT`，或在外层反代。

---

## License

[MIT](LICENSE)

本项目只是调用微软公开的 Edge 朗读接口，与 Microsoft 无关；请自行遵守其服务条款，勿用于商业转售语音服务。
