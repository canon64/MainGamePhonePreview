# MainGamePhonePreview

KoikatsuSunshine 本編で、VR空間にスマホ型プレビュー画面を生成し、
静止画キャプチャと簡易動画キャプチャを行う BepInEx プラグインです。

## 対応環境

- KoikatsuSunshine
- BepInEx 5.x
- KKS_VR (VRGIN/OpenVR)
- .NET Framework 4.7.2 (`net472`)

## インストール

1. `MainGamePhonePreview.dll` を `BepInEx/plugins/MainGamePhonePreview/` に配置
2. 初回起動で同フォルダに `PhonePreviewSettings.json` が生成
3. 必要なら `PhonePreviewSettings.sample.json` を参考に調整

## 操作

- `Ctrl + R`: 設定JSONの再読込（ランタイム反映）
- Grip（既定: `k_EButton_Grip`）: プレビューを掴む/離す
- Summon（既定: 右手 `k_EButton_Axis0`）: プレイヤー前方へ再配置
- Shutter（既定: 右手 `k_EButton_Axis1`）:
  - 短押し: PNG撮影
  - 長押し（`VideoHoldSeconds` 以上）: 動画フレーム収録開始、離すと終了

## 設定ファイル

設定は `PhonePreviewSettings.json` で変更します。主な項目:

- 描画: `RenderWidth`, `RenderHeight`, `CameraFieldOfView`
- 全体位置: `WholeOffsetX/Y/Z`
- 召喚/初期配置: `SpawnOffset*`, `SpawnRotation*`, `Summon*`
- 画面: `PlateWidth`, `PlateHeight`, `DisplayCornerRadius`
- カメラ: `CameraOffset*`, `CameraRotation*`, `ShowCameraMarker`
- 本体モデル: `UseZipmodBodyModel`, `Body*`
- 撮影: `EnableShutter`, `ShotDirectory`, `ShotFilePrefix`
- 動画: `EnableVideoCapture`, `VideoFps`, `VideoDirectory`, `VideoAutoEncodeMp4`, `VideoFfmpegPath`

`Voice` 系の設定はありません。このプラグインは画面プレビュー/撮影用途です。

## ビルド

```bash
dotnet build MainGamePhonePreview.csproj -c Release
```

生成物:

- `bin/Release/net472/MainGamePhonePreview.dll`

## 出力ファイル

実行時にプラグインフォルダ配下へ出力されます。

- ログ: `MainGamePhonePreview.log`
- 静止画: `shots/`
- 動画フレーム・mp4: `videos/`

## 注意点

- `VideoAutoEncodeMp4=true` の場合は `ffmpeg` が必要です。
- JSON値は環境差（H/Actionシーン、VR姿勢、導入mod）で最適値が変わります。
- 公開リポジトリには実行時ログ・画像・動画を含めない運用を推奨します。
