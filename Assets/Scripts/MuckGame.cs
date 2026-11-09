using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace CopperCamp
{
    /// <summary>
    /// The Copper Camp mucker's shift, as a 3D drift. Press Space (or click / tap) when the
    /// marker is inside the green zone to dig, and the load flies into the ore car.
    /// Misses tire your back and shrink the zone. Your back goes at full strain.
    /// The builder menu (Copper Camp > Build Drift Scene) fills in every reference below.
    /// </summary>
    public class MuckGame : MonoBehaviour
    {
        [Header("Filled in by the builder")]
        public Text hudText;
        public Text messageText;
        public RectTransform meterZone;
        public RectTransform meterMarker;
        public RectTransform fatigueFill;
        public Transform shovelPivot;
        public Transform rock;
        public Transform pile;
        public Transform oreCar;
        public Light headLamp;
        public Camera viewCamera;

        enum Phase { Ready, Dig, Throw, Over }

        const float ShiftSeconds = 40f;
        const int Target = 22;
        const float ThrowSeconds = 0.45f;
        const int FullShiftPay = 87;   // same per-shift pay the browser game shows for the mucker

        Phase phase = Phase.Ready;
        float timeLeft;
        int loads;
        int misses;
        float fatigue;
        float markerPos;
        float markerDir = 1f;
        float zoneCenter = 0.5f;
        float zoneWidth = 0.22f;
        float throwT;
        float messageTimer;
        float shake;
        Vector3 cameraRest;
        Quaternion shovelRest;
        float lampBase = 1f;

        void Start()
        {
            if (viewCamera != null) cameraRest = viewCamera.transform.localPosition;
            if (shovelPivot != null) shovelRest = shovelPivot.localRotation;
            if (headLamp != null) lampBase = headLamp.intensity;
            ResetShift();
        }

        void ResetShift()
        {
            phase = Phase.Ready;
            timeLeft = ShiftSeconds;
            loads = 0;
            misses = 0;
            fatigue = 0f;
            markerPos = 0f;
            markerDir = 1f;
            if (rock != null) rock.gameObject.SetActive(false);
            Say("Press Space, click or tap to start the shift", 0f);
            UpdateUi();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Flicker();

            switch (phase)
            {
                case Phase.Ready:
                    if (Pressed()) { phase = Phase.Dig; NewZone(); Say("Wait for the marker to cross the green, then dig", 2.5f); }
                    break;

                case Phase.Dig:
                    timeLeft -= dt;
                    MoveMarker(dt);
                    if (Pressed()) TryDig();
                    CheckEnd();
                    break;

                case Phase.Throw:
                    timeLeft -= dt;
                    AnimateThrow(dt);
                    CheckEnd();
                    break;

                case Phase.Over:
                    if (Restarted()) ResetShift();
                    break;
            }

            if (messageTimer > 0f)
            {
                messageTimer -= dt;
                if (messageTimer <= 0f && messageText != null && phase != Phase.Over && phase != Phase.Ready) messageText.text = "";
            }
            ShakeCamera(dt);
            UpdateUi();
        }

        void MoveMarker(float dt)
        {
            float speed = 0.9f + fatigue * 0.9f;
            markerPos += markerDir * speed * dt;
            if (markerPos >= 1f) { markerPos = 1f; markerDir = -1f; }
            if (markerPos <= 0f) { markerPos = 0f; markerDir = 1f; }
        }

        void NewZone()
        {
            zoneWidth = Mathf.Lerp(0.24f, 0.10f, fatigue);
            zoneCenter = Random.Range(0.2f, 0.8f);
        }

        void TryDig()
        {
            bool good = Mathf.Abs(markerPos - zoneCenter) <= zoneWidth * 0.5f;
            if (good)
            {
                fatigue = Mathf.Min(1f, fatigue + 0.02f);
                phase = Phase.Throw;
                throwT = 0f;
                if (rock != null && pile != null)
                {
                    rock.gameObject.SetActive(true);
                    rock.position = pile.position + Vector3.up * 0.35f;
                }
                Say("Good dig", 0.8f);
            }
            else
            {
                misses++;
                fatigue = Mathf.Min(1f, fatigue + 0.07f);
                shake = 0.25f;
                Say("The shovel skids off the rock", 1.0f);
                NewZone();
            }
        }

        void AnimateThrow(float dt)
        {
            throwT += dt / ThrowSeconds;
            float t = Mathf.Clamp01(throwT);
            if (rock != null && pile != null && oreCar != null)
            {
                Vector3 a = pile.position + Vector3.up * 0.35f;
                Vector3 b = oreCar.position + Vector3.up * 0.7f;
                Vector3 p = Vector3.Lerp(a, b, t);
                p.y += Mathf.Sin(t * Mathf.PI) * 1.1f;
                rock.position = p;
            }
            if (shovelPivot != null)
            {
                float swing = Mathf.Sin(t * Mathf.PI) * -55f;
                shovelPivot.localRotation = shovelRest * Quaternion.Euler(swing, 0f, 0f);
            }
            if (throwT >= 1f)
            {
                loads++;
                if (rock != null) rock.gameObject.SetActive(false);
                if (shovelPivot != null) shovelPivot.localRotation = shovelRest;
                phase = Phase.Dig;
                NewZone();
            }
        }

        void CheckEnd()
        {
            if (fatigue >= 1f)
            {
                Finish("Your back goes. The shift boss sends you up early.");
            }
            else if (timeLeft <= 0f)
            {
                timeLeft = 0f;
                Finish("The whistle blows.");
            }
        }

        void Finish(string headline)
        {
            phase = Phase.Over;
            if (rock != null) rock.gameObject.SetActive(false);
            float progress = Mathf.Clamp01(loads / (float)Target);
            int pay = Mathf.RoundToInt(FullShiftPay * (0.7f + 0.5f * progress));
            if (fatigue >= 1f) pay = Mathf.RoundToInt(pay * 0.8f);
            Say(headline + "\n" + loads + " of " + Target + " cars loaded, " + misses + " misses.  Pay $" + pay + ".\nPress R, click or tap to work another shift.", 0f);
        }

        void Say(string text, float seconds)
        {
            if (messageText != null) messageText.text = text;
            messageTimer = seconds;
        }

        void UpdateUi()
        {
            if (hudText != null)
                hudText.text = "Loads " + loads + " / " + Target + "     Time " + Mathf.CeilToInt(Mathf.Max(0f, timeLeft)) + "s     Misses " + misses;

            if (meterZone != null)
            {
                meterZone.anchorMin = new Vector2(zoneCenter - zoneWidth * 0.5f, 0f);
                meterZone.anchorMax = new Vector2(zoneCenter + zoneWidth * 0.5f, 1f);
                meterZone.offsetMin = Vector2.zero;
                meterZone.offsetMax = Vector2.zero;
            }
            if (meterMarker != null)
            {
                meterMarker.anchorMin = new Vector2(markerPos, 0f);
                meterMarker.anchorMax = new Vector2(markerPos, 1f);
                meterMarker.offsetMin = new Vector2(-4f, -4f);
                meterMarker.offsetMax = new Vector2(4f, 4f);
            }
            if (fatigueFill != null)
            {
                fatigueFill.anchorMin = new Vector2(0f, 0f);
                fatigueFill.anchorMax = new Vector2(fatigue, 1f);
                fatigueFill.offsetMin = Vector2.zero;
                fatigueFill.offsetMax = Vector2.zero;
            }
        }

        void Flicker()
        {
            if (headLamp == null) return;
            float n = Mathf.PerlinNoise(Time.time * 3f, 0.5f);
            headLamp.intensity = lampBase * (0.9f + 0.1f * n);
        }

        void ShakeCamera(float dt)
        {
            if (viewCamera == null) return;
            if (shake > 0f)
            {
                shake = Mathf.Max(0f, shake - dt);
                Vector3 jitter = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f) * shake * 0.08f;
                viewCamera.transform.localPosition = cameraRest + jitter;
            }
            else
            {
                viewCamera.transform.localPosition = cameraRest;
            }
        }

        static bool Pressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            Mouse ms = Mouse.current;
            Touchscreen ts = Touchscreen.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame) return true;
            if (ms != null && ms.leftButton.wasPressedThisFrame) return true;
            if (ts != null && ts.primaryTouch.press.wasPressedThisFrame) return true;
            return false;
#else
            return Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0);
#endif
        }

        static bool Restarted()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame) return true;
            return Pressed();
#else
            return Input.GetKeyDown(KeyCode.R) || Pressed();
#endif
        }
    }
}
