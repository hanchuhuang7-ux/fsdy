using UnityEngine;

namespace Frontline.UnityGame
{
    // Apply the displacement only during camera rendering. Input and HUD projection keep the stable transform.
    [RequireComponent(typeof(Camera))]
    sealed class BattleCameraShake:MonoBehaviour
    {
        const float Duration=.19f;
        Camera lens;
        float started,strength;
        bool shifted;

        void Awake(){lens=GetComponent<Camera>();}

        internal void Kick(float amount)
        {
            if(amount<=0)return;
            float now=Time.unscaledTime;
            strength=Mathf.Max(amount,strength*Mathf.Clamp01(1-(now-started)/Duration));
            started=now;
        }

        internal void Clear(){strength=0;}

        void OnPreCull()
        {
            if(strength<=0)return;
            float elapsed=Time.unscaledTime-started;
            if(elapsed>=Duration){strength=0;return;}
            float t=elapsed/Duration, amplitude=strength*Mathf.Sin(t*Mathf.PI)*(1-t);
            var offset=new Vector3(Mathf.Sin(elapsed*91)*amplitude,Mathf.Sin(elapsed*127)*amplitude*.59f,0);
            lens.worldToCameraMatrix=Matrix4x4.Translate(offset)*lens.worldToCameraMatrix;
            shifted=true;
        }

        void Restore()
        {
            if(!shifted)return;
            lens.ResetWorldToCameraMatrix();
            shifted=false;
        }

        void OnPostRender(){Restore();}
        void OnDisable(){Restore();}
    }
}
