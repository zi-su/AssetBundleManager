using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// AssetBundleを参照カウンタ式で管理するオブジェクト
/// </summary>
public class AssetBundleObject
{
    //バンドル名
    string _bundleName = null;

    //バンドル参照カウント
    int _refCount = 0;

    string _path = null;

    bool _loading = false;

    //ロードアセット参照カウント
    int _refAssetCount = 0;

    public string BundleName
    {
        get { return _bundleName; }
    }
    public int RefCount
    {
        get { return _refCount; }
    }
    public int RefAssetCount{
        get{return _refAssetCount;}
        set { _refAssetCount = value; }
    }
    public bool Loading
    {
        get { return _loading; }
    }
    public bool LoadingAsset
    {
        get { return _refAssetCount != 0; }
    }

    public AssetBundle Bundle
    {
        get { return _bundle; }
    }
    AssetBundle _bundle;
    public AssetBundleObject(string bundleName, string path="")
    {
        _bundleName = bundleName;
        Debug.Log("new AssetBundleObject:" + _bundleName);
        _path = System.IO.Path.Combine(Application.dataPath, "StreamingAssets", path);
    }

    public IEnumerator LoadBundle()
    {
        if (_refCount == 0)
        {
            _loading = true;
            _refCount++;
            var req = AssetBundle.LoadFromFileAsync(System.IO.Path.Combine(_path, _bundleName));
            yield return req;
            _bundle = req.assetBundle;
        }
        else
        {
            _refCount++;
        }
        _loading = false;
        Debug.Log("LoadBundle:" + _bundleName + ":" + _refCount);
    }

    public void UnloadBundle()
    {
        _refCount--;
        Debug.Log("UnloadBundle:" + _bundleName + ":" + _refCount);
        if (_refCount == 0)
        {
            _bundle.Unload(false);
        }
    }
}

/// <summary>
/// アセットバンドルオブジェクトを管理するマネージャー
/// </summary>
public class AssetBundleManager : MonoBehaviour
{
    [SerializeField]
    string _manifestName;
    AssetBundleManifest _manifest;
    string _streaminAssetPath;

    List<AssetBundleObject> _bundleObjectList = new List<AssetBundleObject>();

