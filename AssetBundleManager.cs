using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// AssetBundleを参照カウンタ式で管理するオブジェクト
/// </summary>
public class AssetBundleObject
{
    //バンドル名
    string _bundleName;

    //参照カウント
    int _refCount;

    string _path;

    bool _loading = false;

    public string BundleName
    {
        get { return _bundleName; }
    }
    public int RefCount
    {
        get { return _refCount; }
    }

    public bool Loading
    {
        get { return _loading; }
    }

    public AssetBundle Bundle
    {
        get { return _bundle; }
    }
    AssetBundle _bundle;
    public AssetBundleObject(string bundleName)
    {
        _bundleName = bundleName;
        Debug.Log("new AssetBundleObject:" + _bundleName);
        _path = System.IO.Path.Combine(Application.dataPath, "StreamingAssets");
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
    AssetBundleManifest _manifest;
    string _streaminAssetPath;

    List<AssetBundleObject> _bundleObjectList = new List<AssetBundleObject>();
    
    // Start is called before the first frame update
    void Start()
    {
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
        var req = AssetBundle.LoadFromFileAsync(System.IO.Path.Combine(_streaminAssetPath, "StandaloneWindows"));
        yield return req;
        var asset = req.assetBundle.LoadAssetAsync<AssetBundleManifest>("AssetBundleManifest");
        yield return asset;
        _manifest = asset.asset as AssetBundleManifest;
    }

    public void LoadBundle(string bundleName)
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
                dobj = new AssetBundleObject(d);
                _bundleObjectList.Add(dobj);
            }
            StartCoroutine(dobj.LoadBundle());
        }

        //本体バンドルロード
        var obj = _bundleObjectList.Find((b) => { return b.BundleName == bundleName; });
        if(obj == null)
        {
            obj = new AssetBundleObject(bundleName);
            _bundleObjectList.Add(obj);
        }
        StartCoroutine(obj.LoadBundle());
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
        foreach (var d in dependencies)
        {
            var dobj = _bundleObjectList.Find((b) => { return b.BundleName == d; });
            if(dobj != null)
            {
                ret |= dobj.Loading;
            }
            
        }
        var obj = _bundleObjectList.Find((b) => { return b.BundleName == bundleName; });
        if(obj != null)
        {
            ret |= obj.Loading;
        }
        return ret;
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
            }
        }
    }

    /// <summary>
    /// バンドルから実際にアセットを読み込み、読み込み完了時にcompletedActopmを実行する
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
            var req = bundleObj.Bundle.LoadAssetAsync<T>(assetName);
            yield return req;
            completedAction.Invoke(req.asset as T);
        }
    }
}


