using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using Kirurobo;
using HoloPlay;
using SFB;

public class QuiltFileLoader : MonoBehaviour
{
    WindowController window;
    Texture2D texture;
    Quilt quilt;
    Quilt.Tiling defaultTiling;

    public Text messageText;

    /// <summary>
    /// 読み込み待ちならtrueにする
    /// </summary>
    bool isLoading = false;

    /// <summary>
    /// メッセージを表示した場合、それを消去する時刻[s]を入れる
    /// </summary>
    float messageClearTime = 0;

    /// <summary>
    /// スライドショー対象の指定ファイル。
    /// これが空ならば現在開いたファイルと同じディレクトリを探す。
    /// </summary>
    List<string> targetFiles = new List<string>();

    /// <summary>
    /// 現在表示されている画像ファイルのパス
    /// </summary>
    string currentFile;


    // Use this for initialization
    void Start()
    {
        // ファイルドロップなどを扱うためのWindowControllerインスタンスを取得
        window = FindObjectOfType<WindowController>();
        window.OnFilesDropped += Window_OnFilesDropped;

        // Quiltのインスタンスを取得
        quilt = FindObjectOfType<Quilt>();
        defaultTiling = quilt.tiling;   // Tilingの初期設定を記憶しておく

        // フレームレートを下げる
        Application.targetFrameRate = 15;

        // サンプルの画像を読み込み
        LoadFile(Path.Combine(Application.streamingAssetsPath, "startup.png"));
    }

    void Update()
    {
        // 操作できるのはファイル読み込み待ちでないときだけ
        if (!isLoading)
        {
            if (Input.GetKeyUp(KeyCode.Escape))
            {
                Debug.Log("ESC key pressed");
                Quit();
            }
            // [O] キーまたは右クリックでファイル選択ダイアログを開く
            if (Input.GetKeyDown(KeyCode.O) || Input.GetMouseButtonUp(1))
            {
                Debug.Log("Open key pressed");
                OpenFile();
            }

            // [S] キーで現在の画面を保存
            if (Input.GetKeyDown(KeyCode.S))
            {
                SaveFile();
            }

            //// [T] キーでウィンドウ透過
            //if (Input.GetKey(KeyCode.T))
            //{
            //    if (window)
            //    {
            //        window.isTransparent = !window.isTransparent;
            //    }
            //}

            // 前の画像
            if (Buttons.GetButton(ButtonType.LEFT) || Input.GetKey(KeyCode.LeftArrow))
            {
                ShowMessage("");    // ファイル名が表示されていれば消す
                LoadFile(GetNextFile(-1));
            }

            // 次の画像
            if (Buttons.GetButton(ButtonType.RIGHT) || Input.GetKey(KeyCode.RightArrow))
            {
                ShowMessage("");    // ファイル名が表示されていれば消す
                LoadFile(GetNextFile(1));
            }

            if (Buttons.GetButton(ButtonType.CIRCLE))
            {
                ShowMessage(currentFile);
            }
        }

        // メッセージを一定時間後に消去
        if (messageClearTime > 0)
        {
            if (messageClearTime < Time.time)
            {
                messageText.text = "";
                messageClearTime = 0;
            }
        }
    }
    
