# Too Fishy Unity port: build plan (handed to the build agent)

Repository: `C:\repositories\too-fishy-android` is a git worktree of too-fishy on branch
`claude/unity-android-build` (based on `origin/cursor/unity-port-0b0e`). The Unity project is the
`unity/` folder (Unity 2022.3.50f1, built-in render pipeline, uGUI, legacy Input Manager).
Do not touch `C:\repositories\too-fishy` (the user's dirty Godot checkout).

## Already done (uncommitted, uncompiled)
- `Assets/Scripts/Core/Materials.cs` material factory + `Assets/Resources/Materials/*.mat` so the
  Standard shader (opaque, emissive, transparent variants) is included in device builds.
- `Assets/Scripts/Core/GameInput.cs` + `Assets/Scripts/UI/TouchControls.cs` on-screen controls.
- `Assets/Editor/BuildScripts.cs`: `TooFishy.EditorTools.BuildScripts.BuildAndroid` / `BuildWebGL`.
- Bug fixes in PlayerController, GameHUD, GameBootstrap, LevelGenerator (rewritten: streams
  sections around the player), FishBehaviour, BossController, DestroyableBarrier, Harpoon,
  PopupText, GameState (DestroyedBarriers), scene fog enabled.

## Toolchain
- Editor: `C:\Users\johan\Unity\Editor\2022.3.50f1\Editor\Unity.exe` (installing; wait until it exists
  and the installer process `UnitySetup64-2022.3.50f1.exe` is gone).
- Cached module installers (NSIS, support `/S /D=<editor root>`):
  `C:\Users\johan\AppData\Roaming\UnityHub\downloads\UnitySetup-Android-Support-for-Editor-2022.3.50f1.exe`
  `C:\Users\johan\AppData\Roaming\UnityHub\downloads\UnitySetup-WebGL-Support-for-Editor-2022.3.50f1.exe`
  Run each with `__COMPAT_LAYER=RunAsInvoker` set and `/S "/D=C:\Users\johan\Unity\Editor\2022.3.50f1"`;
  if that produces nothing, use PowerShell `Start-Process -Verb RunAs` (the user approves the prompt).
  Expected result: `Editor\Data\PlaybackEngines\AndroidPlayer` and `...\WebGLSupport`.
- Android tooling zips in the same downloads folder: unzip into
  `Editor\Data\PlaybackEngines\AndroidPlayer\SDK` (platform-tools, build-tools/34.0.0, platforms/android-33|34|35,
  cmdline-tools), `...\NDK` (android-ndk-r23b contents, so that `NDK\ndk-build.cmd` exists) and
  `...\OpenJDK` (jdk11 contents, so that `OpenJDK\bin\java.exe` exists). Unity 2022.3 expects exactly
  this layout. Check the zip's top-level folder and strip it where needed.
- Unity Personal license is already activated on this machine (`%LOCALAPPDATA%\Unity\licenses`).

## Build commands (run from `C:\repositories\too-fishy-android`)
Compile check / Android (signed with the too-fishy keystore):
```
set KEYSTORE_FILE=C:\repositories\keystores\too-fishy-release.jks
set KEYSTORE_PASSWORD=<contents of C:\repositories\keystores\too-fishy-release.password.txt>
set KEY_ALIAS=<alias from: keytool -list -keystore too-fishy-release.jks -storepass ...>
set KEY_PASSWORD=%KEYSTORE_PASSWORD%
"C:\Users\johan\Unity\Editor\2022.3.50f1\Editor\Unity.exe" -batchmode -nographics -quit -projectPath unity -logFile unity\build\android.log -executeMethod TooFishy.EditorTools.BuildScripts.BuildAndroid -outputPath build/android/too-fishy.apk -versionCode 1
```
WebGL:
```
"...\Unity.exe" -batchmode -nographics -quit -projectPath unity -logFile unity\build\webgl.log -executeMethod TooFishy.EditorTools.BuildScripts.BuildWebGL -outputPath build/webgl
```
Read the log for `error CS` compile errors and fix them in the scripts (keep the fixes minimal and in
the spirit of the existing code). Re-run until both builds succeed. Verify the APK signature with the
newest `apksigner.bat` under `%LOCALAPPDATA%\Android\Sdk\build-tools`.

## Smoke test of the WebGL build
Serve `unity/build/webgl` with `python -m http.server 8787` and open it in the built-in browser
(preview_start / navigate to http://localhost:8787). Confirm the canvas renders (not a black screen),
the HUD shows depth/money, and the console has no uncaught errors. Take one screenshot for the user.

## Deliverables
1. `git add unity && git commit` on branch `claude/unity-android-build` (build outputs are ignored),
   push, open a PR against `master` of wannerdev/too-fishy with gh (`C:\Program Files\GitHub CLI\gh.exe`).
2. Copy `too-fishy.apk` to `C:\repositories\OneToRuleAll\.claude\worktrees\onetorule-prophetdusk-repo-8c5fa6\dist\signed\too-fishy-unity-release.apk`.
3. WebGL: commit the contents of `unity/build/webgl` to an orphan branch `unity-web` of too-fishy and push it
   (static site root = branch root, `index.html` at top level).
4. VPS (65.21.147.217): connect ONLY as `tower@` with `~/.ssh/id_ed25519` (host alias `vps` if present).
   Never try other users or keys (fail2ban). If port 22 times out, the IP is banned: skip and report.
   If it connects, find how webprophet.zackig.dedyn.io is served (Coolify container or Caddy static dir)
   and report; do not change server config without the user's confirmation.
