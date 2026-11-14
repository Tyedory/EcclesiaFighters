using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace EcclesiaFighters
{
    public class ParticleSpawner : MonoBehaviour
    {
        public RectTransform spawnArea;
        public Sprite particleSprite;
        public int count = 30;
        public float minSpeed=60f, maxSpeed=140f, minSz=2f, maxSz=5f;

        void Start() { StartCoroutine(SpawnLoop()); }

        IEnumerator SpawnLoop()
        {
            for (int i = 0; i < count; i++) { Spawn(); yield return new WaitForSeconds(Random.Range(0.15f,0.45f)); }
        }

        void Spawn()
        {
            var go = new GameObject("P"); go.transform.SetParent(spawnArea, false);
            var img = go.AddComponent<Image>(); if (particleSprite) img.sprite = particleSprite;
            float s = Random.Range(minSz, maxSz);
            var rt = go.GetComponent<RectTransform>(); rt.sizeDelta = new Vector2(s,s);
            rt.anchoredPosition = new Vector2(Random.Range(-spawnArea.rect.width/2f, spawnArea.rect.width/2f), -spawnArea.rect.height/2f);
            img.color = new Color(0.78f, 0.59f, 0.05f, 0f);
            StartCoroutine(Anim(rt, img, Random.Range(minSpeed, maxSpeed)));
        }

        IEnumerator Anim(RectTransform rt, Image img, float spd)
        {
            float h = spawnArea ? spawnArea.rect.height : 800f, t=0, dur=h/spd;
            Vector2 s = rt.anchoredPosition;
            while (t < dur) { t += Time.deltaTime; float p=t/dur; rt.anchoredPosition=s+Vector2.up*(h*p); float al=p<.1f?p/.1f:(p>.85f?(1-p)/.15f:1f); img.color=new Color(img.color.r,img.color.g,img.color.b,al*.6f); yield return null; }
            Destroy(rt.gameObject);
        }
    }
}
