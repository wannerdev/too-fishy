using UnityEngine;

namespace TooFishy
{
    public class UnderwaterCamera : MonoBehaviour
    {
        Stage _lastStage = (Stage)(-1);

        void LateUpdate()
        {
            var gs = GameState.Instance;
            if (gs == null) return;

            bool underwater = gs.PlayerTransform != null && gs.PlayerTransform.position.y <= -0.2f;
            RenderSettings.fog = underwater;
            if (!underwater) return;

            if (_lastStage != gs.PlayerInStage)
            {
                _lastStage = gs.PlayerInStage;
                RenderSettings.fogColor = FishConfig.StageFogColor(_lastStage);
                RenderSettings.fogDensity = FishConfig.StageFogDensity(_lastStage);
                RenderSettings.fogMode = FogMode.ExponentialSquared;
            }
        }
    }
}
