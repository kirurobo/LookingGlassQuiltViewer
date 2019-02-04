using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Kirurobo;
using HoloPlay;

public class QuiltFileLoader : MonoBehaviour
{
    WindowController window;
    Texture2D texture;
    Quilt quilt;
    Quilt.Tiling defaultTiling;

    public RenderTexture renderTexture;
    public GameObject holoplayCapture;
    public GameObject holoplayUiCamera;
    public Text messageText;

    bool isHoloPlayCaptureActivated = false;

    float messageClearTime = 0;     // メッセージを表示した場合、それを消去する時刻を入れておく

    // Use this for initialization
    void Start()
    {
        // HoloPlay Captureのオブジェクトを取得
        if (!holoplayCapture)
        {
            holoplayCapture = FindObjectOfType<Capture>().gameObject;
        }

        if (!holoplayUiCamera)
        {
            holoplayUiCamera = FindObjectOfType<ExtendedUICamera>().gameObject;
        }

        // ファイルドロップなどを扱うためのWindowControllerインスタンスを取得
        window = FindObjectOfType<WindowController>();
        window.OnFilesDropped += Window_OnFilesDropped;

        // Quiltのインスタンスを取得
        quilt = FindObjectOfType<Quilt>();
        defaultTiling = quilt.tiling;   // Tilingの初期設定を記憶しておく

        // バックグラウンド実行を無効にする
        Application.runInBackground = false;

        // フレームレートを下げる
        Application.targetFrameRate = 15;
    }

    void Update()
    {
        // [O] キーまたは右クリックでファイル選択ダイアログを開く
        if (Input.GetKey(KeyCode.O) || Input.GetMouseButton(1)) {
            OpenFile();
        }

        // [S] キーで現在の画面を保存
        if (Input.GetKey(KeyCode.S))
        {
            SaveFile();
        }

        // HoloPlayの処理が有効だったなら、無効にする
        if (isHoloPlayCaptureActivated)
        {
            holoplayCapture.SetActive(false);
            holoplayUiCamera.SetActive(false);

            isHoloPlayCaptureActivated = false;

            // バックグラウンド実行を無効に戻す
            Application.runInBackground = false;
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

    /// <summary>
    /// 現在の画面をPNGで保存
    /// </summary>
    private void SaveFile()
    {
        // 現在のRenderTextureの内容からTexture2Dを作成
        RenderTexture currentRenderTexture = RenderTexture.active;
        RenderTexture.active = renderTexture;
        Texture2D texture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.ARGB32, false);
        texture.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
        texture.Apply();
        RenderTexture.active = currentRenderTexture;

        // PNGに変換
        byte[] rawData = texture.EncodeToPNG();
        Object.Destroy(texture);

        // 日時を基にファイル名を決定
        string file = "LookingGlass_" + System.DateTime.Now.ToString("yyyyMMdd_hhmmss") + ".png";

        // 書き出し
        System.IO.File.WriteAllBytes(file, rawData);
        Debug.Log("Saved " + file);

        // 保存したというメッセージを表示
        if (messageText)
        {
            messageText.text = "Saved " + file;
            messageClearTime = Time.time + 5f;      // 5秒後にメッセージ消去とする
        }
    }

    /// <summary>
    /// ダイアログからファイルを開く
    /// </summary>
    private void OpenFile()
    {
        string path = window.ShowOpenFileDialog("Quilt images|*.png;*.jpg;*.jpeg");
        if (!string.IsNullOrEmpty(path))
        {
            StartCoroutine("LoadQuiltFile", path);
        }
    }

    /// <summary>
    /// ファイルがドロップされた時の処理
    /// </summary>
    /// <param name="files"></param>
    private void Window_OnFilesDropped(string[] files)
    {
        // 一時的にバックグラウンド実行を有効にする
        Application.runInBackground = true;

        if (files.Length > 0)
        {
            // 最初のファイルだけ読み込み
            StartCoroutine("LoadQuiltFile", files[0]);
        }
    }

    /// <summary>
    /// コルーチンでファイル読み込み
    /// </summary>
    /// <param name="file"></param>
    /// <returns></returns>
    IEnumerator LoadQuiltFile(string file)
    {
        Debug.Log(file);
        WWW www = new WWW(file);
        yield return www;

        // レンダリングのため、HoloPlayのオブジェクトを有効化
        holoplayCapture.SetActive(true);
        holoplayUiCamera.SetActive(true);

        // Quiltを読み込み
        texture = www.texture;
        quilt.tiling = GetTilingType(texture);
        quilt.overrideQuilt = texture;
        quilt.SetupQuilt();
        isHoloPlayCaptureActivated = true;
        Debug.Log("Estimaged tiling: " + quilt.tiling.presetName);     // 選択されたTiling
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
        int skip = 4;

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
            Debug.Log(tilingPresets[i].presetName + " : " + score[i]);

            if (minScore > score[i])
            {
                selectedIndex = i;
                minScore = score[i];
            }
        }
        return tilingPresets[selectedIndex];
    }
}
