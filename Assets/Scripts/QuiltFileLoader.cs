using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using Kirurobo;
using LookingGlass;
using SFB;
using UnityEngine.Video;
using UnityEngine.XR;
using UnityEngine.InputSystem;
using System.Linq;
using System.Text.RegularExpressions;

public class QuiltFileLoader : MonoBehaviour
{
    /// <summary>
    /// ファイル名とフォルダ名の表示モード
    /// </summary>
    public enum FileInfoMode
    {
        None = 0,           // 表示しない
        WhenChanged = 1,    // ロード直後のみ表示
        Always = 2,         // 常に表示
    }

    /// <summary>
    /// スライドショー切替時間の選択肢 [s]
    /// </summary>
    private int[] slideShowTimes =
    {
        0,      // 自動めくりをしない
        5,
        10,
        20,
        30,
        60,
    };

    /// <summary>
    /// PlayerPrefsで使うキー
    /// </summary>
    private static class PrefItems
    {
        public static string SlideShowInterval = "SlideShowInterval";
        public static string FileInfoMode = "FileInfoMode";
        public static string StartupFilePath = "StartupFilePath";
    }

    WindowController window;
    Texture2D texture;
    VideoPlayer videoPlayer;
    RenderTexture videoRenderTexture;
    Holoplay holoplay;
    Quilt.Settings defaultTiling;

    HoloPlayButtonListener buttonListener;      // DirectInputによりバックグラウンドでもボタン取得

    public TextMesh messageText;                // メッセージ表示用のText
    public TextMesh fileInfoText;               // ファイル名等表示用のText
    public GameObject prevIndicator;            // 前のファイルへ移動時に表示するオブジェクト
    public GameObject nextIndicator;            // 次のファイルへ移動時に表示するオブジェクト

    private TextMesh messageTextShadow;         // メッセージ表示用のTextの影
    private TextMesh fileInfoTextShadow;        // ファイル名等表示用のTextの影

    public int frameRateForStill = 10;          // 静止画表示時のフレームレート指定 [fps]
    public int frameRateForMovie = 60;          // 動画再生時のフレームレート指定 [fps]

    public float fileInfoLifeTime = 5f;         // ファイル名の自動消去時間 [s]
    public int slideShowInterval = 0;           // スライドショーの切替時間 [s] 0だと切替なし

    // ファイル名表示モード
    public FileInfoMode fileInfoMode = FileInfoMode.WhenChanged;

    static readonly string[] imageExtensions = { "png", "jpg", "jpeg", "jfif" };
    static readonly string[] movieExtensions = { "mp4", "webm", "mov", "avi" };

    /// <summary>
    /// 読み込み待ちならtrueにする
    /// </summary>
    bool isLoading = false;

    /// <summary>
    /// カーソルが元々表示されているか
    /// </summary>
    bool isCursorVisible = true;

    /// <summary>
    /// メッセージを表示した場合、それを消去する時刻[s]をもつ
    /// </summary>
    float messageClearTime = 0;

    /// <summary>
    /// ファイル情報を表示した場合、それを消去する時刻[s]をもつ
    /// </summary>
    float fileInfoClearTime = 0;

    /// <summary>
    /// スライドショー対象の指定ファイル。
    /// これが空ならば現在開いたファイルと同じディレクトリを探す。
    /// </summary>
    List<string> targetFiles = new List<string>();

    /// <summary>
    /// 現在表示されている画像ファイルのパス
    /// </summary>
    string currentFile;

    /// <summary>
    /// この時刻をすぎると次のスライドへ移る
    /// </summary>
    float nextSlideTime = 0f;

    /// <summary>
    /// これがtrueなら終了時にPlayerPrefsの設定を消去する
    /// </summary>
    bool willSettingsReset = false;

    /// <summary>
    /// デフォルト画像のパス
    /// </summary>
    string defaultImagePath
    {
        get { return Path.Combine(Application.streamingAssetsPath, "startup.png"); }
    }


