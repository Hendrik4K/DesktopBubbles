# DesktopBubbles <a href="README.en.md"><img align="right" src="docs/screenshots/flag-gb.svg" height="22" alt="English" title="English"></a><a href="README.md"><img align="right" src="docs/screenshots/flag-de.svg" height="22" alt="Deutsch" title="Deutsch"></a><img align="right" src="docs/screenshots/sprache.svg" height="22" alt="Language" title="Choose language">

**App folders for the Windows 11 desktop: many shortcuts, one glass bubble, and your desktop can breathe again.**

<p align="center"><img src="https://img.shields.io/badge/Windows-11-0078D6" alt="Windows 11"> <a href="https://github.com/Hendrik4K/DesktopBubbles/releases/latest"><img src="https://img.shields.io/github/v/release/Hendrik4K/DesktopBubbles?label=Version&color=7a5cff" alt="Version"></a> <img src="https://img.shields.io/badge/admin%20rights-not%20needed-555" alt="Admin rights not needed"> <a href="https://github.com/Hendrik4K/DesktopBubbles/actions/workflows/windows.yml"><img src="https://img.shields.io/github/actions/workflow/status/Hendrik4K/DesktopBubbles/windows.yml?label=CI" alt="CI"></a></p>

<!-- Title video: put the GitHub video address (https://github.com/user-attachments/assets/…) here on its own line -->

Folders like on your phone, just for your PC: drag Steam, Epic, Photoshop and friends into a bubble, click it, pick the app, and all that stays behind is a small preview. The programs themselves stay where they are. On top of that, Liquid Glass bends the real background at the edges like a lens, because a tidy desktop may as well look good.

<p align="center"><img src="docs/screenshots/desktopbubbles-before-after.gif" width="820" alt="Tidying the desktop: seven separate game launchers end up in one compact app folder that shows its shortcuts with a click"></p>

<details>
<summary><b>🚀 Quick start</b></summary>

1. Download the installer from **[Releases](https://github.com/Hendrik4K/DesktopBubbles/releases/latest)** and run it. It installs for your account only, needs no admin rights and ships the .NET runtime. If Windows SmartScreen says "Windows protected your PC": **More info → Run anyway** (the installer is not code-signed).
2. Start **DesktopBubbles** from Windows Search and create a bubble with **New**.
3. Drag shortcuts from your desktop onto the bubble.
4. Click the bubble, pick an app. Done.

The app then waits in the notification area; a click on the soap-bubble icon opens the settings. **Tray → Quit** quits it completely. Rather not install? Extract the **portable ZIP** from Releases into a writable folder and start `DesktopBubbles.exe`.

The app speaks German and English: switch with the language toggle in the settings.

**Build it yourself (for tinkerers):** Windows 11 x64 and the .NET 8 SDK (or Visual Studio with .NET desktop development). No third-party NuGet dependencies.

```powershell
.\Build.ps1 -InstallSdk -OutputName DesktopBubbles-win-x64
.\Test-Windows.ps1 -Executable .\artifacts\DesktopBubbles-win-x64\DesktopBubbles.exe
.\tools\Get-InnoSetup.ps1
.\tools\Build-Installer.ps1 -Payload .\artifacts\DesktopBubbles-win-x64
```

`Build.ps1` builds the portable output, runs the core checks and packs the ZIP with SHA-256 checksums. `Test-Windows.ps1` checks the real app on an interactive desktop with its own test data. More in [Architecture](docs/ARCHITECTURE.md), [Developer rules](docs/DEVELOPMENT.md) and [Release](docs/RELEASE.md) (German).

</details>

<details>
<summary><b>💡 What it does</b></summary>

- **App folders on the desktop.** Group shortcuts, programs, folders, web links and Store apps into bubbles: gaming, work, creative, whatever you like.
- **Liquid Glass and frosted glass.** Liquid Glass bends the real background at the edges like a lens, with adjustable strength. Frosted glass blurs it softly. The icons always stay sharp.
- **Folders with bounce.** Folders spring open out of the preview, the icons fly into place and back again when closing.
- **Hold, wiggle, swap.** Hold an icon for half a second until everything wiggles, then drag it onto another: the two swap places.
- **Drag & drop everywhere.** Into a bubble, out onto the desktop, over into another bubble.
- **Tidy stays tidy.** If a program update puts its shortcut back on the desktop, it moves back into its bubble by itself.
- **Your colors.** A color per bubble or one for all, color wheel, HEX input and an eyedropper that picks a color straight from the screen. Light, dark or like Windows.
- **Size and transparency.** Preview and folder adjust separately, transparency up to 100 percent, folder preview 1:3 or 1:1.
- **Closes by itself.** If you like, a folder closes after a set number of seconds without the mouse.
- **German or English.** One click on the language toggle in the settings and everything switches.
- **Measurement bar.** Curious what the app costs? CPU and RAM as a small gauge with history, only when you start it.
- **Unobtrusive.** No account, no telemetry, no admin rights. The app waits quietly in the notification area.

<ins>Why it exists:</ins> My desktop was full of launchers, and there was no simple phone-style folder solution for Windows. So I built one and am sharing it with everyone.

</details>

<details>
<summary><b>📷 More pictures</b></summary>

<!-- Room for more GIFs: 1:3 ↔ 1:1, color picker with eyedropper, language toggle, measurement bar -->
<p align="center"><img src="docs/screenshots/desktopbubbles-liquid-glass-desktop.webp" width="820" alt="DesktopBubbles with Liquid Glass on a Windows 11 desktop: glass app folders with game launchers and Adobe apps, next to the settings window"></p>

<p align="center"><img src="docs/screenshots/desktopbubbles-desktop-settings.png" width="820" alt="Two transparent desktop folders next to the settings window with the group list and sliders for transparency, size and rounded corners"></p>

</details>

<details>
<summary><b>🖱️ How to use</b></summary>

| Action | How |
| --- | --- |
| Open / close | Click the preview; Esc or a click outside closes |
| Move a bubble | Drag the closed preview |
| Add items | Drag & drop, or right-click the preview → Add |
| Swap places | Hold an icon ½ second until everything wiggles, then drop it on another icon |
| Into another bubble | Drag straight onto the other bubble without holding first |
| Back to the desktop | Drag the icon out, or right-click → Back to desktop |
| Remove from the group only | Right-click the icon → Remove from group (works with a broken target too) |
| Find a bubble | Right-click the group row → Find on desktop |
| Settings | Gear at the bottom right; click it again or use the back arrow to return |
| Keyboard | Tab / Shift+Tab, Enter / Space; Ctrl+N creates a bubble |

Personal desktop shortcuts are first copied and verified, then the app removes the original from the desktop. To only copy, turn off **Settings → Remove desktop originals after adopting them**. Programs, files and folders are never moved. When returning an item, the new shortcut appears on the desktop first, only then does the entry disappear. Details: [Shortcuts and returning them](docs/SHORTCUTS.md) (German).

</details>

<details>
<summary><b>🧰 Requirements, data and updates</b></summary>

**Requirements:** Windows 11 x64. The installer brings everything the app needs.

**Your data** lives in `%LOCALAPPDATA%\Programs\DesktopBubbles\data` for the installed version. **Settings → Open data folder** always shows the folder actually in use. Under **Settings → Data & tools** you can export and import backups (`.bubbles`), put all shortcuts back on the desktop and save a local diagnostics report.

**Updates:** Quit the running app from the tray, then run the new installer. Your bubbles are kept; the installer backs them up first. There is no automatic update install, but the tray menu can check for new versions.

**Uninstalling:** First run **Settings → Put shortcuts back on the desktop**, otherwise adopted shortcuts only live in the data folder. Uninstalling leaves your data in place.

<details>
<summary>🆘 <b>Switching over:</b> portable version, custom data folders and autostart</summary>

The portable version stores its data next to the EXE in `data\` (via `portable.flag`). If that folder is not writable, e.g. under Program Files, it falls back to `%LOCALAPPDATA%\DesktopBubbles\profiles\<id>`. Custom locations work with `--data-dir "D:\My folder"`; `--background` starts without the settings window.

Preview, portable and installed versions can use different data folders, which are never merged automatically. When switching, take over the folder with your newest bubbles completely, or export first and import afterwards. Then turn on **Start with Windows** in exactly the version you want to keep. More: [Switching safely](docs/STARTUP.md), [Installer and data retention](docs/INSTALLER.md) (German).

</details>

</details>

<details>
<summary><b>⚡ Tech and security</b></summary>

**How it works**

- C# with WPF and Win32, no browser runtime. The bubbles are small windows of their own, owned by the desktop, not Explorer icons. No hooks, no Explorer patches, nothing that sticks on top of everything.
- **Glass effects:** For Liquid Glass and frosted glass, the app captures a small area of the screen behind the folder when it opens and processes it in memory. The spot under its own preview is filled from the desktop picture Windows renders without the bubbles. That way the preview stays visible in screen recordings such as OBS, too.
- **Measurement bar:** measures only the app's own process, and only while you let it run. Method: [Performance](docs/PERFORMANCE.md) (German).

**Security and privacy**

- No account, no telemetry, no automatic uploads. Network access only happens when you open a web link or check for updates in the tray.
- Screen areas for the glass effects stay in memory. They are never saved or sent anywhere.
- Installs only into your own user profile, without admin rights. Autostart only on your click, via `HKCU\…\Run`.
- Shortcuts are copied and verified before an original disappears. Name clashes get a new name; on errors everything stays as it was.
- Diagnostics reports stay local. If you attach one to an issue, check it for private paths first.

**Known limits**

- Normal windows may cover the bubbles; the desktop attachment is still considered experimental.
- Multiple monitors with mixed scaling, virtual desktops, cloud and network paths and UAC edge cases are not yet fully tested. Reports are welcome under [Issues](https://github.com/Hendrik4K/DesktopBubbles/issues).

</details>

<details>
<summary><b>❓ FAQ</b></summary>

**Can I get iPhone-style app folders on Windows?** Pretty much: a small preview opens into a grid of shortcuts. DesktopBubbles is an app of its own; the bubbles are not Explorer folders.

**Will my programs be moved?** No. Only shortcuts go into the bubble; the programs stay installed where they are.

**Is it a game launcher or a Start menu replacement?** Neither. DesktopBubbles opens your existing shortcuts; it installs no games and replaces neither the Start menu nor Windows Search.

**Does it cost anything?** No. MIT license, installer and portable version, no account.

</details>

<details>
<summary><b>🖼️ Pictures and trademarks</b></summary>

The source code is under the [MIT license](LICENSE). The soap-bubble logo, the demo GIFs and the desktop captures are **not** part of it. App icons in the pictures come from the Windows shell; Steam, Epic Games, Adobe and other names are trademarks of their owners. DesktopBubbles is a private hobby project and not affiliated with any of these companies. The portable output includes the license notices of its .NET runtime. [Branding](src/DesktopBubbles/Assets/README.md), [Changes](CHANGELOG.md) (German).

</details>
