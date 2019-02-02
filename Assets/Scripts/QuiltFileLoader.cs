using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Kirurobo;
using HoloPlay;

public class QuiltFileLoader : MonoBehaviour
{
    WindowController window;
    Texture2D texture;
    Quilt quilt;
    Quilt.Tiling defaultTiling;

    // Use this for initialization
    void Start()
    {
        window = FindObjectOfType<WindowController>();
        window.OnFilesDropped += Window_OnFilesDropped;

        quilt = FindObjectOfType<Quilt>();
        defaultTiling = quilt.tiling;   // Tilingの初期設定
    }

    void Update()
    {
        // [O] キーでファイル選択ダイアログを開く
        if (Input.GetKey(KeyCode.O)) {
            OpenFile();
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

        texture = www.texture;
        quilt.tiling = GetTilingType(texture);
        quilt.overrideQuilt = texture;
        quilt.SetupQuilt();
        Debug.Log(quilt.tiling.presetName + " # " + quilt.TilingPresetIndex);     // 選択されたTiling
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
