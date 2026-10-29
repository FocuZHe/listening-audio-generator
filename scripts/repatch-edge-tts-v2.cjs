#!/usr/bin/env node
/**
 * repatch-edge-tts-v2.cjs — 修复 listening-audio 的 Edge TTS 403
 *
 * 背景：微软 2025 年起把 Edge Read-Aloud 的鉴权参数 Sec-MS-GEC 升级为 v2
 *      （<CS64 校验和>-<SHA256>，哈希输入前面加一行版权串），
 *      并要求 Sec-MS-GEC-Version=2-<chromium版本> 与 ConnectionId。
 *      node-edge-tts 1.2.10 / 1.2.11 仍用 v1 → WebSocket 握手直接 403，
 *      页面能开、资源能加载，但点"生成"永远失败。
 *
 * 本脚本只做两件事（幂等，可反复执行）：
 *   1. 用 scripts/edge-tts-v2/drm.js 覆盖 node_modules/node-edge-tts/dist/drm.js
 *   2. 把 dist/edge-tts.js 的 URL 改成 Sec-MS-GEC-Version=2-...&ConnectionId=...
 * 不触碰 server.cjs / voice_map.json / data.json / nginx / 任何用户数据。
 *
 * 用法：
 *   node scripts/repatch-edge-tts-v2.cjs            # 打补丁 + 合成自检
 *   node scripts/repatch-edge-tts-v2.cjs --restart  # 额外重启 systemd 服务
 *   node scripts/repatch-edge-tts-v2.cjs --check    # 只检查不修改
 *   node scripts/repatch-edge-tts-v2.cjs --no-verify # 只打补丁，跳过联网自检（postinstall 用）
 */
'use strict';
const fs = require('fs');
const path = require('path');
const os = require('os');
const cp = require('child_process');

const ROOT = path.resolve(__dirname, '..');
const MOD = path.join(ROOT, 'node_modules', 'node-edge-tts', 'dist');
const DRM = path.join(MOD, 'drm.js');
const EDGE = path.join(MOD, 'edge-tts.js');
const GOOD_DRM = path.join(__dirname, 'edge-tts-v2', 'drm.js');

const args = process.argv.slice(2);
const CHECK_ONLY = args.includes('--check');
const RESTART = args.includes('--restart');
const SKIP_VERIFY = args.includes('--no-verify');

const MARK_V2 = 'Sec-MS-GEC v2';
let changed = false;

function fail(msg) { console.error('✗ ' + msg); process.exit(1); }
function ok(msg) { console.log('✓ ' + msg); }

if (!fs.existsSync(GOOD_DRM)) fail('缺少已知可用的 drm.js 模板：' + GOOD_DRM);
if (!fs.existsSync(DRM) || !fs.existsSync(EDGE)) {
  fail('找不到 node-edge-tts 模块，请先在 ' + ROOT + ' 执行 npm install');
}

// ---------- 1. drm.js ----------
const drmSrc = fs.readFileSync(DRM, 'utf8');
const drmPatched = drmSrc.includes(MARK_V2) && drmSrc.includes('cs64');
if (drmPatched) {
  ok('drm.js 已是 v2 算法，跳过');
} else if (CHECK_ONLY) {
  console.log('! drm.js 仍是 v1（会 403）');
  changed = true;
} else {
  const stamp = new Date().toISOString().replace(/[-:T]/g, '').slice(0, 12);
  const bak = DRM + '.bak-' + stamp;
  if (!fs.existsSync(bak)) fs.copyFileSync(DRM, bak);
  fs.copyFileSync(GOOD_DRM, DRM);
  ok('drm.js 已替换为 v2（备份 ' + path.basename(bak) + '）');
  changed = true;
}

