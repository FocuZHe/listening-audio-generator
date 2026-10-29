"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.generateSecMsGecToken = exports.TRUSTED_CLIENT_TOKEN = exports.CHROMIUM_FULL_VERSION = void 0;
const node_crypto_1 = require("node:crypto");
exports.CHROMIUM_FULL_VERSION = '143.0.3650.96';
exports.TRUSTED_CLIENT_TOKEN = '6A5AA1D4EAFF4E9FB37E23D68491D6F4';
const WINDOWS_FILE_TIME_EPOCH = 11644473600n;
// Sec-MS-GEC v2 (Microsoft, 2025+): "<CS64 checksum>-<SHA256>" of Edge's
// copyright line followed by the v1 input (rounded win-file-time ticks + token).
const EDGE_COPYRIGHT = "Copyright (C) Microsoft Corporation. All rights reserved.";
const SWAP_FIRST_WORD = { swappedWord: 0xEF0569FB, swappedSum: 0x76F50F34, swappedProduct: 0xC3EFEA97 };
const SWAP_SECOND_WORD = { swappedWord: 0xC31713DB, swappedSum: 0x1881CE3C, swappedProduct: 0x35BD1EC9 };
const MIX_FIRST_WORD = [0xCF98B111, 0x87085B9F, 0x12CEB96D, 0x257E1D83];
const MIX_SECOND_WORD = [0xA27416F5, 0xD38396FF, 0x7C932B89, 0xBFA49F69];
function swapHalves(value) {
    return ((value << 16) | (value >>> 16)) >>> 0;
}
function swapTerm(word, key, m) {
    const sum = Math.imul(word, key) + Math.imul(swapHalves(word), m.swappedWord);
    const product = Math.imul(swapHalves(sum), m.swappedSum) >>> 0;
    return (Math.imul(swapHalves(product), m.swappedProduct) + product) >>> 0;
}
function swapAndMultiply(value, multipliers) {
    for (const m of multipliers) {
        value = Math.imul(swapHalves(value), m) >>> 0;
    }
    return value;
}
function cs64(message, digest) {
    const dv = new DataView(digest.buffer, digest.byteOffset, digest.byteLength);
    const firstKey = (dv.getUint32(0, true) | 1) >>> 0;
    const secondKey = (dv.getUint32(4, true) | 1) >>> 0;
    const padded = new Uint8Array(Math.ceil(message.length / 8) * 8);
    padded.set(message);
    const words = new DataView(padded.buffer);
    let swapSum = 0;
    let lastSwap = 0;
    let mix = 0;
    let mixSum = 0;
    for (let offset = 0; offset < padded.length; offset += 8) {
        const firstWord = words.getUint32(offset, true);
        const secondWord = words.getUint32(offset + 4, true);
        lastSwap = swapTerm(secondWord, secondKey, SWAP_SECOND_WORD);
        swapSum = (swapSum + swapTerm(firstWord, firstKey, SWAP_FIRST_WORD) + lastSwap) >>> 0;
        mix = swapAndMultiply(Math.imul(mix + firstWord, firstKey), MIX_FIRST_WORD);
        mixSum = (mixSum + mix) >>> 0;
        mix = swapAndMultiply(Math.imul(mix + secondWord, secondKey), MIX_SECOND_WORD);
        mixSum = (mixSum + mix) >>> 0;
    }
    const out = Buffer.alloc(8);
    out.writeUInt32LE((lastSwap ^ mix) >>> 0, 0);
    out.writeUInt32LE((swapSum ^ mixSum) >>> 0, 4);
    return out;
}
function generateSecMsGecToken() {
    const ticks = BigInt(Math.floor((Date.now() / 1000) + Number(WINDOWS_FILE_TIME_EPOCH))) * 10000000n;
    const roundedTicks = ticks - (ticks % 3000000000n);
    const message = Buffer.from(`${EDGE_COPYRIGHT}${roundedTicks}${exports.TRUSTED_CLIENT_TOKEN}`, "utf8");
    const digest = node_crypto_1.createHash("sha256").update(message).digest();
    return `${cs64(message, digest).toString("hex").toUpperCase()}-${digest.toString("hex").toUpperCase()}`;
}
exports.generateSecMsGecToken = generateSecMsGecToken;
