# 听力音频生成器

给英语试卷做听力音频。把题目录进去，导出一整条 WAV，放给学生听。

语音用微软 Edge 在线朗读，免费，不需要 API Key。电脑需要能上网。

## 下载

按自己的系统单独下载一个程序，不要把两个再打进同一个压缩包。

<https://github.com/FocuZHe/listening-audio-generator/releases/latest>

- **`listening-audio-generator.exe`**：用系统浏览器打开。适合 Windows 10 和 Windows 11，电脑上要有浏览器。
- **`listening-audio-generator-webview.exe`**：在自己的窗口里打开，界面与网页版相同，导出时可以选择保存位置。适合 Windows 11，以及 Windows 10 1803（2018 年 4 月更新）及以后。Windows 10 需要已安装 Microsoft Edge，或安装 [WebView2 运行库](https://developer.microsoft.com/microsoft-edge/webview2/)。Windows 7 和 Windows 8 不能用这个窗口版。

默认音色不用改：男声 William、女声 Natasha、旁白晓晓。

## 怎么用

1. 左上角选择或新建一份材料。
2. 新增大题，点铅笔填写标题和指导语，然后保存。
3. 增加单句题或对话题，填上文字。单句由男声、女声各读一遍；对话按角色读完后，整段再读第二遍。引导语默认是 `Number 1.`，留空就不念。
4. 需要的话填写开场白和结束语。结束语默认是「听力部分到此结束，请接着完成笔试部分。」
5. 可以只试听一道题，也可以播放整卷。点左侧大纲只是跳到对应大题。
6. 点「导出整卷高清晰音频」。窗口版会先选择保存位置；浏览器版保存到浏览器的下载目录。题目多时请稍等。

语速默认 0.9 倍，可点「0.85x」。停顿可以输入小数。每题前的叮咚可以关掉。

## 卷子存在哪里

程序旁边的 `data.json` 里保存题目和设置。拷走程序时把这个文件一起拷走。窗口版旁边的 `webview-profile` 不要删。

`data.json` 是你自己的试卷，不要外传。朗读内容会发给微软，不要用来读敏感内容。

## 部署到服务器

这一节是给管理员的。程序装在一台服务器上，老师和学生用自己的电脑打开网页。Windows 7、Windows 8、Windows 10、Windows 11、Mac 和手机都可以，只要浏览器能打开那个地址。语音合成在服务器上完成，所以服务器必须能访问微软。

服务器用 Linux，需要 [Node.js 18](https://nodejs.org/) 或更高版本。

```bash
sudo mkdir -p /opt/listening-audio
sudo cp -r . /opt/listening-audio
cd /opt/listening-audio
sudo npm install --omit=dev
sudo cp deploy/listening-audio.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now listening-audio
```

服务只在服务器本机的 3000 端口接收请求。别人不要打开 `localhost`。同一局域网里的人打开 `http://服务器IP:3000`。

放到公网时，用 Nginx 把域名指到这个端口，示例是 `deploy/nginx.conf.example`。站点必须在域名根路径，例如 `https://listening.school.edu/`，不要挂到 `/listening/` 下面。反代超时设为 300 秒，整卷导出时单次请求会比较久。配好之后，别人打开的是这个域名，不是 3000 端口。

试卷保存在服务器上的 `/opt/listening-audio/data.json`。换端口时修改 service 文件里的 `PORT`，然后 `sudo systemctl restart listening-audio`。

试听失败时，先在服务器上确认能访问微软。如果是朗读接口更新导致的，在 `/opt/listening-audio` 里执行 `sudo npm run check:tts`，再执行 `sudo npm run patch:tts`，然后重启服务。

## 许可

[MIT](LICENSE)。本工具调用微软公开的 Edge 朗读接口，与 Microsoft 无关。
