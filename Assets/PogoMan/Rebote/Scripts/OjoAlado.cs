using UnityEngine;
using UnityEngine.Events;

namespace PogoMan.Rebote
{
    [System.Serializable] public class ImpactoOjo : UnityEvent<Transform, float> {}
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public sealed class OjoAlado : MonoBehaviour
    {
        public Transform objetivo;
        public LayerMask obstaculos;
        public LayerMask capasDano = ~0;
        [Tooltip("Opcional. Asignar la hurtbox exacta si el jugador usa un trigger para recibir golpes.")]
        public Collider hurtboxObjetivo;
        public float vidaMaxima = 32, velocidad = 4.2f, radioAlerta = 15, dano = 8;
        public ImpactoOjo alAcertar = new ImpactoOjo();
        public UnityEvent alMorir = new UnityEvent();
        public Animator animador;
        enum Estado { Vigilar, Perseguir, Apuntar, Embestir, Herido, Caer }
        Estado estado;
        Rigidbody cuerpo;
        SphereCollider esfera;
        float vida, reloj, siguienteAtaque, buscar;
        Vector3 rumbo;
        bool acerto;
        Vector3 Centro => cuerpo.position + cuerpo.rotation * esfera.center;
        public float Vida => vida;
        // Integración: el juego (OjoEnemy) lleva la vida y la sincroniza aquí.
        public void ConfigurarVida(float maxima, float actual)
        {
            vidaMaxima = maxima;
            vida = Mathf.Clamp(actual, 0, maxima);
        }

        void Awake()
        {
            cuerpo = GetComponent<Rigidbody>();
            esfera = GetComponent<SphereCollider>();
            if (!animador) animador = GetComponentInChildren<Animator>();
            cuerpo.useGravity = false;
            cuerpo.isKinematic = true;
        }
        void OnEnable()
        {
            vida = vidaMaxima;
            esfera.enabled = true;
            siguienteAtaque = 1;
            buscar = 0;
            Cambiar(Estado.Vigilar);
        }
        void Cambiar(Estado nuevo)
        {
            estado = nuevo;
            reloj = 0;
            if (animador && animador.runtimeAnimatorController)
                animador.CrossFadeInFixedTime("Base Layer.Ojo_" + nuevo, .07f, 0, 0);
        }
        public void RecibirDano(float cantidad)
        {
            if (estado == Estado.Caer || cantidad <= 0) return;
            vida = Mathf.Max(0, vida - cantidad);
            if (vida == 0)
            {
                esfera.enabled = false;
                Cambiar(Estado.Caer);
                alMorir.Invoke();
                return;
            }
            if (estado != Estado.Herido) Cambiar(Estado.Herido);
        }
        readonly RaycastHit[] contactos = new RaycastHit[24];

        bool Obstaculo(Vector3 origen, float radio, Vector3 eje, float largo, out RaycastHit impacto)
        {
            int count = Physics.SphereCastNonAlloc(origen, radio, eje, contactos, largo, obstaculos, QueryTriggerInteraction.Ignore);
            var hits = count == contactos.Length
                ? Physics.SphereCastAll(origen, radio, eje, largo, obstaculos, QueryTriggerInteraction.Ignore)
                : contactos;
            if (hits != contactos) count = hits.Length;
            impacto = default(RaycastHit);
            float minimo = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var h = hits[i];
                var objeto = h.collider.transform;
                if (h.rigidbody == cuerpo || objeto.IsChildOf(transform)) continue;
                if (objetivo && (objeto == objetivo || objeto.IsChildOf(objetivo))) continue;
                if (h.distance >= minimo) continue;
                impacto = h;
                minimo = h.distance;
            }
            return minimo < float.PositiveInfinity;
        }
        bool Mover(Vector3 paso)
        {
            float largo = paso.magnitude;
            if (largo < .00001f) return true;
            Vector3 eje = paso / largo;
            if (Obstaculo(Centro, esfera.radius, eje, largo + .035f, out var golpe))
            {
                cuerpo.MovePosition(cuerpo.position + eje * Mathf.Clamp(golpe.distance - .035f, 0, largo));
                return false;
            }
            cuerpo.MovePosition(cuerpo.position + paso);
            return true;
        }
        void Girar(Vector3 eje)
        {
            Vector3 plano = Vector3.ProjectOnPlane(eje, Vector3.up);
            if (plano.sqrMagnitude > .001f)
                cuerpo.MoveRotation(Quaternion.RotateTowards(cuerpo.rotation, Quaternion.LookRotation(plano), 240 * Time.fixedDeltaTime));
        }
        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            reloj += dt;
            siguienteAtaque -= dt;
            if (estado == Estado.Caer)
            {
                Mover(Vector3.down * dt * 1.5f);
                if (reloj >= 1) gameObject.SetActive(false);
                return;
            }
            if (estado == Estado.Herido)
            {
                if (reloj >= .42f) Cambiar(Estado.Perseguir);
                return;
            }
            if (!objetivo || !objetivo.gameObject.activeInHierarchy)
            {
                objetivo = null;
                if ((buscar -= dt) <= 0)
                {
                    var jugador = GameObject.FindGameObjectWithTag("Player");
                    if (jugador) objetivo = jugador.transform;
                    buscar = 1;
                }
                if (!objetivo) { if (estado != Estado.Vigilar) Cambiar(Estado.Vigilar); return; }
            }
            Vector3 blanco = objetivo.position + Vector3.up * .9f;
            Vector3 delta = blanco - Centro;
            if (estado == Estado.Embestir)
            {
                // Sweep the target during the dash, even when a fixed step spans its collider.
                float largo = 9.0f * dt;
                if (Obstaculo(Centro, esfera.radius, rumbo, largo + .035f, out var muro))
                    largo = Mathf.Clamp(muro.distance-.035f,0,largo);
                if (!acerto)
                    foreach (var c in Physics.OverlapCapsule(Centro, Centro + rumbo * largo, esfera.radius, capasDano, hurtboxObjetivo ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore))
                        if ((!hurtboxObjetivo || c == hurtboxObjetivo) && (c.transform == objetivo || c.transform.IsChildOf(objetivo)))
                        { acerto = true; alAcertar.Invoke(objetivo, dano); break; }
                if (estado != Estado.Embestir) return;
                if (!Mover(rumbo * (9 * dt)) || reloj >= .40f)
                { siguienteAtaque = 2.2f; Cambiar(Estado.Perseguir); }
                return;
            }
            if (estado == Estado.Apuntar)
            {
                if (reloj < .40f && delta.sqrMagnitude > .001f) rumbo = delta.normalized;
                Girar(rumbo);
                if (reloj >= .66f) { acerto = false; Cambiar(Estado.Embestir); }
                return;
            }
            if (delta.magnitude > radioAlerta * (estado == Estado.Perseguir ? 1.35f : 1f))
            { if (estado != Estado.Vigilar) Cambiar(Estado.Vigilar); return; }
            if (estado != Estado.Perseguir) Cambiar(Estado.Perseguir);
            Girar(delta);
            if (delta.magnitude <= 3.7f && siguienteAtaque <= 0 && delta.sqrMagnitude > .001f &&
                !Obstaculo(Centro, .015f, delta.normalized, delta.magnitude, out _))
            { rumbo = delta.normalized; Cambiar(Estado.Apuntar); return; }
            var destino = blanco + Vector3.up * (.32f + .11f * Mathf.Sin(reloj * 3));
            Vector3 paso = destino-Centro;
            if (paso.sqrMagnitude < .01f) return;
            Vector3 eje = paso.normalized;
            if (Obstaculo(Centro, esfera.radius, eje, .9f, out var pared))
            {
                var lateral = Vector3.Cross(Vector3.up, pared.normal);
                if (Vector3.Dot(lateral,eje)<0) lateral=-lateral;
                eje = (lateral + Vector3.up*.6f + pared.normal*.22f).normalized;
            }
            Mover(eje * Mathf.Min(paso.magnitude, velocidad*dt));
        }
    }
}
