using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine.Networking;
using System.Collections;

[System.Serializable]
public class Soru
{
    public string soru;
    public string[] secenekler;
    public int dogru;
    public string zorluk;
}

[System.Serializable]
public class SoruListesi
{
    public List<Soru> sorular;
}

public class SoruSecici : MonoBehaviour
{
    [Header("Paneller")]
    public GameObject startPanel;
    public GameObject gamePanel;

    [Header("UI Elemanları")]
    public TMP_Text soruText;
    public TMP_Text paraText;
    public TMP_Text sureText;
    public Button[] cevapButonlari;
    public Button cekilButonu;

    [Header("Joker Butonları")]
    public Button yuzdeElliButonu;
    public Button ciftCevapButonu;
    public Button soruDegistirButonu;
    public Button sureDurdurButonu;

    [Header("Sesler")]
    public AudioSource audioSource;
    public AudioClip dogruSes;
    public AudioClip yanlisSes;

    private float kalanSure;
    private int aktifIndex;
    private int kazanilanPara;
    private int barajPara;

    private bool cevapVerildi;
    private bool yuzdeElliKullanildi;
    private bool soruDegistirKullanildi;
    private bool sureDurdurKullanildi;
    private bool sureDurdu;
    private bool ciftCevapAktif;

    private List<Soru> tumSorular = new List<Soru>();
    private List<Soru> oyunSorulari = new List<Soru>();

    private int[] paraTablosu = {
        1000, 2000, 3000, 5000, 10000,
        20000, 30000, 50000, 100000, 200000,
        300000, 500000, 750000, 1000000, 5000000
    };

    void Start()
    {
        startPanel.SetActive(true);
        gamePanel.SetActive(false);
    }

    void Update()
    {
        if (cevapVerildi || !gamePanel.activeSelf || sureDurdu) return;

        if (aktifIndex < 7) // İlk 7 soruda süre var
        {
            kalanSure -= Time.deltaTime;
            sureText.text = "Süre: " + Mathf.Ceil(kalanSure);

            if (kalanSure <= 0)
                Elendin();
        }
        else
        {
            sureText.text = "Süre Yok";
        }
    }

    public void OyunuBaslat()
    {
        StartCoroutine(JsonVerileriniYukle());
    }

    IEnumerator JsonVerileriniYukle()
    {
        string yol = Path.Combine(Application.streamingAssetsPath, "sorular.json");
        string jsonVerisi = "";

        if (yol.Contains("://") || yol.Contains(":///")) // Android/WebGL uyumu
        {
            UnityWebRequest www = UnityWebRequest.Get(yol);
            yield return www.SendWebRequest();
            if (www.result == UnityWebRequest.Result.Success)
                jsonVerisi = www.downloadHandler.text;
        }
        else
        {
            if (File.Exists(yol))
                jsonVerisi = File.ReadAllText(yol);
        }

        if (string.IsNullOrEmpty(jsonVerisi))
        {
            Debug.LogError("JSON dosyası bulunamadı!");
            yield break;
        }

        var data = JsonUtility.FromJson<SoruListesi>(jsonVerisi);
        tumSorular = data.sorular;

        // Soruları zorluk derecelerine göre karıştırıp seçiyoruz
        var kolaylar = tumSorular.Where(s => s.zorluk == "kolay").OrderBy(x => Random.value).Take(5).ToList();
        var ortalar = tumSorular.Where(s => s.zorluk == "orta").OrderBy(x => Random.value).Take(5).ToList();
        var zorlar = tumSorular.Where(s => s.zorluk == "zor").OrderBy(x => Random.value).Take(5).ToList();

        if (kolaylar.Count < 5 || ortalar.Count < 5 || zorlar.Count < 5)
        {
            Debug.LogError("Yetersiz soru sayısı! JSON'da her zorluktan en az 5 soru olmalı.");
            yield break;
        }

        oyunSorulari.Clear();
        oyunSorulari.AddRange(kolaylar);
        oyunSorulari.AddRange(ortalar);
        oyunSorulari.AddRange(zorlar);

        BaslangicAyarlariniYap();
    }

    void BaslangicAyarlariniYap()
    {
        startPanel.SetActive(false);
        gamePanel.SetActive(true);
        aktifIndex = 0;
        kazanilanPara = 0;
        barajPara = 0;
        paraText.text = "Para: 0 TL";

        ResetJokerVisuals();
        YeniSoru();
    }

    void YeniSoru()
    {
        if (aktifIndex >= 15)
        {
            soruText.text = "TEBRİKLER!\n" + kazanilanPara.ToString("N0") + " TL KAZANDINIZ";
            Invoke(nameof(AnaMenuyeDon), 4f);
            return;
        }

        cevapVerildi = false;
        sureDurdu = false;
        ciftCevapAktif = false;

        cekilButonu.gameObject.SetActive(aktifIndex > 0);

        Soru s = oyunSorulari[aktifIndex];

        // Süre Ayarları
        if (aktifIndex < 5) kalanSure = 15;
        else if (aktifIndex < 7) kalanSure = 45;
        else kalanSure = 0;

        soruText.text = s.soru;

        for (int i = 0; i < 4; i++)
        {
            cevapButonlari[i].interactable = true;
            cevapButonlari[i].image.color = Color.white;
            cevapButonlari[i].GetComponentInChildren<TMP_Text>().text = s.secenekler[i];
        }

        // Joker butonlarının görünürlük kuralları
        soruDegistirButonu.gameObject.SetActive(!soruDegistirKullanildi && aktifIndex >= 7);
        sureDurdurButonu.gameObject.SetActive(!sureDurdurKullanildi && aktifIndex < 7);
    }

