using UnityEngine;

public class UIManager : MonoBehaviour
{
    [SerializeField] private GameObject _mainCanva;

    [Header("Almanac")]
    [SerializeField] private GameObject _codex;
    [SerializeField] private GameObject[] _personnagesPrinc;
    [SerializeField] private GameObject[] _personnagesSecond;

    [SerializeField] private GameObject[] _OgounEvo;
    [SerializeField] private GameObject[] _BSEvo;

    private int lastID;

    private void Start()
    {
        lastID = -1;
    }

    private void Update()
    {
        if (_codex.activeInHierarchy && Input.GetKey(KeyCode.Escape))
            CloseCodex();
    }

    public void TryOpenCodex()
    {
        if (!_codex.activeInHierarchy)
            OpenCodex();
    }

    private void OpenCodex()
    {
        _mainCanva.SetActive(false);
        _codex.SetActive(true);
        _personnagesPrinc[0].SetActive(true);
    }

    private void CloseCodex()
    {
        for (int i = 0; i < _personnagesSecond.Length; i++)
        {
            _personnagesSecond[i].SetActive(false);
        }
        for (int i = 0; i < _personnagesPrinc.Length; i++)
        {
            _personnagesPrinc[i].SetActive(false);
        }
        _codex.SetActive(false);
        _mainCanva.SetActive(true);
    }

    public void TryOpenEvoOgun(int id)
    {

        if (lastID == id)
        {
            OpenEvoOgun(false, id);
            lastID = -1;
        }
        else
        {
            if (lastID != -1)
                OpenEvoOgun(false, lastID);
            OpenEvoOgun(true, id);
            lastID = id;
        }
    }

    private void OpenEvoOgun(bool active, int id)
    {
        _OgounEvo[id].SetActive(active);
    }
    public void TryOpenEvoBS(int id) {

        if (lastID == id) {
            OpenEvoBS(false, id);
            lastID = -1;
        } else {
            if(lastID != -1)
                OpenEvoBS(false, lastID);
            OpenEvoBS(true, id);
            lastID = id;
        }
    }

    private void OpenEvoBS(bool active, int id) {
        _BSEvo[id].SetActive(active);
    }
}