    /// <summary>
    /// 保存された設定を読み込む
    /// </summary>
    private void LoadSettings()
    {
        slideShowInterval = PlayerPrefs.GetInt(PrefItems.SlideShowInterval);
        fileInfoMode = (FileInfoMode)PlayerPrefs.GetInt(PrefItems.FileInfoMode);
        string path = PlayerPrefs.GetString(PrefItems.StartupFilePath);
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            LoadFile(path);
        }
    }

    /// <summary>
    /// 現在の設定を保存
    /// </summary>
    private void SaveSettings()
    {
        PlayerPrefs.SetInt(PrefItems.SlideShowInterval, slideShowInterval);
        PlayerPrefs.SetInt(PrefItems.FileInfoMode, (int)fileInfoMode);
        PlayerPrefs.SetString(PrefItems.StartupFilePath, currentFile);
    }

    /// <summary>
    /// 保存されている設定をすべて消去
    /// </summary>
    private void DeleteSettings()
    {
        PlayerPrefs.DeleteAll();
    }

    /// <summary>
    /// TextMeshを複製して影にする
    /// </summary>
    private void InitializeTextShadow()
    {
        if (messageText)
        {
            messageTextShadow = GameObject.Instantiate<TextMesh>(messageText);
            messageTextShadow.transform.parent = messageText.transform;
            messageTextShadow.transform.localPosition += new Vector3(0.05f, -0.04f, 0.01f);
            messageTextShadow.color = Color.black;
            messageText.GetComponent<MeshRenderer>().sortingOrder += 1;
        }
        if (fileInfoText)
        {
            fileInfoTextShadow = GameObject.Instantiate<TextMesh>(fileInfoText);
            fileInfoTextShadow.transform.parent = fileInfoText.transform;
            fileInfoTextShadow.transform.localPosition += new Vector3(0.05f, -0.04f, 0.01f);
            fileInfoTextShadow.color = Color.black;
            fileInfoText.GetComponent<MeshRenderer>().sortingOrder += 1;
        }
    }

    // Use this for initialization
    void Start()
    {
        // ファイルドロップなどを扱うためのWindowControllerインスタンスを取得
        window = FindObjectOfType<WindowController>();
        window.OnFilesDropped += Window_OnFilesDropped;

        // Quiltのインスタンスを取得
        holoplay = FindObjectOfType<Holoplay>();
        defaultTiling = holoplay.quiltSettings;
        //defaultTiling = Quilt.GetPreset(Quilt.Preset.Custom, holoplay.cal);   // Tilingの初期設定を記憶しておく
        holoplay.background = new Color(0, 0, 0, 0);             // 背景は透明にする

        // VideoPlayerのインスタンスを取得
        videoPlayer = FindObjectOfType<VideoPlayer>();
        if (videoPlayer)
        {
            //videoRenderTexture = new RenderTexture(4096, 4096, 24);
            videoRenderTexture = new RenderTexture(4096, 4096, 32);
            videoPlayer.targetTexture = videoRenderTexture;

            videoPlayer.seekCompleted += VideoPlayer_seekCompleted;
        }

        // フレームレートを指定
        Application.targetFrameRate = frameRateForStill;

        // 操作に対する表示は非表示にしておく
        if (nextIndicator) nextIndicator.SetActive(false);
        if (prevIndicator) prevIndicator.SetActive(false);

        // TextMeshがあれば複製して影とする
        InitializeTextShadow();

        // サンプルの画像を読み込み
        LoadFile(defaultImagePath);

        // メッセージ欄を最初に消去
        ShowMessage("");
        ShowFileInfo("");

        // 保存された設定を読込
        LoadSettings();

        // スライドショーが行われるならその間隔を最初に表示
        if (slideShowInterval > 0)
        {
            ShowMessage("Slideshow: " + slideShowInterval + " s");
        }

        // バックグラウンドでのボタン管理
        SetupButtonListener();

        // カーソルを表示するか否かを記憶
        isCursorVisible = Cursor.visible;
    }

    /// <summary>
    /// バックグラウンドでも操作できるようButtonManagerは使わず、独自クラスでボタン処理
    /// </summary>
    private void SetupButtonListener()
    {
        buttonListener = new HoloPlayButtonListener();
        buttonListener.OnkeyDown += ButtonListener_OnkeyDown;
    }

    /// <summary>
    /// Looking Glass のボタンが押されたときに呼ばれる
    /// </summary>
    /// <param name="button"></param>
    private void ButtonListener_OnkeyDown(HoloPlayButtonListener.HoloPlayButton button)
    {
        if (isLoading) return;      // ローディング中なら処理はしない

        switch (button)
        {
            case HoloPlayButtonListener.HoloPlayButton.Left:
                ShowMessage("");                // ファイル名が表示されていれば消す
                LoadFile(GetNextFile(-1));      // 前のファイルを開く
                break;

            case HoloPlayButtonListener.HoloPlayButton.Right:
                ShowMessage("");                // ファイル名が表示されていれば消す
                LoadFile(GetNextFile(1));       // 次のファイルを開く
                break;

            case HoloPlayButtonListener.HoloPlayButton.Circle:
                ToggleFileInfoMode();           // ファイル名表示モード切替
                break;

            case HoloPlayButtonListener.HoloPlayButton.Square:
                ToggleSlideShowInterval();      // スライドショー間隔切替
                break;
        }
    }

    /// <summary>
    /// 毎フレームの処理
    /// </summary>
    void Update()
    {
        buttonListener.Update();    // バックグラウンドでもボタン状態取得

        // 操作できるのはファイル読み込み待ちでないときだけ
        if (!isLoading)
        {
            // [Esc] キーで終了
            if (Input.GetKeyUp(KeyCode.Escape))
            {
                Quit();
            }

            // [O] キーまたは右クリックでファイル選択ダイアログを開く
            if (Input.GetKeyDown(KeyCode.O) || Input.GetMouseButtonUp(1))
            {
                OpenFile();
            }

            // [S] キーを押されたタイミングで、スクリーンショットのためカーソルや情報を非表示に
            if (Input.GetKeyDown(KeyCode.S))
            {
                ShowMessage("");
                ShowFileInfo("");
                Cursor.visible = false;
            }
            // [S] キーが離されたタイミングで現在の画面を保存。カーソルを写さないため非表示化とタイミングをずらす
            if (Input.GetKeyUp(KeyCode.S))
            {
                SaveFile();
            }

            // [R] キーでPlayerPrefsに保存された設定を消去し、デフォルトの画像を表示
            if (Input.GetKeyDown(KeyCode.R))
            {
                DeleteSettings();

                // デフォルトの画像を読み込み
                LoadFile(defaultImagePath);

                // 終了時に削除されるようにする
                ShowFileInfo("History has been removed");
                ShowMessage("Settings will be reset on quit");
                willSettingsReset = true;
            }

            // 前の画像
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                ShowFileInfo("");    // ファイル名が表示されていれば消す
                LoadFile(GetNextFile(-1));
            }

            // 次の画像
            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                ShowFileInfo("");    // ファイル名が表示されていれば消す
                LoadFile(GetNextFile(1));
            }

            // 開かれているファイル名を表示
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                ToggleFileInfoMode();
            }

            UpdateSlideShow();
        }

        // 左ボタンが押されていることを表示
        if (prevIndicator) prevIndicator.SetActive(Input.GetKey(KeyCode.LeftArrow) || buttonListener.GetKey(HoloPlayButtonListener.HoloPlayButton.Left));

        // 右ボタンが押されていることを表示
        if (nextIndicator) nextIndicator.SetActive(Input.GetKey(KeyCode.RightArrow) || buttonListener.GetKey(HoloPlayButtonListener.HoloPlayButton.Right));

        //UpdateVideo();

        UpdateMessage();
        UpdateFileInfo();
    }

    private void UpdatePreview2D()
    {
        // Looking Glass が見つかっていなければ、強制的に preview2D を有効とする
        if (holoplay && !holoplay.preview2D)
        {
            if (holoplay.cal.serial == "")
            {
                holoplay.preview2D = true;
            }
        }
    }

    /// <summary>
    /// スライドショーの自動めくり処理
    /// </summary>
    private void UpdateSlideShow()
    {
        // 間隔が0sなら何もしない
        if (slideShowInterval <= 0)
        {
            nextSlideTime = Mathf.Infinity;
            return;
        }

        // 画像切替時刻をすぎていたら次の画像に移動
        if (Time.time >= nextSlideTime)
        {
            // 次の画像を読み込み
            LoadFile(GetNextFile(1));
        }
    }
    
    /// <summary>
    /// 終了処理
    /// </summary>
    private void Quit() {
#if UNITY_EDITOR
        // エディタ上なら、再生を終了
        UnityEditor.EditorApplication.isPlaying = false;
#else
        // スタンドアローンなら、アプリケーションを終了
        Application.Quit();
#endif
    }

    /// <summary>
    /// 終了時に設定を保存
    /// </summary>
    private void OnApplicationQuit()
    {
        if (willSettingsReset)
        {
            DeleteSettings();
        }else { 
            SaveSettings();
        }
    }

    /// <summary>
    /// 動画再生中ならばテクスチャを更新
    /// </summary>
    private void UpdateVideo()
    {
        if (videoPlayer && videoPlayer.isPlaying && texture)
        {
            // 強制的に描画？
            holoplay.RenderQuilt();
        }
    }

    /// <summary>
    /// メッセージを指定時刻に消す
    /// </summary>
    private void UpdateMessage()
    {
        if (messageClearTime > 0)
        {
            if (messageClearTime < Time.time)
            {
                if (messageText)
                {
                    messageText.text = "";
                    messageTextShadow.text = "";
                }
                messageClearTime = 0;
            }
        }
    }

    /// <summary>
    /// 一定時間で消えるメッセージを表示
    /// </summary>
    /// <param name="text">メッセージ文字列</param>
    /// <param name="lifetime">消えるまでの時間[s]</param>
    private void ShowMessage(string text, float lifetime = 5f)
    {
        if (messageText)
        {
            messageText.text = text;
            messageTextShadow.text = Regex.Replace(text, "<color=#[0-9A-Fa-f]+>", "<color=#000000>");
            messageClearTime = Time.time + lifetime;
        }
    }

    /// <summary>
    /// メッセージを指定時刻に消す
    /// </summary>
    private void UpdateFileInfo()
    {
        if (fileInfoClearTime > 0)
        {
            if (fileInfoClearTime < Time.time)
            {
                if (fileInfoText)
                {
                    fileInfoText.text = "";
                    fileInfoTextShadow.text = "";
                }
                fileInfoClearTime = 0;
            }
        }
    }

    /// <summary>
    /// 一定時間で消えるファイル情報表示
    /// </summary>
    /// <param name="text">メッセージ文字列</param>
    /// <param name="lifetime">消えるまでの時間[s]</param>
    private void ShowFileInfo(string text, float lifetime = 5f)
    {
        if (fileInfoText)
        {
            fileInfoText.text = text;
            fileInfoTextShadow.text = Regex.Replace(text, "<color=#[0-9A-Fa-f]+>", "<color=#000000>");
            fileInfoClearTime = Time.time + lifetime;
        }
    }

    /// <summary>
    /// ファイル名を表示
    /// </summary>
    /// <param name="path"></param>
    private void ShowFilename(string path)
    {
        if (fileInfoMode == FileInfoMode.None)
        {
            // 表示しないならメッセージ消去
            ShowFileInfo("");
            return;
        }

        string dir = Path.GetFileName(Path.GetDirectoryName(path));
        string file = Path.GetFileName(path);

        ShowFileInfo(
            "<size=40><color=#FFFFFF>" + file + "</color></size>"
            + System.Environment.NewLine
            + "<size=30><color=#00FF00>" + dir + "</color></size>"
            , (fileInfoMode == FileInfoMode.Always ? Mathf.Infinity : fileInfoLifeTime)
            );
    }

    /// <summary>
    /// ファイル名表示モード切替
    /// </summary>
    private void ToggleFileInfoMode()
    {
        switch (fileInfoMode)
        {
            case FileInfoMode.WhenChanged:
                fileInfoMode = FileInfoMode.Always;
                ShowMessage("File name: Always show");
                break;

            case FileInfoMode.Always:
                fileInfoMode = FileInfoMode.None;
                ShowMessage("File name: Hide");
                break;

            default:
                fileInfoMode = FileInfoMode.WhenChanged;
                ShowMessage("File name: On loaded");
                break;

        }
        ShowFilename(currentFile);
    }

    /// <summary>
    /// スライドショーの間隔を変更
    /// </summary>
    private void ToggleSlideShowInterval()
    {
        if (slideShowInterval >= slideShowTimes[slideShowTimes.Length - 1])
        {
            // 選択肢の最後またはそれより大きい値だったら0に戻す
            slideShowInterval = 0;

            ShowMessage("Slideshow: OFF");
        }
        else
        {
            // 最大でなければ、今の次に大きな値にする
            foreach (int val in slideShowTimes)
            {
                if (val > slideShowInterval)
                {
                    slideShowInterval = val;
                    ShowMessage("Slideshow: " + val + " s");

                    break;
                }
            }
        }

        // 次の画像の時刻を再設定
        nextSlideTime = Time.time + slideShowInterval;
    }

    /// <summary>
    /// 現在の画面をPNGで保存
    /// </summary>
    private void SaveFile()
    {
        StartCoroutine(SaveFileCoroutine());
    }

    /// <summary>
    /// フレーム描画後に画像を保存
    /// </summary>
    /// <returns></returns>
    private IEnumerator SaveFileCoroutine()
    {
        yield return new WaitForEndOfFrame();

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

        // カーソル表示を元に戻す
        Cursor.visible = isCursorVisible;
    }


    /// <summary>
    /// 画像を読み込み
    /// </summary>
    /// <param name="uri">Path.</param>
    private void LoadFile(string path) {
        if (string.IsNullOrEmpty(path)) return;
        if (!File.Exists(path)) return;

        isLoading = true;
        currentFile = path;

        // ファイル読み込み後に次の時刻は更新されるが、念のためここでも再設定
        nextSlideTime = Time.time + slideShowInterval;

        // 動画は停止し初期状態に戻す
        if (videoPlayer)
        {
            videoPlayer.targetTexture = videoRenderTexture;
            videoPlayer.Stop();
        }

        if (CheckMovieFile(path))
        {    // 動画を開く場合
            Application.targetFrameRate = frameRateForMovie;
            StartCoroutine("LoadMovieFileCoroutine", path);
        }
        else
        {   // 静止画を開く場合
            Application.targetFrameRate = frameRateForStill;

            // もし動画が再生されていれば停止しておく
            if (videoPlayer && videoPlayer.isPlaying)
            {
                videoPlayer.Stop();
            }

            string uri = new System.Uri(path).AbsoluteUri;
            //Debug.Log("Loading: " + uri);

            StartCoroutine("LoadImageFileCoroutine", uri);
        }
        
        // 強制2D化はファイルを開くタイミングでチェック
        //UpdatePreview2D();
    }

    /// <summary>
    /// コルーチンで画像ファイル読み込み
    /// </summary>
    /// <param name="uri"></param>
    /// <returns></returns>
    IEnumerator LoadImageFileCoroutine(string uri)
    {
        // 読み込み
        WWW www = new WWW(uri);
        yield return www;

        // 前のtextureを破棄
        Destroy(texture);

        // Quiltを読み込み
        texture = www.texture;
        holoplay.customQuiltSettings = GetTilingType(texture);
        holoplay.SetQuiltPreset(Quilt.Preset.Custom);
        holoplay.overrideQuilt = texture;

        holoplay.quiltRT.filterMode = FilterMode.Trilinear;
        holoplay.SetupQuilt();

        //Debug.Log("Estimaged tiling: " + holoplay.quiltSettings.numViews);     // 選択されたTiling

    
        // 念のため毎回GCをしてみる…
        System.GC.Collect();

        // 読み込めたらファイル名を表示
        ShowFilename(currentFile);

        // 次の画像にする時刻を設定
        nextSlideTime = Time.time + slideShowInterval;

        // フラグを読み込み完了とする
        isLoading = false;
    }


    /// <summary>
    /// コルーチンで動画ファイル読み込み
    /// </summary>
    /// <param name="uri"></param>
    /// <returns></returns>
    IEnumerator LoadMovieFileCoroutine(string uri)
    {
        ShowMessage("Loading the movie...   ");
        holoplay.overrideQuilt = null;     // 読み込み開始を伝えるため、前の画像は消してしまう

        // 動画を読み込み
        videoPlayer.url = uri;
        videoPlayer.Prepare();
        while (!videoPlayer.isPrepared)
        {
            yield return null;
        }

        // 前のtextureを破棄
        //Destroy(texture);

        yield return new WaitForSecondsRealtime(0.1f);
        videoPlayer.Play();

        ShowMessage("Loading the movie......", 0.5f);
        yield return new WaitForSecondsRealtime(0.5f);  // フレームが表示されそうな時間、強制的に待つ
        //Debug.Log("Play movie");

        // Seek
        videoPlayer.frame = 0;

        yield return new WaitForEndOfFrame();

        // 念のため読み込み毎にGCをしてみる…
        System.GC.Collect();

        // 読み込めたらファイル名を表示
        ShowFilename(currentFile);

        // スライドショー間隔より動画の時間が長ければ、次の画像にする時刻は再生時間だけ後に設定
        float duration = (videoPlayer.frameRate == 0 ? 0 : videoPlayer.frameCount / videoPlayer.frameRate);
        nextSlideTime = Time.time + (duration > slideShowInterval ? duration : slideShowInterval);

        // フラグを読み込み完了とする
        isLoading = false;
    }

    /// <summary>
    /// 動画の準備が整ったらタイル数推定を行って描画開始
    /// </summary>
    /// <param name="source"></param>
    private void VideoPlayer_seekCompleted(VideoPlayer source)
    {
        if (holoplay)
        {
            // 前のtextureを破棄
            Destroy(texture);

            // 最初のフレームを使ってタイル数の推定
            texture = new Texture2D(videoRenderTexture.width, videoRenderTexture.height);
            RenderTexture currentRenderTexture = RenderTexture.active;
            RenderTexture.active = videoRenderTexture;
            texture.ReadPixels(new Rect(0, 0, videoRenderTexture.width, videoRenderTexture.height), 0, 0);
            texture.Apply();
            RenderTexture.active = currentRenderTexture;

            holoplay.customQuiltSettings = GetTilingType(texture);
            holoplay.SetupQuilt();
            //Debug.Log("Estimaged tiling: " + holoplay.customQuiltSettings.numViews);     // 選択されたTiling

            holoplay.overrideQuilt = videoRenderTexture;
            holoplay.quiltRT.filterMode = FilterMode.Bilinear;
        }
    }

    /// <summary>
    /// 指定ディレクトリ内の画像をターゲットのリストに追加
    /// </summary>
    /// <param name="directory"></param>
    /// <param name="list"></param>
    private void AddTargetDirectory(string directory, ref List<string> list)
    {
        string[] allFiles = Directory.GetFiles(directory);
        foreach (string path in allFiles)
        {
            if (CheckImageFile(path) || CheckMovieFile(path))
            {
                list.Add(path);
            }
        }
    }

    /// <summary>
    /// 指定ファイルが対象となる画像かどうかを判別
    /// 現状、JPEGかPNGなら通す
    /// </summary>
    /// <param name="path">ファイルのパス</param>
    /// <returns>対象の形式ならtrue</returns>
    private bool CheckImageFile(string path)
    {
        // 先頭のピリオドは除去して小文字にした拡張子
        string ext = Path.GetExtension(path).Substring(1).ToLower();

        foreach (string extension in imageExtensions)
        {
            if (extension == ext) return true;
        }
        return false;
    }

    /// <summary>
    /// 指定ファイルが対象となる画像かどうかを判別
    /// 現状、JPEGかPNGなら通す
    /// </summary>
    /// <param name="path">ファイルのパス</param>
    /// <returns>対象の形式ならtrue</returns>
    private bool CheckMovieFile(string path)
    {
        // 先頭のピリオドは除去して小文字にした拡張子
        string ext = Path.GetExtension(path).Substring(1).ToLower();

        foreach (string extension in movieExtensions)
        {
            if (extension == ext) return true;
        }
        return false;
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
            files = new List<string>();
            AddTargetDirectory(directory, ref files);   // ディレクトリ内の画像一覧を取得

            if (files.Count < 1)
            {
                // ファイルが全く無かった場合はデフォルト画像を読込み
                files.Add(defaultImagePath);
                currentIndex = 0;
            }
            else
            {
                files.Sort();   // パスの順に並び替え
                currentIndex = files.IndexOf(currentFile);
                //Debug.Log("Index: " + currentIndex);
            }
        }

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
        // ロード中は不用意に操作されないようフラグを立てておく
        isLoading = true;

        // Standalone File Browserを利用
        var extensions = new[] {
                new ExtensionFilter("Image & Movie", imageExtensions.Concat(movieExtensions).ToArray()),
                new ExtensionFilter("Image Files", imageExtensions),
                new ExtensionFilter("Movie Files", movieExtensions ),
                new ExtensionFilter("All Files", "*" ),
            };
        //string[] files = StandaloneFileBrowser.OpenFilePanel("Open File", "", extensions, false);
        StandaloneFileBrowser.OpenFilePanelAsync("Open image", "", extensions, false, OpenFileCallback);
    }

    /// <summary>
    /// 非同期ファイルダイアログの完了時コールバック
    /// </summary>
    /// <param name="files"></param>
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

        // 表示対象リストを消去
        targetFiles.Clear();

        foreach (string path in files)
        {
            if (File.Exists(path))
            {
                // 画像ならば表示対象に追加
                if (CheckImageFile(path) || CheckMovieFile(path))
                {
                    targetFiles.Add(path);
                }
            }
            else if (Directory.Exists(path))
            {
                // フォルダならばその中の画像を表示対象に追加
                AddTargetDirectory(path, ref targetFiles);
            }
        }
        targetFiles.Sort();

        if (targetFiles.Count < 1) return;

        // 1ファイルだけ読み込み
        LoadFile(targetFiles[0]);

        // 指定ファイルが1つしかなければ、表示対象リストなしとして同一フォルダ内探索を行う。
        // そうでなければ表示対象のみのスライドショーとする
        if (targetFiles.Count == 1)
        {
            targetFiles.Clear();
        }
    }

    /// <summary>
    /// 自己相関からタイル数を推定する
    /// プリセットにあるパターン（4x6,4x8,5x9,6x8）および 6x10, 5x10-2 のどれかに限定
    /// </summary>
    /// <param name="texture"></param>
    /// <returns></returns>
    private Quilt.Settings GetTilingType(Texture2D texture)
    {
        // 縦横比でポートレイトかを判断
        var isPortrait = (holoplay.cal.screenHeight > holoplay.cal.screenWidth);

        // 8x6 では 4x6 でも高相関となるため例外的にチェック
        int index4x6 = -1;

        List<Quilt.Settings> tilingPresets = new List<Quilt.Settings>();
        foreach (var preset in Quilt.presets)
        {
            if (preset.viewColumns == 4 && preset.viewRows == 6)
            {
                index4x6 = tilingPresets.Count;
            }

            if ((preset.quiltHeight == texture.height) && (preset.quiltWidth == texture.width))
            {
                // 画像サイズがプリセットのサイズと一致すれば候補とする
                tilingPresets.Add(preset);
            }
            else
            {
                // サイズが一致しなければ、そのtileX,tileYでサイズを合わせた候補を作成
                tilingPresets.Add(
                    new Quilt.Settings(
                        texture.width, texture.height,
                        preset.viewColumns, preset.viewRows,
                        preset.numViews
                        ));
            }
        }

        // 8x6 == 48 のパターンもさらに調べる。Looking Glass Portrait で普通にQuiltを作るとこうなるため。
        tilingPresets.Add(new Quilt.Settings(texture.width, texture.height, 8, 6, 48));

        // 6x10 のパターンも追加で調べる
        tilingPresets.Add(new Quilt.Settings(texture.width, texture.height, 6, 10, 60));

        // 5x10-2 == 48 のパターンもさらに調べる
        tilingPresets.Add(new Quilt.Settings(texture.width, texture.height, 5, 10, 48));

        // どれも候補に残らなければ初期指定のTilingにしておく
        if (tilingPresets.Count < 1)
        {
            return defaultTiling;
        }

        // テクスチャを配列に取得
        Color[] pixels = texture.GetPixels(0, 0, texture.width, texture.height);

        // この変数にTiling候補ごとの評価値（小さい方が良い）が入る
        float[] score = new float[tilingPresets.Count];

        // 相関をとる周期の調整値。1だと全ピクセルについて相関をとるが遅い。
        int skip = texture.width / 512;     // 固定値 4 としてもでも動いたが、それだと4096pxのとき遅い
        if (skip < 1) skip = 1;             // 最低1はないと無限ループとなってしまう

        // Calculate the score for each Tiling preset
        for (int presetIndex = 0; presetIndex < tilingPresets.Count; presetIndex++)
        {
            var preset = tilingPresets[presetIndex];
            score[presetIndex] = 0;
            
            // Loop for each sample position in a view. It's not necessary to look at all pixels.
            for (int v = 0; v < preset.viewHeight; v += skip)
            {
                for (int u = 0; u < preset.viewWidth; u += skip)
                {
                    Color pixelColor = Color.black;
                    for (int viewNo = 0; viewNo < preset.numViews; viewNo++)
                    {
                        int viewY = viewNo / preset.viewColumns;
                        int viewX = viewNo % preset.viewColumns;

                        // Copy the pixel color as a comparison color
                        Color prevPixelColor = pixelColor;
                        
                        // RGB for the current view
                        pixelColor = pixels[
                            (viewY * preset.viewHeight+ v) * texture.width + (viewX * preset.viewWidth + u)
                        ];
                        
                        // In the first view, only extracts the color of the comparison.
                        if (viewNo < 1) continue;
                        
                        // Difference
                        Color diff = pixelColor - prevPixelColor;
                        
                        // Squared Difference
                        Color variance = diff * diff;

                        // Sum of Squared Difference (RGB each also total)
                        score[presetIndex] += (variance.r + variance.g + variance.b);
                    }
                }
            }
        }

        // 最も評価値が良かったTilingを選択
        int selectedIndex = 0;
        float minScore = float.MaxValue;
        for (int i = 0; i < tilingPresets.Count; i++)
        {
            Debug.Log("Index: " + i + " Order: " + tilingPresets[i].viewColumns + " x " + tilingPresets[i].viewRows + " : " + score[i]);

            if (minScore > score[i])
            {
                selectedIndex = i;
                minScore = score[i];
            }

            // 8x6 だった場合、4x6 のスコアとの差異が 5% 未満なら 8x6 を優先させる
            if (tilingPresets[i].viewColumns == 8 && tilingPresets[i].viewRows == 6)
            {
                if (selectedIndex == index4x6)
                {
                    if (Mathf.Abs(minScore - score[i]) < (minScore * 0.05))
                    {
                        selectedIndex = i;
                        minScore = score[i];
                    }
                }
            }
        }
        
        Debug.Log("Selected preset: " + selectedIndex + " Order: " + tilingPresets[selectedIndex].viewColumns + " x " + tilingPresets[selectedIndex].viewRows);
        return tilingPresets[selectedIndex];
    }
}
