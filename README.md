# LookingGlassQuiltViewer
A tiled (quilt) image viewer for the Looking Glass

### Sample quilt image
<img src="https://github.com/kirurobo/LookingGlassQuiltViewer/blob/master/Assets/StreamingAssets/example01.png" width="256" alt="Sample quilt image">


# これは何？

[The Looking Glass](https://lookingglassfactory.com/)用の、Quiltと呼んでいるタイル状の画像を表示するアプリです。

Windows (x64) または macOS で動作します。

公式の [Lightfield Photo App](https://lookingglassfactory.com/devtools/lightfield-photo-app) に近いものです。

## 公式の LightFieldApp との違い
- ○　タイルの配置を（既定のものの中から）自動判別します
- ×　Exif情報に埋め込まれたタイル配置は読み取れません
- ×　決まったタイル配置しか使えません
- ×　自動判別に失敗した場合に手動で枚数指定はできません
- ○　画像を表示するだけならおそらく手軽です
- ○　Windowsなら画像やフォルダをドロップして開けます
- ○　他のアプリがアクティブでも、Looking Glass 前面のボタンで操作できます
- ○　オープンソースです


## ファイルの開き方について

### ダイアログからファイルを開いた場合、または1つだけファイルをドロップした場合
カーソルキー左右またはLooking Glass前面の左右ボタンを押すと、同じフォルダにある前/次の画像を探し出して表示します。

後からファイルが増えたらそれも対象になります。

スライドショー時も同様に、その都度検索されます。


### 複数のファイル、またはフォルダをドロップした場合
選ばれたファイルが左右キーでの切り替え、スライドショーの対象となります。

後からフォルダ内に画像が増えても、それは対象になりません。


### バックグラウンドでのLooking Glass前面ボタン操作
Windows では SharpDX.DirectInput を使ってバックグラウンドでも操作を受け付けるようにしています。

Assets/DirectInputButtonListerner 以下をコピーし、ButtonManager の代わりに使うことで、この LookingGlassQuiltViewer 以外でも同様に使えるはずです。たぶん。


## 設定の保存について

アプリ終了時、Unity の PlayerPrefs を使って設定および最後に開いていたファイルのパスが保存されます。

次に起動すると自動で読み込まれます。

設定を消去したい場合は下記を削除してください。
- Windows： レジストリの HKEY_CURRENT_USER\Software\Kirurobo\LookingGlassQuiltViewer
- macOS： ユーザーフォルダ下の Library/Preferences/unity.Kirurobo.LookingGlassQuiltViewer.plist


# System requirements
- [The Looking Glass](https://lookingglassfactory.com/)
- Windows 10 x64 or macOS


# License

## LookingGlassQuiltViewer
Copyright (c) 2019-2020 Kirurobo
Released under the MIT License  
https://github.com/kirurobo/LookingGlassQuiltViewer/blob/master/LICENSE


## HoloPlay SDK [(Link)](https://docs.lookingglassfactory.com/Unity/)
LICENSE 
[(PDF)](https://github.com/kirurobo/LookingGlassQuiltViewer/blob/master/Assets/HoloPlay/License.pdf)  
Copyright 2017-18 Looking Glass Factory Inc. All rights reserved.


## UnityStandaloneFileBrowser [(Link)](https://github.com/gkngkc/UnityStandaloneFileBrowser)
Copyright (c) 2017 Gökhan Gökçe  
Released under the MIT License  
https://github.com/gkngkc/UnityStandaloneFileBrowser/blob/master/LICENSE.txt  


## HoloPlayButtonListener
Copyright (c) 2020 Kirurobo
Released under the MIT License  
https://github.com/kirurobo/LookingGlassQuiltViewer/blob/master/Assets/ButtonListener/LICENSE.txt


## SharpDX
Copyright (c) 2010-2015 SharpDX - Alexandre Mutel
Released under the MIT License  
https://github.com/kirurobo/LookingGlassQuiltViewer/blob/master/Assets/ButtonListener/LICENSE.txt