// ---------- 2. edge-tts.js URL ----------
let edgeSrc = fs.readFileSync(EDGE, 'utf8');
const urlOk = edgeSrc.includes('Sec-MS-GEC-Version=2-') && edgeSrc.includes('ConnectionId=');
if (urlOk) {
  ok('edge-tts.js URL 已是 v2 + ConnectionId，跳过');
} else if (CHECK_ONLY) {
  console.log('! edge-tts.js URL 仍是旧参数');
  changed = true;
} else {
  const stamp = new Date().toISOString().replace(/[-:T]/g, '').slice(0, 12);
  const bak = EDGE + '.bak-' + stamp;
  if (!fs.existsSync(bak)) fs.copyFileSync(EDGE, bak);

  let s = edgeSrc;
  // 版本前缀 1- → 2-（仅动 URL 里的那一处）
  s = s.replace(/Sec-MS-GEC-Version=1-\$\{drm_1\.CHROMIUM_FULL_VERSION\}/,
                'Sec-MS-GEC-Version=2-${drm_1.CHROMIUM_FULL_VERSION}');

  // 补 ConnectionId 参数
  if (!s.includes('ConnectionId=')) {
    s = s.replace(/Sec-MS-GEC-Version=2-\$\{drm_1\.CHROMIUM_FULL_VERSION\}`/,
                  'Sec-MS-GEC-Version=2-${drm_1.CHROMIUM_FULL_VERSION}&ConnectionId=${connectionId}`');
  }

  // 声明 connectionId（放在 WebSocket 构造之前）
  if (!/const connectionId\s*=/.test(s)) {
    s = s.replace(/(\n\s*)const wsConnect = new ws_1\.WebSocket\(/,
                  '$1const connectionId = require(\'node:crypto\').randomUUID().replace(/-/g, \'\');$1const wsConnect = new ws_1.WebSocket(');
  }

  if (!(s.includes('Sec-MS-GEC-Version=2-') && s.includes('ConnectionId=${connectionId}'))) {
    fail('edge-tts.js 结构不符合预期，未修改。请人工比对 ' + EDGE);
  }
  fs.writeFileSync(EDGE, s);
  ok('edge-tts.js URL 已改为 v2 + ConnectionId（备份 ' + path.basename(bak) + '）');
  changed = true;
}

if (CHECK_ONLY) {
  console.log(changed ? '\n结果：补丁不完整，请运行 node scripts/repatch-edge-tts-v2.cjs' : '\n结果：补丁完整');
  process.exit(changed ? 1 : 0);
}

if (SKIP_VERIFY) {
  console.log('已跳过合成自检（--no-verify）。补丁完成，启动服务后即可使用。');
  process.exit(0);
}

// ---------- 3. 合成自检 ----------
const probe = path.join(os.tmpdir(), 'edge_tts_selfcheck_' + Date.now() + '.mp3');
try {
  const { EdgeTTS } = require(path.join(ROOT, 'node_modules', 'node-edge-tts'));
  const tts = new EdgeTTS({ voice: 'zh-CN-XiaoxiaoNeural', lang: 'zh-CN', rate: '+0%' });
  tts.ttsPromise('合成自检', probe).then(() => {
    const size = fs.existsSync(probe) ? fs.statSync(probe).size : 0;
    try { fs.unlinkSync(probe); } catch (e) {}
    if (size > 2000) {
      ok('自检合成成功，' + size + ' 字节');
      if (RESTART) {
        const r = cp.spawnSync('sudo', ['-n', 'systemctl', 'restart', 'listening-audio'], { encoding: 'utf8' });
        if (r.status === 0) ok('已重启 listening-audio');
        else console.error('! 重启失败（需 sudo 权限）：' + ((r.stderr || '').trim().split('\n')[0] || ''));
      } else {
        console.log('提示：服务还在用旧模块，生效需重启：sudo systemctl restart listening-audio（或加 --restart）');
      }
    } else {
      fail('自检合成没拿到音频（' + size + ' 字节），可能微软又改协议了');
    }
  }).catch((e) => fail('自检合成失败：' + (e && e.message ? e.message : e)));
} catch (e) {
  fail('无法加载 node-edge-tts：' + e.message);
}