    public void CevapKontrol(int index)
    {
        if (cevapVerildi) return;

        Soru s = oyunSorulari[aktifIndex];

        if (index == s.dogru)
        {
            cevapVerildi = true;
            cevapButonlari[index].image.color = Color.green;
            if (audioSource && dogruSes) audioSource.PlayOneShot(dogruSes);

            kazanilanPara = paraTablosu[aktifIndex];
            paraText.text = "Para: " + kazanilanPara.ToString("N0") + " TL";

            // Baraj noktaları (5. ve 10. soru)
            if (aktifIndex == 4 || aktifIndex == 9)
                barajPara = kazanilanPara;

            // Çift cevap jokerini 5. sorudan sonra aktif et (veya istediğin kurala göre)
            if (aktifIndex == 4) ciftCevapButonu.gameObject.SetActive(true);

            aktifIndex++;
            Invoke(nameof(YeniSoru), 2f);
        }
        else
        {
            // Çift Cevap Jokeri Kontrolü
            if (ciftCevapAktif)
            {
                ciftCevapAktif = false;
                cevapButonlari[index].image.color = Color.red;
                cevapButonlari[index].interactable = false;
                if (audioSource && yanlisSes) audioSource.PlayOneShot(yanlisSes);
                return; // Oyuna devam et
            }

            // Normal Elenme
            cevapButonlari[index].image.color = Color.red;
            cevapButonlari[s.dogru].image.color = Color.green;
            if (audioSource && yanlisSes) audioSource.PlayOneShot(yanlisSes);

            cevapVerildi = true;
            Invoke(nameof(Elendin), 2f);
        }
    }

    // =================== JOKERLER ===================

    public void JokerYuzdeElli()
    {
        if (yuzdeElliKullanildi) return;
        yuzdeElliKullanildi = true;
        yuzdeElliButonu.interactable = false;

        Soru s = oyunSorulari[aktifIndex];
        var yanlislar = Enumerable.Range(0, 4)
            .Where(i => i != s.dogru)
            .OrderBy(x => Random.value)
            .Take(2);

        foreach (int i in yanlislar)
        {
            cevapButonlari[i].interactable = false;
            cevapButonlari[i].image.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            cevapButonlari[i].GetComponentInChildren<TMP_Text>().text = "";
        }
    }

    public void CiftCevapJoker()
    {
        if (ciftCevapAktif) return;
        ciftCevapAktif = true;
        ciftCevapButonu.interactable = false;
    }

    public void SoruDegistirJoker()
    {
        if (soruDegistirKullanildi) return;

        Soru mevcut = oyunSorulari[aktifIndex];
        // Tüm havuzdan henüz oyun listesinde olmayan bir soru bul
        var yeniSoru = tumSorular
            .Where(s => s.zorluk == mevcut.zorluk && !oyunSorulari.Any(o => o.soru == s.soru))
            .OrderBy(x => Random.value)
            .FirstOrDefault();

        if (yeniSoru != null)
        {
            oyunSorulari[aktifIndex] = yeniSoru;
            soruDegistirKullanildi = true;
            soruDegistirButonu.interactable = false;
            YeniSoru();
        }
    }

    public void SureyiDurdurJoker()
    {
        if (sureDurdurKullanildi || aktifIndex >= 7) return;
        sureDurdu = true;
        sureDurdurKullanildi = true;
        sureText.text = "SÜRE DURDU";
        sureDurdurButonu.interactable = false;
    }

    public void Cekil()
    {
        cevapVerildi = true;
        soruText.text = "OYUNDAN ÇEKİLDİNİZ\nÖdül: " + kazanilanPara.ToString("N0") + " TL";
        Invoke(nameof(AnaMenuyeDon), 3f);
    }

    void Elendin()
    {
        soruText.text = "ELENDİNİZ\nÖdül: " + barajPara.ToString("N0") + " TL";
        Invoke(nameof(AnaMenuyeDon), 3f);
    }

    void AnaMenuyeDon()
    {
        gamePanel.SetActive(false);
        startPanel.SetActive(true);
    }

    void ResetJokerVisuals()
    {
        yuzdeElliKullanildi = false;
        soruDegistirKullanildi = false;
        sureDurdurKullanildi = false;

        yuzdeElliButonu.interactable = true;
        soruDegistirButonu.interactable = true;
        sureDurdurButonu.interactable = true;
        ciftCevapButonu.interactable = true;

        ciftCevapButonu.gameObject.SetActive(false);
    }

    public void OyundanCik()
    {
        Application.Quit();
    }
}