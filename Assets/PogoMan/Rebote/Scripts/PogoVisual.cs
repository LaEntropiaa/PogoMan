using UnityEngine;

namespace PogoMan.Rebote
{
    // Call once on entry to the corresponding gameplay state.
    [DisallowMultipleComponent]
    public sealed class PogoVisual : MonoBehaviour
    {
        public Animator animador;
        bool enSuelo = true;
        string actual;
        string alTerminar;
        float espera;

        void Awake()
        {
            if (!animador) animador = GetComponentInChildren<Animator>();
            if (animador) animador.applyRootMotion = false;
        }

        void OnEnable() { enSuelo = true; actual = null; Reposo(); }

        void Reproducir(string clip, string siguiente = null, float duracion = 0, bool repetir = false)
        {
            if (!animador || !animador.runtimeAnimatorController || (clip == actual && !repetir)) return;
            actual = clip;
            alTerminar = siguiente;
            espera = duracion;
            animador.CrossFadeInFixedTime("Base Layer.PM_" + clip, .045f, 0, 0);
        }

        void Update()
        {
            if (alTerminar == null || !animador || !animador.isActiveAndEnabled) return;
            espera -= Time.deltaTime * animador.speed;
            if (espera <= 0) Reproducir(alTerminar);
        }

        public void Reposo() { enSuelo = true; Reproducir("Reposo"); }
        public void Cargar() { enSuelo = true; Reproducir("Cargar"); }
        public void Despegar() { enSuelo = false; Reproducir("Despegar", "Aire", 11f / 24f); }
        public void Aire() { enSuelo = false; Reproducir("Aire"); }
        public void Aplastar() { enSuelo = false; Reproducir("Aplastar"); }
        public void Aterrizar() { enSuelo = true; Reproducir("Aterrizar", "Reposo", 14f / 24f); }
        public void Giro(float direccion)
        {
            if (Mathf.Approximately(direccion, 0)) return;
            Reproducir(direccion < 0 ? "GirarIzq" : "GirarDer", enSuelo ? "Reposo" : "Aire", 17f / 24f, true);
        }
        public void Golpe() { Reproducir("Golpe", enSuelo ? "Reposo" : "Aire", 15f / 24f, true); }
    }
}