    private void Quit() {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>
    /// 一定時間で消えるメッセージを表示
    /// </summary>
    /// <param name="text"></param>
    private void ShowMessage(string text)
    {
        const float lifetime = 5f;  // 消去までの時間[s]

        if (messageText)
        {
            messageText.text = text;
            messageClearTime = Time.time + lifetime;
        }
    }

    /// <summary>
    /// 現在の画面をPNGで保存
    /// </summary>
    private void SaveFile()
    {
        // 現在のRenderTextureの内容からTexture2Dを作成
        RenderTexture renderTexture = RenderTexture.active;
        int w = Screen.width;
        int h = Screen.height;
        Texture2D texture = new Texture2D(w, h, TextureFormat.ARGB32, false);
        texture.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        texture.Apply();

        // PNGに変換
        byte[] rawData = texture.EncodeToPNG();
        Destroy(texture);

        // 日時を基にファイル名を決定
        string file = "LookingGlass_" + System.DateTime.Now.ToString("yyyyMMdd_hhmmss") + ".png";

        // 書き出し
        System.IO.File.WriteAllBytes(file, rawData);
        Debug.Log("Saved " + file);

        // 保存したというメッセージを表示
        ShowMessage("Saved " + file);
    }

    /// <summary>
    /// 画像を読み込み
    /// </summary>
    /// <param name="uri">Path.</param>
    private void LoadFile(string path) {
        if (string.IsNullOrEmpty(path)) return;

        isLoading = true;
        currentFile = path;

        string uri = new System.Uri(path).AbsoluteUri;
        Debug.Log("Loading: " + uri);
        StartCoroutine("LoadFileCoroutine", uri);
    }

    /// <summary>
    /// コルーチンでファイル読み込み
    /// </summary>
    /// <param name="file"></param>
    /// <returns></returns>
    IEnumerator LoadFileCoroutine(string file)
    {
        WWW www = new WWW(file);
        yield return www;

        // 前のtextureを破棄
        Destroy(texture);

        // Quiltを読み込み
        texture = www.texture;
        quilt.tiling = GetTilingType(texture);
        quilt.overrideQuilt = texture;
        quilt.SetupQuilt();

        // 念のため毎回GCをしてみる…
        System.GC.Collect();

        Debug.Log("Estimaged tiling: " + quilt.tiling.presetName);     // 選択されたTiling

        // 読み込み完了
        isLoading = false;
    }

    /// <summary>
    /// スライドショーでの次のファイルパスを返す
    /// </summary>
    /// <returns>path</returns>
    /// <param name="step">1なら１つ次、-1なら１つ前</param>
    private string GetNextFile(int step) {
        List<string> files;
        int currentIndex = 0;

        if (targetFiles.Count > 0) {
            // 対象ファイルが指定されている場合はそのリストをたどる
            currentIndex = targetFiles.IndexOf(currentFile);
            files = targetFiles;
        } else {
            // 対象ファイル指定なしならば、現在のファイルと同じディレクトリから一覧を取得
            //   利便性のため、毎回一覧を取得
            string directory = Path.GetDirectoryName(currentFile);
            string filename = Path.GetFileName(currentFile);
            files = new List<string>();

            string[] allFiles = Directory.GetFiles(directory);
            foreach (string path in allFiles) {
                string ext = Path.GetExtension(path).ToLower();
                if (ext == ".png" || ext == ".jpg" || ext == ".jpeg") {
                    files.Add(path);
                    if (Path.GetFileName(path) == filename) {
                        currentFile = path;
                    }
                    Debug.Log(path);
                }
            }

            files.Sort();
            currentIndex = files.IndexOf(currentFile);
            Debug.Log("Index: " + currentIndex);
        }

        Debug.Log("Current index: " + currentIndex + "  Step: " + step);
        int index = currentIndex + step;
        if ((currentIndex >= (files.Count - 1)) && (step > 0))
        {
            // 最後のファイル表示中にさらに次を押されたら、最初に送る
            index = 0;
        }
        else if ((currentIndex == 0) && (step < 0))
        {
            // 最初のファイル表示中にさらに前を押されたら、最後に送る
            index = files.Count - 1;
        }

        if (index < 0)
        {
            // インデックスが0より小さくなったら、先頭とする
            index = 0;
        }
        else if (index >= files.Count) {
            // インデックスがリストを超えたら、最後に送る
            index = files.Count - 1;
        } 
        return files[index];
    }

    /// <summary>
    /// ダイアログからファイルを開く
    /// </summary>
    private void OpenFile()
    {
        // OSX対応のためここでもフラグを立ててみる
        isLoading = true;
        // Standalone File Browserを利用
        var extensions = new[] {
                new ExtensionFilter("Image Files", "png", "jpg", "jpeg" ),
                new ExtensionFilter("All Files", "*" ),
            };
        //string[] files = StandaloneFileBrowser.OpenFilePanel("Open File", "", extensions, false);
        Debug.Log("Dialog opened. isLoading " + isLoading);
        StandaloneFileBrowser.OpenFilePanelAsync("Open image", "", extensions, false, OpenFileCallback);
        Debug.Log("Dialog opened");
    }

    private void OpenFileCallback(string[] files)
    {
        if (files.Length < 1)
        {
            isLoading = false;
            return;
        }

        string path = files[0];
        if (!string.IsNullOrEmpty(path))
        {
            LoadFile(path);
        }
        else
        {
            isLoading = false;
        }
    }

    /// <summary>
    /// ファイルがドロップされた時の処理
    /// </summary>
    /// <param name="files"></param>
    private void Window_OnFilesDropped(string[] files)
    {
        // 自分のウィンドウにフォーカスを与える
        window.Focus();

        if (files.Length == 1) {
            // 一つだけドロップの場合はスライドショーリストを消去
            targetFiles.Clear();

            // ファイルだけ読み込み
            LoadFile(files[0]);
        }
        else if (files.Length > 1)
        {
            // 複数のファイルがあれば、それらをスライドショー対象とする
            targetFiles.Clear();
            targetFiles.AddRange(files);
            targetFiles.Sort();

            // 最初のファイルだけ読み込み
            LoadFile(targetFiles[0]);
        }
    }

    /// <summary>
    /// 自己相関からタイル数を推定
    /// </summary>
    /// <param name="texture"></param>
    /// <returns></returns>
    private Quilt.Tiling GetTilingType(Texture2D texture)
    {
        List<Quilt.Tiling> tilingPresets = new List<Quilt.Tiling>();
        foreach (var preset in Quilt.tilingPresets)
        {
            if ((preset.quiltH == texture.height) && (preset.quiltW == texture.width))
            {
                // 画像サイズがプリセットのサイズと一致すれば候補とする
                tilingPresets.Add(preset);
            }
            else
            {
                // サイズが一致しなければ、そのtileX,tileYでサイズを合わせた候補を作成
                tilingPresets.Add(
                    new Quilt.Tiling(
                        "Custom " + preset.tilesX + "x" + preset.tilesY,
                        preset.tilesX, preset.tilesY,
                        texture.width, texture.height
                        ));
            }
        }

        // どれも候補に残らなければ初期指定のTilingにしておく
        if (tilingPresets.Count < 1)
        {
            return defaultTiling;
        }

        // テクスチャを配列に取得
        Color[] pixels = texture.GetPixels(0, 0, texture.width, texture.height);

        // Tiling候補ごとの自己相関を求める
        float[] score = new float[tilingPresets.Count];

        // 相関をとる周期の調整値。1だと全ピクセルについて相関をとるが遅い。
        int skip = texture.width / 512;     // 4;

        int index = 0;
        foreach (var preset in tilingPresets)
        {
            score[index] = 0;
            for (int v = 0; v < preset.tileSizeY; v += skip)
            {
                for (int u = 0; u < preset.tileSizeX; u += skip)
                {
                    Color sum = Color.clear;
                    for (int y = 0; y < preset.tilesY; y++)
                    {
                        for (int x = 0; x < preset.tilesX; x++)
                        {
                            Color color = pixels[(y * preset.tileSizeY + v) * texture.width + (x * preset.tileSizeX + u)];
                            sum += color;
                        }
                    }
                    Color average = sum / preset.numViews;

                    Color variance = Color.clear;
                    for (int y = 0; y < preset.tilesY; y++)
                    {
                        for (int x = 0; x < preset.tilesX; x++)
                        {
                            Color color = pixels[(y * preset.tileSizeY + v) * texture.width + (x * preset.tileSizeX + u)];
                            Color diff = color - average;
                            variance += diff * diff;
                        }
                    }
                    score[index] += variance.r + variance.g + variance.b;
                }
            }
            index++;
        }

        // 最も相関が高かったプリセットを選択
        int selectedIndex = 0;
        float minScore = float.MaxValue;
        for (int i = 0; i < tilingPresets.Count; i++)
        {
            //Debug.Log(tilingPresets[i].presetName + " : " + score[i]);

            if (minScore > score[i])
            {
                selectedIndex = i;
                minScore = score[i];
            }
        }
        return tilingPresets[selectedIndex];
    }
}
