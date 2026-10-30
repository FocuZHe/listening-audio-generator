# 听力音频生成器

给英语试卷做听力音频。把题目录进去，点一次，就能得到一整条连续的 WAV，直接放给学生听。

语音用的是微软 Edge 在线朗读，免费，不需要 API Key。电脑需要能上网。

适合中小学英语老师做单元卷、期中期末卷的听力部分。

## 下载

发布页里有两个文件，请按自己的需要**单独下载**。不要把它们再打进同一个压缩包。

- **不想安装任何环境**：下载 `听力音频生成器.exe`，双击即可。浏览器会打开页面。做好的试卷保存在这个 exe 旁边的 `data.json`。这份程序里带有完整音色表，发布前已逐个音色合成过一句，确认都能生成音频。
- **想自己运行网页服务**：下载 `听力音频生成器-网页服务.zip`，解压后按下面的「最快的用法」操作。

下载地址：<https://github.com/FocuZHe/listening-audio-generator/releases/latest>

## 先装好这个

只需要 [Node.js 18](https://nodejs.org/) 或更高版本。安装时请勾选 **Add to PATH**。

装好后，在命令行输入 `node -v`，能看到版本号就可以继续。

## 最快的用法

1. 下载或克隆这个文件夹，放到任意位置。整个文件夹都可以拷走，换一台装了 Node.js 的电脑也能用。
2. Windows：双击 **`一键启动.bat`**。  
   macOS / Linux：在这个文件夹里执行 `bash 一键启动.sh`。
3. 第一次会自动安装依赖，需要联网，等它跑完。以后再打开就直接启动。
4. 浏览器会自己打开 <http://localhost:3000> 。如果没有打开，就手动输入这个地址。
5. 用完后关掉黑色命令行窗口，服务就停了。

默认音色已经设好，一般不用改：

- 男声：Microsoft WilliamMultilingual Online (Natural)
- 女声：Microsoft Natasha Online (Natural) - English (Australia)
- 旁白 / 中文：Microsoft Xiaoxiao Online (Natural) - Chinese (Mainland)

## 页面上怎么出一套听力

按这个顺序做即可。

1. **选一份材料。** 左上角默认是「听力材料」。要另做一套卷子，点「新建听力材料」，起个名字，比如「五年级上册期中」。
2. **加大题。** 点「立即新建大题」或「添加新听力大题部分」。点铅笔图标，填写大题标题和指导语，再点保存。不保存的话，播放时不会念这个标题。
3. **加小题。**
   - 「增加单句题」：同一句先由男声读一遍，停一下，再由女声读一遍。
   - 「增加对话题」：按每句旁边的男声 / 女声读完一遍，停一下，再把整段对话读第二遍。
   - 题号前面的引导语默认是 `Number 1.`，可以改成别的，留空就不念。
   - 单句和对话可以来回切换。切换时只保留你已经写的文字，不会自动塞进示例句子。空白台词会被跳过，不会读出来。
4. **写开场白和结束语。** 在「试卷听力广播语」里填写。结束语默认是「听力部分到此结束，请接着完成笔试部分。」留空就不会念。
5. **试听。** 每道题左边的播放按钮只听这一题。上方「播放完整考试录音」会从开场白一直念到结束语。点左侧大纲只是跳到那一道大题，不会改成从中间开始播。
6. **导出。** 点「导出整卷高清晰音频」。浏览器会下载一个 WAV，文件名就是当前材料的名字。这一步会逐句向微软请求语音，题目多时要等一会儿，不要关窗口。

右侧可以调节：

- **语速**：默认 0.9 倍。点「0.85x」会切到五年级常用速度。
- **停顿**：男女声之间、对话句子之间、每题答完后的留白。可以直接输入小数，例如 `1.5`。
- **叮咚**：每题开始前是否先响一声。

## 做好的卷子存在哪里

题目主要保存在这台电脑的浏览器里，同时会在项目文件夹生成 `data.json` 作为备份。

- 换浏览器，或清空了浏览器数据，可能看不到原来的卷子。`data.json` 还在的话，用同一个地址打开通常能再同步回来。
- `data.json` 是你自己的试卷，不要发给别人，也不要上传到网上。
- 朗读内容会发给微软的公开朗读服务，不要用来读敏感内容。

## 遇到问题

**双击后提示没有 Node.js**  
去 <https://nodejs.org/> 安装 18 或更高版本，重开一次命令行，再双击 `一键启动.bat`。

**页面打不开**  
看黑色窗口是否还开着。地址是 <http://localhost:3000> 。如果 3000 端口被占用，可以换一个端口后再启动：

```bat
set PORT=3080
一键启动.bat
```

PowerShell：

```powershell
$env:PORT = "3080"
.\一键启动.bat
```

macOS / Linux：`PORT=3080 bash 一键启动.sh`

**能打开页面，但试听或导出失败**  
先确认电脑能上网。微软有时会更新朗读接口，导致安装好的组件失效。在项目文件夹里执行：

```bash
npm run check:tts
npm run patch:tts
```

第一条是检查，第二条会重新打补丁，并真的合成一句中文来确认。然后再双击启动。

**导出很慢**  
正常。每一句都要单独合成，再拼成一条音频。

## 不想双击脚本时

在项目文件夹里：

```bash
npm install
npm start
```

然后打开 <http://localhost:3000> 。Windows、macOS、Linux 都是这两条命令。

## 文件夹里有什么

```
一键启动.bat                 Windows 双击：安装依赖并启动
一键启动.sh                  macOS / Linux：安装依赖并启动
scripts/setup-and-run.cjs    上面两个脚本实际调用的程序
scripts/start.cjs            只启动服务，不重复安装
server.cjs                   网页和语音接口
dist/                        已经打包好的页面
voice_map.json               音色名称和 Edge 音色编号的对照表
data.json                    运行后自动出现，保存你的试卷，不进仓库
```

想增加音色时，在 `voice_map.json` 里加一行 `"页面上显示的名字": "xx-XX-XxxNeural"`，保存后重启服务。音色编号可以在微软的 [语音列表](https://learn.microsoft.com/azure/ai-services/speech-service/language-support) 里查。

## 放到学校服务器上

适合已经会用 Linux 的人。把文件夹拷到服务器后：

```bash
cd /opt/listening-audio
npm install --omit=dev
sudo cp deploy/listening-audio.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now listening-audio
```

服务文件里已经写了生产模式和 3000 端口。前面再加一层网站反代时，用 `deploy/nginx.conf.example`。网站必须挂在域名根路径上，例如 `https://school.example.com/`，不要挂到 `/listening/` 这样的子目录，否则页面样式和脚本会找不到。

反代超时建议放到 300 秒，因为导出整卷时单次请求可能比较久。

## 给开发者的接口

| 方法 | 路径 | 作用 |
| --- | --- | --- |
| GET | `/api/health` | 服务是否还活着 |
| GET | `/api/voices` | 全部音色 |
| GET | `/api/data` | 读取已保存的试卷和设置 |
| POST | `/api/data` | 保存试卷或设置。只传要改的字段，其余保留 |
| POST | `/api/tts` | 合成一句。内容为 `{text, voiceName, role, speed}`，返回 `{audio: base64 的 MP3}` |

`role` 用 `male`、`female` 或 `narrator`。`speed` 是倍数，例如 `0.9`。`voiceName` 用 `voice_map.json` 里的显示名。

页面文件在 `dist/` 里，是打包结果，这个仓库没有前端源码。`server.cjs` 可以直接改。

旧的配置里可能还有一项叫 `gemini` 的引擎。现在没有这个接口，试听和导出都走 Edge，不需要填写 API Key。

## 许可

[MIT](LICENSE)

本工具只是调用微软公开的 Edge 朗读接口，与 Microsoft 无关。请遵守对方的服务条款，不要把语音服务拿去转售。