    static AssetBundleManager _instance;
    static public AssetBundleManager Instance
    {
        get
        {
            if(_instance == null)
            {
                _instance = FindObjectOfType<AssetBundleManager>();
            }
            return _instance;
        }
    }
    // Start is called before the first frame update
    void Awake()
    {
        DontDestroyOnLoad(gameObject);
        _streaminAssetPath = System.IO.Path.Combine(Application.dataPath, "StreamingAssets");
        StartCoroutine(LoadManifest());
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    /// <summary>
    /// アセットバンドルマニフェストのロード。起動時に一度する
    /// </summary>
    /// <returns></returns>
    public IEnumerator LoadManifest()
    {
        var req = AssetBundle.LoadFromFileAsync(System.IO.Path.Combine(_streaminAssetPath, _manifestName));
        yield return req;
        var asset = req.assetBundle.LoadAssetAsync<AssetBundleManifest>("AssetBundleManifest");
        yield return asset;
        _manifest = asset.asset as AssetBundleManifest;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="bundleName">バンドル名</param>
    /// <param name="path">StreamingAssets/以下のフォルダパス</param>
    public void LoadBundle(string bundleName, string path="")
    {
        //依存バンドルロード
        var dependencies = _manifest.GetAllDependencies(bundleName);
        foreach (var d in dependencies)
        {
            //bundleObjectListに含まれていたら取得
            var dobj = _bundleObjectList.Find((b) => { return b.BundleName == d; });
            if(dobj == null)
            {
                //なければnewして生成
                dobj = new AssetBundleObject(d, path);
                _bundleObjectList.Add(dobj);
            }
            StartCoroutine(dobj.LoadBundle());
        }

        //本体バンドルロード
        var obj = _bundleObjectList.Find((b) => { return b.BundleName == bundleName; });
        if(obj == null)
        {
            obj = new AssetBundleObject(bundleName, path);
            _bundleObjectList.Add(obj);
        }
        StartCoroutine(obj.LoadBundle());
    }

    public void UnloadBundle(string bundleName)
    {
        //依存バンドルをアンロード
        //参照カウンタを減らして、0になれば実際にアンロード
        var dependencies = _manifest.GetAllDependencies(bundleName);
        foreach (var d in dependencies)
        {
            var obj = _bundleObjectList.Find((b) => { return b.BundleName == d; });
            if (obj != null)
            {
                obj.UnloadBundle();
                if(obj.RefCount == 0)
                {
                    _bundleObjectList.Remove(obj);
                    obj = null;
                }
            }
        }

        //本体バンドルをアンロード
        //参照カウンタを減らして、0になれば実際にアンロード
        var bundle = _bundleObjectList.Find((b) => { return b.BundleName == bundleName; });
        if (bundle != null)
        {
            bundle.UnloadBundle();
            if (bundle.RefCount == 0)
            {
                _bundleObjectList.Remove(bundle);
                bundle = null;
            }
        }
    }

    /// <summary>
    /// バンドルからアセットを読み込み、完了時にcompletedActionを実行する
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="bundleName"></param>
    /// <param name="assetName"></param>
    /// <param name="completedAction"></param>
    /// <returns></returns>
    public IEnumerator LoadAsset<T>(string bundleName, string assetName, System.Action<T> completedAction) where T : Object
    {
        var bundleObj = _bundleObjectList.Find((b) => { return b.BundleName == bundleName; });
        if (bundleObj != null)
        {
            bundleObj.RefAssetCount++;
            yield return new WaitWhile(()=> { return bundleObj.Loading; });
            var req = bundleObj.Bundle.LoadAssetAsync<T>(assetName);
            yield return req;
            completedAction.Invoke(req.asset as T);
            bundleObj.RefAssetCount--;
        }
    }

    /// <summary>
    /// アセットバンドルロード中チェック
    /// </summary>
    /// <param name="bundleName"></param>
    /// <returns></returns>
    public bool IsLoadingBundle(string bundleName)
    {
        if (_manifest == null) return true;
        bool ret = false;
        var dependencies = _manifest.GetAllDependencies(bundleName);
        //依存バンドルのロード中チェック
        foreach (var d in dependencies)
        {
            var dobj = _bundleObjectList.Find((b) => { return b.BundleName == d; });
            if (dobj != null)
            {
                ret |= dobj.Loading;
            }
        }

        //本体バンドルのロード中チェック
        var obj = _bundleObjectList.Find((b) => { return b.BundleName == bundleName; });
        if (obj != null)
        {
            ret |= obj.Loading;
        }
        return ret;
    }

    /// <summary>
    /// バンドルからアセットロード中か
    /// </summary>
    /// <param name="bundleName"></param>
    /// <returns></returns>
    public bool IsLoadingAsset(string bundleName)
    {
        var bundleObj = _bundleObjectList.Find((b) => { return b.BundleName == bundleName; });
        if(bundleObj != null)
        {
            return bundleObj.LoadingAsset;
        }
        return false;
    }

    /// <summary>
    /// バンドルのロード中またはバンドルからアセットロード中か
    /// </summary>
    /// <param name="bundleName"></param>
    /// <returns></returns>
    public bool IsLoading(string bundleName)
    {
        var bundleObj = _bundleObjectList.Find((b) => { return b.BundleName == bundleName; });
        if (bundleObj != null)
        {
            //依存バンドルのロード中チェック
            var dependencies =_manifest.GetAllDependencies(bundleName);
            bool ret = false;
            foreach (var d in dependencies)
            {
                var dobj = _bundleObjectList.Find((b) => { return b.BundleName == d; });
                if(dobj != null)
                {
                    ret |= dobj.Loading || dobj.LoadingAsset;
                }
            }
            return bundleObj.Loading || bundleObj.LoadingAsset || ret;
        }
        return false;
    }
}