#if UNITY_EDITOR
using System;
using AutoChess.Render;
using NUnit.Framework;
using UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>覆盖受伤棋子血条的创建、进度、父子关系和世界到屏幕位置同步。</summary>
public class UIHpTests
{
    private GameObject _root;
    private Camera _worldCamera;
    private Camera _uiCamera;
    private Canvas _canvas;
    private UIHp _uiHp;
    private UIHpBar _hpBarPrefab;
    private RenderManager _renderManager;
    private GameObject _chessPrefab;
    private GameObject _bulletPrefab;

    [SetUp]
    public void SetUp()
    {
        _root = new GameObject("UIHpTestsRoot");

        GameObject worldCameraObject = new GameObject("Main Camera");
        worldCameraObject.tag = "MainCamera";
        worldCameraObject.transform.SetParent(_root.transform, false);
        worldCameraObject.transform.position = new Vector3(50f, 91.6f, 28f);
        worldCameraObject.transform.rotation = Quaternion.Euler(79.04f, 0f, 0f);
        _worldCamera = worldCameraObject.AddComponent<Camera>();
        _worldCamera.orthographic = false;
        _worldCamera.fieldOfView = 60f;
        _worldCamera.nearClipPlane = 0.3f;
        _worldCamera.farClipPlane = 1000f;

        GameObject uiCameraObject = new GameObject("UICamera");
        uiCameraObject.transform.SetParent(_root.transform, false);
        _uiCamera = uiCameraObject.AddComponent<Camera>();
        _uiCamera.orthographic = true;
        _uiCamera.orthographicSize = 5f;
        _uiCamera.cullingMask = 1 << 5;

        GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform));
        canvasObject.layer = 5;
        canvasObject.transform.SetParent(_root.transform, false);
        _canvas = canvasObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceCamera;
        _canvas.worldCamera = _uiCamera;
        canvasObject.AddComponent<CanvasScaler>();
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject uiHpObject = new GameObject("UIHp", typeof(RectTransform));
        uiHpObject.layer = 5;
        uiHpObject.transform.SetParent(canvasObject.transform, false);
        RectTransform uiHpRect = uiHpObject.GetComponent<RectTransform>();
        uiHpRect.anchorMin = Vector2.zero;
        uiHpRect.anchorMax = Vector2.one;
        uiHpRect.offsetMin = Vector2.zero;
        uiHpRect.offsetMax = Vector2.zero;
        uiHpRect.pivot = new Vector2(0.5f, 0.5f);
        _uiHp = uiHpObject.AddComponent<UIHp>();

        _hpBarPrefab = CreateHpBarPrefab();
        SetObjectReference(_uiHp, "_hpBarPrefab", _hpBarPrefab);
        _uiHp.SetWorldCamera(_worldCamera);

        _chessPrefab = CreateChessPrefab();
        _bulletPrefab = new GameObject("BulletViewPrefab");
        _bulletPrefab.transform.SetParent(_root.transform, false);
        _bulletPrefab.AddComponent<BulletView>();
        _renderManager = new RenderManager(_chessPrefab, _bulletPrefab, _root.transform);

        Canvas.ForceUpdateCanvases();
    }

    [TearDown]
    public void TearDown()
    {
        _renderManager?.Dispose();
        _renderManager = null;
        DestroyImmediateIfExists(_root);
        _uiHp = null;
        _hpBarPrefab = null;
        _chessPrefab = null;
        _bulletPrefab = null;
        _worldCamera = null;
        _uiCamera = null;
        _canvas = null;
    }

    /// <summary>验证受伤进度会在 UIHp 下创建血条并写入 fillAmount。</summary>
    [Test]
    public void SyncChessHpCreatesBarWithProgress()
    {
        _uiHp.SyncChessHp(11, 0.4f);

        Assert.That(_uiHp.HpBarCount, Is.EqualTo(1));
        Assert.That(_uiHp.TryGetHpBar(11, out UIHpBar bar), Is.True);
        Assert.That(bar.transform.parent, Is.EqualTo(_uiHp.transform));
        Assert.That(GetProgressImage(bar).fillAmount, Is.EqualTo(0.4f).Within(0.0001f));
    }

    /// <summary>验证满血、归零和非法 UniqueId 不会留下血条。</summary>
    [Test]
    public void SyncChessHpIgnoresFullZeroAndInvalidIds()
    {
        _uiHp.SyncChessHp(11, 0.4f);
        _uiHp.SyncChessHp(11, 1f);
        Assert.That(_uiHp.HpBarCount, Is.EqualTo(0));

        _uiHp.SyncChessHp(12, 0.2f);
        _uiHp.SyncChessHp(12, 0f);
        Assert.That(_uiHp.HpBarCount, Is.EqualTo(0));

        _uiHp.SyncChessHp(0, 0.5f);
        _uiHp.SyncChessHp(-3, 0.5f);
        Assert.That(_uiHp.HpBarCount, Is.EqualTo(0));
    }

    /// <summary>验证 ApplyRenderFrame 只保留受伤棋子血条，并清掉快照中消失的条目。</summary>
    [Test]
    public void ApplyRenderFrameKeepsOnlyInjuredChess()
    {
        ChessView first = _renderManager.CreateChessView(1, 1);
        ChessView second = _renderManager.CreateChessView(2, 2);
        first.SetPosition(new Vector2(40f, 40f));
        second.SetPosition(new Vector2(60f, 60f));

        _uiHp.ApplyRenderFrame(
            new BattleRenderFrame(
                1,
                new[]
                {
                    new ChessRenderSnapshot(1, 1, new Vector2(40f, 40f), Vector2.up, 0.5f),
                    new ChessRenderSnapshot(2, 2, new Vector2(60f, 60f), Vector2.up, 1f)
                },
                Array.Empty<BulletRenderSnapshot>()),
            _renderManager);

        Assert.That(_uiHp.HpBarCount, Is.EqualTo(1));
        Assert.That(_uiHp.TryGetHpBar(1, out UIHpBar injured), Is.True);
        Assert.That(_uiHp.TryGetHpBar(2, out _), Is.False);
        Assert.That(GetProgressImage(injured).fillAmount, Is.EqualTo(0.5f).Within(0.0001f));

        _uiHp.ApplyRenderFrame(
            new BattleRenderFrame(2, Array.Empty<ChessRenderSnapshot>(), Array.Empty<BulletRenderSnapshot>()),
            _renderManager);
        Assert.That(_uiHp.HpBarCount, Is.EqualTo(0));
    }

    /// <summary>验证血条挂在 UIHp 下，位置等于 HpTransform 的世界到 Canvas 换算。</summary>
    [Test]
    public void BarFollowsHpTransformWithoutParentingToChessView()
    {
        ChessView view = _renderManager.CreateChessView(11, 1);
        view.SetPosition(new Vector2(50f, 50f));
        Assert.That(_renderManager.TryGetChessView(11, out ChessView lookedUp), Is.True);
        Assert.That(lookedUp, Is.SameAs(view));
        Assert.That(view.HpTransform, Is.Not.Null);

        _uiHp.ApplyRenderFrame(
            new BattleRenderFrame(
                1,
                new[] { new ChessRenderSnapshot(11, 1, new Vector2(50f, 50f), Vector2.up, 0.3f) },
                Array.Empty<BulletRenderSnapshot>()),
            _renderManager);

        Assert.That(_uiHp.TryGetHpBar(11, out UIHpBar bar), Is.True);
        Assert.That(bar.transform.parent, Is.EqualTo(_uiHp.transform));
        Assert.That(bar.transform.parent, Is.Not.EqualTo(view.HpTransform));
        AssertAnchoredPositionMatchesWorld(bar, view.HpTransform.position);

        view.HpTransform.localPosition = new Vector3(0f, 4f, 0f);
        _uiHp.SyncAllBarPositions();
        AssertAnchoredPositionMatchesWorld(bar, view.HpTransform.position);
    }

    /// <summary>验证找不到对应 ChessView 时位置同步会销毁血条。</summary>
    [Test]
    public void SyncAllBarPositionsRemovesBarsWithoutChessView()
    {
        _uiHp.ApplyRenderFrame(
            new BattleRenderFrame(
                1,
                new[] { new ChessRenderSnapshot(8, 1, Vector2.zero, Vector2.up, 0.6f) },
                Array.Empty<BulletRenderSnapshot>()),
            _renderManager);

        Assert.That(_uiHp.HpBarCount, Is.EqualTo(1));
        _uiHp.SyncAllBarPositions();
        Assert.That(_uiHp.HpBarCount, Is.EqualTo(0));
    }

    private void AssertAnchoredPositionMatchesWorld(UIHpBar bar, Vector3 worldPosition)
    {
        Canvas.ForceUpdateCanvases();
        Vector3 screenPoint = _worldCamera.WorldToScreenPoint(worldPosition);
        Assert.That(screenPoint.z, Is.GreaterThan(0f));
        Assert.That(
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _uiHp.transform as RectTransform,
                screenPoint,
                _uiCamera,
                out Vector2 expected),
            Is.True);
        RectTransform barRect = bar.transform as RectTransform;
        Assert.That(barRect.anchoredPosition.x, Is.EqualTo(expected.x).Within(0.1f));
        Assert.That(barRect.anchoredPosition.y, Is.EqualTo(expected.y).Within(0.1f));
    }

    private UIHpBar CreateHpBarPrefab()
    {
        GameObject prefab = new GameObject("UIHpBarPrefab", typeof(RectTransform));
        prefab.transform.SetParent(_root.transform, false);
        RectTransform rect = prefab.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(173f, 40f);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        GameObject progressObject = new GameObject("Progress", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        progressObject.transform.SetParent(prefab.transform, false);
        Image progress = progressObject.GetComponent<Image>();
        progress.type = Image.Type.Filled;
        progress.fillMethod = Image.FillMethod.Horizontal;
        progress.fillAmount = 1f;

        UIHpBar bar = prefab.AddComponent<UIHpBar>();
        SetObjectReference(bar, "_progressBar", progress);
        return bar;
    }

    private GameObject CreateChessPrefab()
    {
        GameObject prefab = new GameObject("ChessViewPrefab");
        prefab.transform.SetParent(_root.transform, false);
        ChessView view = prefab.AddComponent<ChessView>();
        GameObject hpPointObject = new GameObject("HpPoint");
        hpPointObject.transform.SetParent(prefab.transform, false);
        hpPointObject.transform.localPosition = new Vector3(0f, 2.55f, 0f);
        SetObjectReference(view, "_hpTransform", hpPointObject.transform);
        return prefab;
    }

    private static Image GetProgressImage(UIHpBar bar)
    {
        return bar.GetComponentInChildren<Image>(true);
    }

    private static void SetObjectReference(UnityEngine.Object target, string fieldName, UnityEngine.Object value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        serializedObject.FindProperty(fieldName).objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void DestroyImmediateIfExists(GameObject target)
    {
        if (target != null)
        {
            UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
#endif