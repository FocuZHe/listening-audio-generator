# 听力音频生成器

给英语试卷做听力音频。把题目录进去，点一次，就能得到一整条连续的 WAV，直接放给学生听。

语音用的是微软 Edge 在线朗读，免费，不需要 API Key。电脑需要能上网。

适合中小学英语老师做单元卷、期中期末卷的听力部分。

这是最初的正式版本。发布页上只有这一版，里面是两个分开的 Windows 程序。

## 下载

请按自己的电脑**单独下载其中一个**，不要把两个程序再打进同一个压缩包。

下载地址：<https://github.com/FocuZHe/listening-audio-generator/releases/latest>

| 文件 | 图标 | 打开方式 | 适合的系统 |
| --- | --- | --- | --- |
| `listening-audio-generator.exe` | 喇叭加一个小地球 | 用系统浏览器打开页面 | Windows 10、Windows 11。电脑上要有浏览器。不需要 WebView2 |
| `listening-audio-generator-webview.exe` | 喇叭加一个小窗口 | 在自己的窗口里显示，界面和网页版一样，不会另外弹出浏览器 | Windows 11，以及 Windows 10 1803（2018 年 4 月更新）及以后。Windows 7、Windows 8 不能用 |

窗口版在 Windows 11 上一般可以直接打开。Windows 10 需要已经安装 Microsoft Edge，或安装 [WebView2 运行库](https://developer.microsoft.com/microsoft-edge/webview2/)。如果缺这个组件，程序会提示下载地址。

两个程序的默认音色都是：

- 男声：Microsoft WilliamMultilingual Online (Natural)
- 女声：Microsoft Natasha Online (Natural) - English (Australia)
- 旁白 / 中文：Microsoft Xiaoxiao Online (Natural) - Chinese (Mainland)

一般不用改。

## 打开之后怎么用

浏览器版和窗口版的页面是同一套。

1. **选一份材料。** 左上角默认是「听力材料」。要另做一套卷子，点「新建听力材料」，起个名字，比如「五年级上册期中」。
2. **加大题。** 点「立即新建大题」或「添加新听力大题部分」。点铅笔图标，填写大题标题和指导语，再点保存。不保存的话，播放时不会念这个标题。
3. **加小题。**
   - 「增加单句题」：同一句先由男声读一遍，停一下，再由女声读一遍。
   - 「增加对话题」：按每句旁边的男声 / 女声读完一遍，停一下，再把整段对话读第二遍。
   - 题号前面的引导语默认是 `Number 1.`，可以改成别的，留空就不念。
   - 单句和对话可以来回切换。切换时只保留你已经写的文字，不会自动塞进示例句子。空白台词会被跳过。
4. **写开场白和结束语。** 在「试卷听力广播语」里填写。结束语默认是「听力部分到此结束，请接着完成笔试部分。」留空就不会念。
5. **试听。** 每道题左边的播放按钮只听这一题。上方「播放完整考试录音」会从开场白一直念到结束语。点左侧大纲只是跳到那一道大题，不会改成从中间开始播。
6. **导出。** 点「导出整卷高清晰音频」。窗口版会先让你选择保存位置；浏览器版会下载到浏览器的默认下载目录，文件名是当前材料的名字。题目多时要等一会儿，不要关掉程序。

还可以调节：

- **语速**：默认 0.9 倍。点「0.85x」会切到五年级常用速度。
- **停顿**：男女声之间、对话句子之间、每题答完后的留白。可以直接输入小数，例如 `1.5`。
- **叮咚**：每题开始前是否先响一声。

## 卷子保存在哪里

两个 exe 都会在**程序旁边**生成 `data.json`，题目、语速、停顿和音色都写在这个文件里。把 exe 拷到别的文件夹时，把旁边的 `data.json` 一起拷走，卷子才会跟着走。

窗口版还会在旁边生成 `webview-profile` 文件夹，那是窗口自己的页面数据，不要删。

`data.json` 是你自己的试卷，不要发给别人，也不要上传到网上。朗读内容会发给微软的公开朗读服务，不要用来读敏感内容。

## 从源码运行

只有要改程序，或在 macOS、Linux 上使用时，才需要这一节。普通使用下载上面的 exe 即可。

需要 [Node.js 18](https://nodejs.org/) 或更高版本。Windows 安装时勾选 **Add to PATH**。装好后在命令行输入 `node -v`，能看到版本号即可。

1. 克隆或下载本仓库，放到任意位置。整个文件夹都可以拷走。
2. Windows 双击 **`一键启动.bat`**。macOS / Linux 在这个文件夹里执行 `bash 一键启动.sh`。
3. 第一次会自动安装依赖，需要联网。以后再打开就直接启动。
4. 浏览器会打开 <http://localhost:3000> 。用完后关掉黑色命令行窗口，服务就停了。

也可以手动执行：

```bash
npm install
npm start
```

端口被占用时可以换一个。Windows 命令行：

```bat
set PORT=3080
一键启动.bat
```

PowerShell：`$env:PORT = "3080"; .\一键启动.bat`  
macOS / Linux：`PORT=3080 bash 一键启动.sh`

从源码运行时，题目主要在浏览器里，同时会在项目文件夹生成 `data.json` 作为备份。换浏览器后如果看不到卷子，只要 `data.json` 还在，用同一个地址打开通常能再同步回来。

## 遇到问题

**窗口版打不开，提示缺少 WebView2**  
这是 Windows 10 上缺组件。安装 Microsoft Edge，或到 <https://developer.microsoft.com/microsoft-edge/webview2/> 下载 Evergreen 运行库。Windows 7 和 Windows 8 请改用浏览器版；浏览器版也需要 Windows 10 或 Windows 11。

**页面能开，但试听或导出失败**  
先确认电脑能上网。导出慢是正常的，每一句都要单独向微软请求语音，再拼成一条音频。

从源码运行时，如果微软更新了朗读接口，在项目文件夹里执行：

```bash
npm run check:tts
npm run patch:tts
```

第一条是检查，第二条会重新打补丁，并真的合成一句中文来确认。然后再启动。

**双击 `一键启动.bat` 后提示没有 Node.js**  
去 <https://nodejs.org/> 安装 18 或更高版本，重新打开命令行，再双击一次。

## 文件夹里有什么

```
一键启动.bat                 Windows：安装依赖并启动网页
一键启动.sh                  macOS / Linux：安装依赖并启动网页
server.cjs                   网页和语音接口
dist/                        已经打包好的页面
voice_map.json               音色名称和 Edge 音色编号的对照表
desktop-webview/             窗口版源码
data.json                    运行后出现，保存你的试卷，不进仓库
```

想增加音色时，在 `voice_map.json` 里加一行 `"页面上显示的名字": "xx-XX-XxxNeural"`，保存后重启。音色编号可以在微软的 [语音列表](https://learn.microsoft.com/azure/ai-services/speech-service/language-support) 里查。

## 放到学校服务器上

适合已经会用 Linux 的人。把文件夹拷到服务器后：

```bash
cd /opt/listening-audio
npm install --omit=dev
sudo cp deploy/listening-audio.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now listening-audio
```

前面再加一层网站反代时，用 `deploy/nginx.conf.example`。网站必须挂在域名根路径上，例如 `https://school.example.com/`，不要挂到 `/listening/` 这样的子目录，否则页面样式和脚本会找不到。反代超时建议放到 300 秒。

## 给开发者的接口

| 方法 | 路径 | 作用 |
| --- | --- | --- |
| GET | `/api/health` | 服务是否还活着 |
| GET | `/api/voices` | 全部音色 |
| GET | `/api/data` | 读取已保存的试卷和设置 |
| POST | `/api/data` | 保存试卷或设置。只传要改的字段，其余保留 |
| POST | `/api/tts` | 合成一句。内容为 `{text, voiceName, role, speed}`，返回 `{audio: base64 的 MP3}` |

`role` 用 `male`、`female` 或 `narrator`。`speed` 是倍数，例如 `0.9`。`voiceName` 用 `voice_map.json` 里的显示名。

页面文件在 `dist/` 里，是打包结果，这个仓库没有前端源码。`server.cjs` 可以直接改。试听和导出都走 Edge，不需要 API Key。

## 许可

[MIT](LICENSE)

本工具只是调用微软公开的 Edge 朗读接口，与 Microsoft 无关。请遵守对方的服务条款，不要把语音服务拿去转售。
